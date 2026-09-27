using System.Collections.Concurrent;


namespace MaksIT.PostClient.Shared.Archive;


public sealed class MailArchiveCatalog : IDisposable {
  private readonly ConcurrentDictionary<string, MailArchiveStore> _stores =
    new(StringComparer.OrdinalIgnoreCase);
  private readonly ConcurrentDictionary<string, string> _paths =
    new(StringComparer.OrdinalIgnoreCase);
  private readonly Lock _gate = new();
  private readonly string? _spamPath;
  private SpamMemory? _spam;

  public event Action<string, int>? Progress;

  public MailArchiveCatalog(string? spamDatabasePath = null) {
    _spamPath = spamDatabasePath;
  }

  public MailArchiveStore Open(string mailboxId, string databasePath) {
    ArgumentException.ThrowIfNullOrWhiteSpace(mailboxId);
    ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
    return OffUi(() => {
      lock (_gate) {
        if (_stores.TryGetValue(mailboxId, out var existing)
            && _paths.TryGetValue(mailboxId, out var path)
            && path.Equals(databasePath, StringComparison.OrdinalIgnoreCase)) {
          if (_spam is not null)
            Absorb(existing);
          return existing;
        }

        if (_stores.TryRemove(mailboxId, out var previous)) {
          if (_spam is not null)
            Absorb(previous);
          previous.Dispose();
        }

        var store = new MailArchiveStore(databasePath);
        _stores[mailboxId] = store;
        _paths[mailboxId] = databasePath;
        if (_spam is not null)
          Absorb(store);
        return store;
      }
    });
  }

  public void Close(string mailboxId) {
    OffUi(() => {
      if (_stores.TryRemove(mailboxId, out var store)) {
        if (_spam is not null)
          Absorb(store);
        store.Dispose();
      }

      _paths.TryRemove(mailboxId, out _);
      return 0;
    });
  }

  public void OpenAll(IReadOnlyList<MailboxAccount> mailboxes) {
    foreach (var box in mailboxes) {
      try {
        Open(box.Id, MailArchiveLayout.DatabasePath(box, mailboxes));
      }
      catch {
      }
    }

    OffUi(() => {
      AbsorbAll();
      return 0;
    });
  }

  public MailArchiveStore Require(string mailboxId) {
    if (_stores.TryGetValue(mailboxId, out var store))
      return store;
    throw new InvalidOperationException("Archive is not open for mailbox " + mailboxId + ".");
  }

  public MailArchiveStore? TryGet(string mailboxId) {
    _stores.TryGetValue(mailboxId, out var store);
    return store;
  }

  public void UpsertHeaders(string mailboxId, IEnumerable<MailArchiveHeader> headers) =>
    OffUi(() => {
      Require(mailboxId).UpsertHeaders(mailboxId, headers);
      return 0;
    });

  public void SetKeywordIndexEnabled(string mailboxId, bool enabled) =>
    OffUi(() => {
      var store = TryGet(mailboxId);
      if (store is not null)
        store.KeywordIndexEnabled = enabled;
      return 0;
    });

  public int MessageCount(string mailboxId) =>
    OffUi(() => TryGet(mailboxId)?.MessageCount(mailboxId) ?? 0);

  public int TotalMessageCount() =>
    OffUi(() => {
      var count = 0;
      foreach (var pair in _stores)
        count += pair.Value.MessageCount(pair.Key);
      return count;
    });

  public int RebuildKeywordIndex(string mailboxId) =>
    OffUi(() => TryGet(mailboxId)?.RebuildKeywordIndex() ?? 0);

  public void UpdateFlags(
    string mailboxId,
    string folder,
    IEnumerable<(uint Uid, bool IsSeen, bool IsFlagged)> flags) =>
    OffUi(() => {
      Require(mailboxId).UpdateFlags(mailboxId, folder, flags);
      return 0;
    });

  public void UpsertBody(
    string mailboxId,
    MailArchiveHeader header,
    string emlPath,
    string bodyText,
    string attachmentText) =>
    OffUi(() => {
      Require(mailboxId).UpsertBody(mailboxId, header, emlPath, bodyText, attachmentText);
      return 0;
    });

  public IReadOnlyList<EmbeddingWorkItem> PendingEmbeddings(
    string modelId,
    int limit,
    IReadOnlyCollection<string>? mailboxIds = null) =>
    OffUi(() => {
      if (mailboxIds is { Count: 0 })
        return (IReadOnlyList<EmbeddingWorkItem>)[];
      var rows = new List<EmbeddingWorkItem>();
      foreach (var pair in _stores) {
        if (mailboxIds is { Count: > 0 }
            && !mailboxIds.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
          continue;
        var take = Math.Max(0, limit - rows.Count);
        if (take == 0)
          break;
        rows.AddRange(pair.Value.PendingEmbeddings(modelId, take));
      }

      return rows;
    });

  public int EmbeddingCount(string modelId, IReadOnlyCollection<string>? mailboxIds = null) =>
    OffUi(() => {
      var count = 0;
      foreach (var pair in _stores) {
        if (mailboxIds is { Count: > 0 }
            && !mailboxIds.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
          continue;
        if (mailboxIds is { Count: 0 })
          return 0;
        count += pair.Value.EmbeddingCount(modelId);
      }

      return count;
    });

  public int EmbeddingPendingCount(string modelId, IReadOnlyCollection<string>? mailboxIds = null) =>
    OffUi(() => {
      var count = 0;
      foreach (var pair in _stores) {
        if (mailboxIds is { Count: > 0 }
            && !mailboxIds.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
          continue;
        if (mailboxIds is { Count: 0 })
          return 0;
        count += pair.Value.EmbeddingPendingCount(modelId);
      }

      return count;
    });

  public void UpsertEmbedding(string mailboxId, long messageId, string modelId, float[] vector) =>
    OffUi(() => {
      Require(mailboxId).UpsertEmbedding(messageId, modelId, vector);
      return 0;
    });

  public void MarkSpam(string mailboxId, string folder, uint uid, string modelId) =>
    OffUi(() => {
      var store = TryGet(mailboxId);
      store?.MarkSpam(mailboxId, folder, uid, modelId);
      var fingerprint = store?.SpamFingerprintOf(mailboxId, folder, uid);
      if (string.IsNullOrWhiteSpace(fingerprint))
        return 0;
      foreach (var other in Stores())
        other.ClearSpamDismissal(fingerprint);
      Spam().ClearDismissal(fingerprint);
      AbsorbAll();
      return 0;
    });

  public void DismissMessage(string mailboxId, string folder, uint uid) =>
    OffUi(() => {
      var fingerprint = TryGet(mailboxId)?.SpamFingerprintOf(mailboxId, folder, uid);
      if (string.IsNullOrWhiteSpace(fingerprint))
        return 0;
      Spam().Dismiss(fingerprint);
      foreach (var store in Stores())
        store.DismissSpam(fingerprint);
      return 0;
    });

  public void DismissSpam(string mailboxId, string fingerprint) =>
    OffUi(() => {
      Spam().Dismiss(fingerprint);
      foreach (var store in Stores())
        store.DismissSpam(fingerprint);
      return 0;
    });

  public void ApplySpamEmbedding(string mailboxId, long messageId, string modelId, float[] vector, bool filterEnabled) =>
    OffUi(() => {
      var store = TryGet(mailboxId);
      if (store is null)
        return 0;
      var fingerprint = store.SpamFingerprintOf(messageId) ?? "";
      var shared = Spam().Vectors(modelId, fingerprint);
      store.ApplySpamEmbedding(messageId, modelId, vector, filterEnabled, shared);
      Absorb(store);
      return 0;
    });

  public SpamExampleInfo? LocateSpam(string mailboxId, string fingerprint) =>
    OffUi(() => TryGet(mailboxId)?.LocateSpam(fingerprint));

  public IReadOnlyList<(string MailboxId, SpamExampleInfo Example)> ListSpam(string modelId) =>
    OffUi(() => {
      AbsorbAll();
      var rows = new List<(string, SpamExampleInfo)>();
      foreach (var example in Spam().List(modelId))
        rows.Add(Present(example));
      return rows;
    });

  public IReadOnlyDictionary<uint, SpamFlag> SpamFlags(string mailboxId, string folder, string modelId) =>
    OffUi(() => TryGet(mailboxId)?.SpamFlags(mailboxId, folder, modelId, Spam().ExplicitMap(modelId))
      ?? new Dictionary<uint, SpamFlag>());

  public int SpamExampleCount(string modelId) =>
    OffUi(() => {
      AbsorbAll();
      return Spam().Count(modelId);
    });

  public void DropForeignEmbeddings(string modelId) =>
    OffUi(() => {
      foreach (var store in Stores())
        store.DropForeignEmbeddings(modelId);
      return 0;
    });

  public int ClearEmbeddings() =>
    OffUi(() => Sum(store => store.ClearEmbeddings()));

  public ArchiveIndexStats IndexStats(string modelId) =>
    OffUi(() => {
      var messages = 0;
      var keyword = 0;
      var meaning = 0;
      var pending = 0;
      var orphans = 0;
      foreach (var store in Stores()) {
        var row = store.IndexStats(modelId);
        messages += row.Messages;
        keyword += row.KeywordRows;
        meaning += row.MeaningRows;
        pending += row.MeaningPending;
        orphans += row.Orphans;
      }

      return new ArchiveIndexStats(messages, keyword, meaning, pending, orphans);
    });

  public int RebuildKeywordIndex() =>
    OffUi(() => Sum(store => store.RebuildKeywordIndex()));

  public ArchiveIndexStats SanitizeIndices(string modelId) {
    OffUi(() => {
      foreach (var store in Stores())
        store.SanitizeIndices(modelId);
      return 0;
    });
    return IndexStats(modelId);
  }

  public IReadOnlyList<string> ListFolders(string mailboxId) =>
    OffUi(() => TryGet(mailboxId)?.ListFolders(mailboxId) ?? []);

  public IReadOnlyList<MailArchiveHeader> ListFolder(string mailboxId, string folder) =>
    OffUi(() => Stamp(mailboxId, TryGet(mailboxId)?.ListFolder(mailboxId, folder) ?? []));

  public (int Total, int Unread) FolderCounts(string mailboxId, string folder) =>
    OffUi(() => TryGet(mailboxId)?.FolderCounts(mailboxId, folder) ?? (0, 0));

  public IReadOnlySet<uint> Uids(string mailboxId, string folder) =>
    OffUi(() => TryGet(mailboxId)?.Uids(mailboxId, folder) ?? new HashSet<uint>());

  public IReadOnlyList<uint> MissingBodies(string mailboxId, string folder) =>
    OffUi(() => TryGet(mailboxId)?.MissingBodies(mailboxId, folder) ?? []);

  public IReadOnlyList<MailArchiveUid> MissingBodies(string mailboxId) =>
    OffUi(() => TryGet(mailboxId)?.MissingBodies(mailboxId) ?? []);

  public IReadOnlyList<MailArchiveUid> MissingBodies(
    IReadOnlyCollection<string>? mailboxIds = null,
    int limit = 0) =>
    OffUi(() => {
      var rows = new List<MailArchiveUid>();
      foreach (var pair in _stores) {
        if (mailboxIds is { Count: > 0 }
            && !mailboxIds.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
          continue;
        rows.AddRange(pair.Value.MissingBodies([pair.Key], limit > 0 ? Math.Max(0, limit - rows.Count) : 0));
        if (limit > 0 && rows.Count >= limit)
          break;
      }

      return rows;
    });

  public int MissingBodyCount(IReadOnlyCollection<string>? mailboxIds = null) =>
    OffUi(() => {
      var count = 0;
      foreach (var pair in _stores) {
        if (mailboxIds is { Count: > 0 }
            && !mailboxIds.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
          continue;
        count += pair.Value.MissingBodyCount([pair.Key]);
      }

      return count;
    });

  public void SetSeen(string mailboxId, string folder, bool seen) =>
    OffUi(() => {
      TryGet(mailboxId)?.SetSeen(mailboxId, folder, seen);
      return 0;
    });

  public IReadOnlyList<string> RemoveFolder(string mailboxId, string folder) =>
    OffUi(() => TryGet(mailboxId)?.RemoveFolder(mailboxId, folder) ?? []);

  public int RewriteFolderPrefix(string mailboxId, string from, string to) =>
    OffUi(() => TryGet(mailboxId)?.RewriteFolderPrefix(mailboxId, from, to) ?? 0);

  public IReadOnlyList<string> RemoveUids(
    string mailboxId,
    string folder,
    IReadOnlyCollection<uint>? uids) =>
    OffUi(() => TryGet(mailboxId)?.RemoveUids(mailboxId, folder, uids) ?? []);

  public string? EmlPath(string mailboxId, string folder, uint uid) =>
    OffUi(() => TryGet(mailboxId)?.EmlPath(mailboxId, folder, uid));

  public IReadOnlyList<MailArchiveHeader> Search(string mailboxId, string folder, string query) =>
    Search(mailboxId, folder, query, queryVector: null);

  public IReadOnlyList<MailArchiveHeader> Search(
    string mailboxId,
    string folder,
    string query,
    float[]? queryVector) =>
    OffUi(() => Stamp(mailboxId, TryGet(mailboxId)?.Search(mailboxId, folder, query, queryVector) ?? []));

  public IReadOnlyList<MailArchiveHeader> SearchAll(string query, float[]? queryVector) =>
    OffUi(() => {
      var hits = new List<MailArchiveHeader>();
      foreach (var pair in _stores) {
        foreach (var folder in pair.Value.ListFolders(pair.Key))
          hits.AddRange(Stamp(pair.Key, pair.Value.Search(pair.Key, folder, query, queryVector)));
      }

      return hits;
    });

  public uint NextUid(string mailboxId, string folder) =>
    OffUi(() => Require(mailboxId).NextUid(mailboxId, folder));

  public IReadOnlyList<string> LabelNames(string mailboxId) =>
    OffUi(() => TryGet(mailboxId)?.LabelNames(mailboxId) ?? []);

  public void AddLabel(string mailboxId, string folder, uint uid, string name) =>
    OffUi(() => {
      TryGet(mailboxId)?.AddLabel(mailboxId, folder, uid, name);
      return 0;
    });

  public void Checkpoint() =>
    OffUi(() => {
      foreach (var store in Stores())
        store.Checkpoint();
      return 0;
    });

  public bool HasMessageId(string mailboxId, string messageId) =>
    OffUi(() => TryGet(mailboxId)?.HasMessageId(mailboxId, messageId) ?? false);

  public uint CopyIndexed(
    string sourceMailboxId,
    string sourceFolder,
    uint uid,
    string destMailboxId,
    string destFolder,
    string destEmlPath,
    uint destUid = 0) =>
    OffUi(() => {
      var source = Require(sourceMailboxId);
      var dest = Require(destMailboxId);
      var copy = source.ReadCopy(sourceMailboxId, sourceFolder, uid)
        ?? throw new InvalidOperationException("Message is missing from the source archive.");
      copy = new MailArchiveCopy {
        Header = new MailArchiveHeader {
          Uid = destUid,
          Folder = destFolder,
          Subject = copy.Header.Subject,
          From = copy.Header.From,
          Date = copy.Header.Date,
          IsSeen = copy.Header.IsSeen,
          IsFlagged = copy.Header.IsFlagged,
          HasAttachments = copy.Header.HasAttachments,
          Priority = copy.Header.Priority,
          EnvelopeKind = copy.Header.EnvelopeKind,
          EnvelopeBadge = copy.Header.EnvelopeBadge,
          EnvelopeTipo = copy.Header.EnvelopeTipo,
          MessageId = copy.Header.MessageId,
          InReplyTo = copy.Header.InReplyTo,
          EmlPath = destEmlPath,
          Labels = copy.Header.Labels
        },
        BodyText = copy.BodyText,
        AttachmentText = copy.AttachmentText,
        Labels = copy.Labels,
        ModelId = copy.ModelId,
        Vector = copy.Vector
      };
      return dest.InsertCopy(destMailboxId, destFolder, copy);
    });

  public IReadOnlyList<uint> UidsOlderThan(
    string mailboxId,
    string folder,
    DateTimeOffset cutoff,
    bool useReceivedDate = false) =>
    OffUi(() => TryGet(mailboxId)?.UidsOlderThan(mailboxId, folder, cutoff, useReceivedDate) ?? []);

  public int CopyDirectory(string source, string dest) =>
    OffUi(() => {
      CopyTree(source, dest);
      Progress?.Invoke("copied", 1);
      return 1;
    });

  public void Release() {
    OffUi(() => {
      foreach (var store in Stores())
        store.Dispose();
      _stores.Clear();
      _paths.Clear();
      _spam?.Dispose();
      _spam = null;
      return 0;
    });
  }

  public void Dispose() =>
    Release();

  private SpamMemory Spam() {
    if (_spam is not null)
      return _spam;
    var path = string.IsNullOrWhiteSpace(_spamPath) ? AppPaths.SpamDatabase() : _spamPath;
    _spam = new SpamMemory(path);
    return _spam;
  }

  private void AbsorbAll() {
    var lessons = new List<SpamLesson>();
    var dismissals = new List<string>();
    foreach (var store in Stores()) {
      lessons.AddRange(store.ExportSpam());
      dismissals.AddRange(store.ExportSpamDismissals());
    }

    Spam().Absorb(lessons, dismissals);
  }

  private void Absorb(MailArchiveStore store) =>
    Spam().Absorb(store.ExportSpam(), store.ExportSpamDismissals());

  private (string MailboxId, SpamExampleInfo Example) Present(SpamExampleInfo example) {
    foreach (var pair in _stores) {
      var hit = pair.Value.LocateSpam(example.Fingerprint);
      if (hit is not { Found: true })
        continue;
      return (pair.Key, new SpamExampleInfo {
        Fingerprint = example.Fingerprint,
        From = example.From,
        Subject = example.Subject,
        DateUtc = example.DateUtc,
        Explicit = example.Explicit,
        Gone = false,
        Found = true,
        Folder = hit.Folder,
        Uid = hit.Uid
      });
    }

    return ("", new SpamExampleInfo {
      Fingerprint = example.Fingerprint,
      From = example.From,
      Subject = example.Subject,
      DateUtc = example.DateUtc,
      Explicit = example.Explicit,
      Gone = true
    });
  }

  private IEnumerable<MailArchiveStore> Stores() =>
    _stores.Values.ToList();

  private static IReadOnlyList<MailArchiveHeader> Stamp(string mailboxId, IReadOnlyList<MailArchiveHeader> rows) {
    foreach (var row in rows)
      row.MailboxId = mailboxId;
    return rows;
  }

  private List<T> Merge<T>(Func<MailArchiveStore, IReadOnlyList<T>> take) {
    var rows = new List<T>();
    foreach (var store in Stores())
      rows.AddRange(take(store));
    return rows;
  }

  private int Sum(Func<MailArchiveStore, int> take) {
    var total = 0;
    foreach (var store in Stores())
      total += take(store);
    return total;
  }

  private static void CopyTree(string source, string dest) {
    Directory.CreateDirectory(dest);
    foreach (var file in Directory.EnumerateFiles(source))
      File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
    foreach (var child in Directory.EnumerateDirectories(source))
      CopyTree(child, Path.Combine(dest, Path.GetFileName(child)));
  }

  internal static T OffUi<T>(Func<T> work) {
    if (SynchronizationContext.Current is null)
      return work();
    return Task.Run(work).GetAwaiter().GetResult();
  }
}
