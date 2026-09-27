namespace MaksIT.PostClient.Client.Mail;


/// <summary>
/// Headless mailbox sync for every enrolled account.
/// The machine service reads the central store and does not need anyone signed in.
/// </summary>
public sealed class MailSyncEngine {
  public static readonly TimeSpan Interval = TimeSpan.FromMinutes(3);
  private const int ListChunk = 250;

  private readonly ConfigurationFileService _files;
  private readonly MailArchiveCatalog _archive;
  private readonly ISecretStore _secrets;
  private readonly IMailAuthService _auth;
  private readonly IMailSessionFactory _sessions;
  private readonly SemanticSearchService _semantic;

  public MailSyncEngine(
    ConfigurationFileService files,
    MailArchiveCatalog archive,
    ISecretStore secrets,
    IMailAuthService auth,
    IMailSessionFactory sessions,
    SemanticSearchService semantic) {
    _files = files;
    _archive = archive;
    _secrets = secrets;
    _auth = auth;
    _sessions = sessions;
    _semantic = semantic;
  }

  public async Task RunAsync(CancellationToken cancellationToken) {
    var server = MailSyncIdentity.IsServiceAccount();
    if (!server && !MailSyncIdentity.IsOwnAccount(out var reason)) {
      AppLog.Write(reason);
      return;
    }

    AppPaths.EnsureDirectories();
    FileStream? gate;
    try {
      var lockRoot = server ? SharedMailPaths.Root() : AppPaths.DataDirectory();
      Directory.CreateDirectory(lockRoot);
      var lockPath = Path.Combine(lockRoot, "sync.lock");
      gate = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
      if (!OperatingSystem.IsWindows())
        File.SetUnixFileMode(lockPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
    catch (IOException) {
      AppLog.Write("Mail sync is already running for this account.");
      return;
    }

    using (gate) {
      _semantic.Start();
      while (!cancellationToken.IsCancellationRequested) {
        try {
          await PassAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
          break;
        }
        catch (Exception ex) {
          AppLog.Write(ex);
        }

        try {
          await Task.Delay(Interval, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
          break;
        }
      }
    }
  }

  private async Task PassAsync(CancellationToken cancellationToken) {
    Configuration configuration;
    if (MailSyncIdentity.IsServiceAccount()) {
      configuration = new Configuration { Mailboxes = [.. SharedMailboxStore.List()] };
    }
    else {
      configuration = _files.Reload();
      if (SharedMailboxStore.MergeMissing(configuration))
        _files.Save(configuration);
    }
    _archive.OpenAll(configuration.Mailboxes);
    var sessions = new Dictionary<string, IMailSession>(StringComparer.OrdinalIgnoreCase);
    try {
      foreach (var box in configuration.Mailboxes) {
        cancellationToken.ThrowIfCancellationRequested();
        if (box.IsLocalStore || !CanOpen(box))
          continue;
        var session = await ConnectAsync(box, cancellationToken).ConfigureAwait(false);
        if (session is null)
          continue;
        sessions[box.Id] = session;
        await SyncMailboxAsync(configuration, box, session, cancellationToken).ConfigureAwait(false);
      }

      await RetentionAsync(configuration, sessions, cancellationToken).ConfigureAwait(false);
    }
    finally {
      foreach (var session in sessions.Values)
        await session.DisposeAsync().ConfigureAwait(false);
    }
  }

  private bool CanOpen(MailboxAccount box) {
    if (_auth.HasTokens(box.Id) || MailAuthKind.IsOAuth(box.AuthKind))
      return true;
    var stored = _secrets.Get(FileSecretStore.MailboxKey(box.Id));
    return stored.IsSuccess && !string.IsNullOrWhiteSpace(stored.Value);
  }

  private string PasswordFor(MailboxAccount box) {
    var stored = _secrets.Get(FileSecretStore.MailboxKey(box.Id));
    return stored.IsSuccess ? stored.Value ?? "" : "";
  }

  private async Task<IMailSession?> ConnectAsync(MailboxAccount box, CancellationToken cancellationToken) {
    var session = _sessions.Create(box);
    var connected = await session.ConnectAsync(box, PasswordFor(box), cancellationToken).ConfigureAwait(false);
    if (connected.IsSuccess)
      return session;
    AppLog.Write("Sync skipped " + box.Label + ": " + string.Join(" ", connected.Messages));
    await session.DisposeAsync().ConfigureAwait(false);
    return null;
  }

  private async Task SyncMailboxAsync(
    Configuration configuration,
    MailboxAccount box,
    IMailSession session,
    CancellationToken cancellationToken) {
    var listed = await session.ListFoldersAsync(cancellationToken).ConfigureAwait(false);
    if (!listed.IsSuccess || listed.Value is null)
      return;
    var folders = listed.Value;
    var names = folders.Select(f => (f.Name, f.FullName)).ToList();
    var folderIndex = 0;
    var ruleFolders = _archive.ListFolders(box.Id).ToList();
    var ruleCursor = 0;
    var ruleOffset = 0;
    const int rulesSlice = 48;
    const int bodyTurn = 4;
    while (true) {
      cancellationToken.ThrowIfCancellationRequested();
      var listedPage = false;
      if (folderIndex < folders.Count) {
        var done = await SyncFolderAsync(
          configuration, box, session, folders[folderIndex].FullName, names, cancellationToken)
          .ConfigureAwait(false);
        listedPage = true;
        if (done)
          folderIndex++;
      }

      var ruled = false;
      if (ruleCursor < ruleFolders.Count) {
        var folder = ruleFolders[ruleCursor];
        var headers = _archive.ListFolder(box.Id, folder).Select(MailArchiveMap.ToHeader).ToList();
        if (ruleOffset > headers.Count)
          ruleOffset = 0;
        var slice = headers.Skip(ruleOffset).Take(rulesSlice).ToList();
        if (slice.Count > 0)
          await ApplyRulesAsync(configuration, box, session, folder, slice, names, cancellationToken)
            .ConfigureAwait(false);
        ruled = true;
        if (ruleOffset + slice.Count >= headers.Count) {
          ruleCursor++;
          ruleOffset = 0;
        }
        else
          ruleOffset += slice.Count;
      }

      if (folderIndex >= folders.Count && !box.InitialSyncCompleted) {
        box.InitialSyncCompleted = true;
        _files.Save(configuration);
      }

      _archive.SetKeywordIndexEnabled(box.Id, box.InitialSyncCompleted);
      var fetched = await IndexBodiesAsync(configuration, box, session, bodyTurn, cancellationToken)
        .ConfigureAwait(false);
      if (fetched > 0)
        _semantic.Wake();
      if (!listedPage && !ruled && fetched == 0)
        break;
    }
  }

  private async Task<bool> SyncFolderAsync(
    Configuration configuration,
    MailboxAccount box,
    IMailSession session,
    string folder,
    IReadOnlyList<(string Name, string FullName)> names,
    CancellationToken cancellationToken) {
    var known = _archive.Uids(box.Id, folder);
    var page = await session.ListMessagesAsync(folder, known, cancellationToken, ListChunk).ConfigureAwait(false);
    if (!page.IsSuccess)
      return true;
    var sync = page.Value ?? new MailFolderSync();
    _archive.SetKeywordIndexEnabled(box.Id, box.InitialSyncCompleted);
    if (sync.Flags.Count > 0)
      _archive.UpdateFlags(box.Id, folder, sync.Flags.Select(f => (f.Id, f.IsSeen, f.IsFlagged)));
    _archive.UpsertHeaders(box.Id, sync.Headers.Select(MailArchiveMap.FromHeader));
    if (sync.Present is not null) {
      var keep = sync.Present.ToHashSet();
      var gone = _archive.Uids(box.Id, folder).Where(uid => !keep.Contains(uid)).ToList();
      if (gone.Count > 0)
        _archive.RemoveUids(box.Id, folder, gone);
    }

    var fresh = sync.Headers.Where(h => !known.Contains(h.Id)).Take(48).ToList();
    await ApplyRulesAsync(configuration, box, session, folder, fresh, names, cancellationToken)
      .ConfigureAwait(false);
    if (sync.Incomplete && fresh.Count == 0)
      return true;
    return !sync.Incomplete;
  }

  private async Task<int> IndexBodiesAsync(
    Configuration configuration,
    MailboxAccount box,
    IMailSession session,
    int limit,
    CancellationToken cancellationToken) {
    var pending = _archive.MissingBodies([box.Id], 512)
      .Where(item => item.Size <= 0 || item.Size <= MailFetch.MaxBackfillBytes)
      .Take(limit)
      .ToList();
    var fetched = 0;
    foreach (var item in pending) {
      cancellationToken.ThrowIfCancellationRequested();
      try {
        var result = await session.GetMessageAsync(item.Folder, item.Uid, cancellationToken, interactive: false)
          .ConfigureAwait(false);
        if (result.IsSuccess && result.Value is not null) {
          StoreBody(configuration, box, result.Value);
          fetched++;
          continue;
        }

        if (MailFetch.IsGone(result.Messages)) {
          _archive.RemoveUids(item.MailboxId, item.Folder, [item.Uid]);
          fetched++;
        }
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
        throw;
      }
      catch (Exception ex) {
        AppLog.Write(ex);
      }
    }

    return fetched;
  }

  private void StoreBody(Configuration configuration, MailboxAccount box, MailMessageBody body) {
    if (body.RawEml.Length == 0)
      return;
    var header = body.Header;
    var path = MailArchiveLayout.EmlPath(box, configuration.Mailboxes, header.Folder, header.Id);
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrWhiteSpace(dir))
      Directory.CreateDirectory(dir);
    File.WriteAllBytes(path, body.RawEml);
    _archive.UpsertBody(
      box.Id,
      MailArchiveMap.FromHeader(header),
      path,
      MailArchiveMap.BodyText(body, configuration.UnwrapEnvelope),
      MailArchiveMap.AttachmentIndex(body, configuration.UnwrapEnvelope));
  }

  private async Task ApplyRulesAsync(
    Configuration configuration,
    MailboxAccount box,
    IMailSession session,
    string folder,
    IReadOnlyList<MailMessageHeader> headers,
    IReadOnlyList<(string Name, string FullName)> names,
    CancellationToken cancellationToken) {
    var rules = MailRuleEngine.Ready(configuration.Rules)
      .Where(rule => MailRuleEngine.TargetsMailbox(rule, box.Id))
      .ToList();
    if (rules.Count == 0 || headers.Count == 0 || !session.SupportsFolders)
      return;
    var moves = new Dictionary<string, List<uint>>(StringComparer.OrdinalIgnoreCase);
    var read = new List<uint>();
    var flagged = new List<uint>();
    foreach (var header in headers) {
      foreach (var rule in rules) {
        if (!MailRuleEngine.CanApply(rule, box.Id, names))
          continue;
        if (!MailRuleEngine.Matches(rule, header.From, "", header.Subject, "", header.HasAttachments))
          continue;
        if (rule.Action == MailRuleAction.Delete) {
          var trash = MailRetention.ResolveTrash(names);
          if (!string.IsNullOrWhiteSpace(trash))
            AddUid(moves, trash, header.Id);
        }
        else if (rule.Action == MailRuleAction.Move) {
          var dest = MailRuleEngine.ExactFolder(rule.Folder, names);
          var destBox = MailRuleEngine.FolderMailbox(rule);
          if (!string.IsNullOrWhiteSpace(dest)
              && destBox.Equals(box.Id, StringComparison.OrdinalIgnoreCase)
              && !dest.Equals(folder, StringComparison.OrdinalIgnoreCase))
            AddUid(moves, dest, header.Id);
        }

        if (rule.Action == MailRuleAction.MarkRead)
          read.Add(header.Id);
        if (rule.Action == MailRuleAction.Flag)
          flagged.Add(header.Id);
        if (rule.Action == MailRuleAction.Label && !string.IsNullOrWhiteSpace(rule.Label))
          _archive.AddLabel(box.Id, folder, header.Id, rule.Label);
        if (rule.Stop)
          break;
      }
    }

    var movedIds = moves.SelectMany(pair => pair.Value).ToHashSet();
    read.RemoveAll(movedIds.Contains);
    flagged.RemoveAll(movedIds.Contains);
    foreach (var pair in moves) {
      var moved = await session.MoveMessagesAsync(folder, pair.Value, pair.Key, cancellationToken)
        .ConfigureAwait(false);
      if (moved.IsSuccess)
        _archive.RemoveUids(box.Id, folder, pair.Value);
    }

    if (read.Count > 0) {
      await session.SetMessageFlagsAsync(folder, read, new MailFlagUpdate { Seen = true }, cancellationToken)
        .ConfigureAwait(false);
      _archive.UpdateFlags(
        box.Id,
        folder,
        headers.Where(header => read.Contains(header.Id))
          .Select(header => (header.Id, true, header.IsFlagged || flagged.Contains(header.Id))));
    }

    if (flagged.Count > 0)
      await session.SetMessageFlagsAsync(folder, flagged, new MailFlagUpdate { Flagged = true }, cancellationToken)
        .ConfigureAwait(false);
  }

  private async Task RetentionAsync(
    Configuration configuration,
    Dictionary<string, IMailSession> sessions,
    CancellationToken cancellationToken) {
    configuration.EnsureDefaults();
    foreach (var job in MailRetention.Jobs(configuration.Retention)) {
      cancellationToken.ThrowIfCancellationRequested();
      var box = configuration.FindMailbox(job.MailboxId);
      if (box is null || box.IsLocalStore)
        continue;
      if (!sessions.TryGetValue(box.Id, out var session) || !session.IsConnected)
        continue;
      var known = _archive.ListFolders(box.Id);
      var folder = MailRetention.BindFolder(job.Folder, known);
      if (string.IsNullOrWhiteSpace(folder))
        continue;
      var cutoff = DateTimeOffset.UtcNow.AddDays(-job.Days);
      var ids = _archive.UidsOlderThan(box.Id, folder, cutoff, useReceivedDate: MailRetention.IsTrash(folder));
      if (ids.Count == 0)
        continue;
      if (MailRetention.IsTrash(folder) || !session.SupportsFolders) {
        if (!MailRetention.IsTrash(folder))
          continue;
        var purged = await session
          .SetMessageFlagsAsync(folder, ids, new MailFlagUpdate { Deleted = true }, cancellationToken)
          .ConfigureAwait(false);
        if (purged.IsSuccess)
          _archive.RemoveUids(box.Id, folder, ids);
        continue;
      }

      var names = known.Select(name => (Name: name, FullName: name)).ToList();
      var trash = MailRetention.ResolveTrash(names);
      if (string.IsNullOrWhiteSpace(trash) || folder.Equals(trash, StringComparison.OrdinalIgnoreCase)) {
        var purged = await session
          .SetMessageFlagsAsync(folder, ids, new MailFlagUpdate { Deleted = true }, cancellationToken)
          .ConfigureAwait(false);
        if (purged.IsSuccess)
          _archive.RemoveUids(box.Id, folder, ids);
        continue;
      }

      var moved = await session.MoveMessagesAsync(folder, ids, trash, cancellationToken).ConfigureAwait(false);
      if (moved.IsSuccess)
        _archive.RemoveUids(box.Id, folder, ids);
    }
  }

  private static void AddUid(Dictionary<string, List<uint>> map, string folder, uint id) {
    if (!map.TryGetValue(folder, out var ids)) {
      ids = [];
      map[folder] = ids;
    }

    if (!ids.Contains(id))
      ids.Add(id);
  }
}
