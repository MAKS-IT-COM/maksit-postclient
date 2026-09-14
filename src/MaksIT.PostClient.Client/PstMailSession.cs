using System.Globalization;
using MimeKit;
using OfficeIMO.Email;
using OfficeIMO.Email.Store;
using MaksIT.Results;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public sealed class PstMailSession : IMailSession {
  private const int FolderScan = 1_000_000;
  private readonly Lock _gate = new();
  private readonly MailSessionGate _io = new();
  private readonly Dictionary<string, Dictionary<uint, string>> _items =
    new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, int> _listOffset = new(StringComparer.OrdinalIgnoreCase);
  private EmailStoreSession? _store;
  private string _path = "";

  public string StorePath {
    get {
      lock (_gate)
        return _path;
    }
  }

  public string? LastNotice { get; private set; }

  public event Action<string>? StorePathChanged;

  public bool IsConnected {
    get {
      lock (_gate)
        return _store is not null;
    }
  }

  public bool SupportsFolders =>
    true;

  public Task<Result> ConnectAsync(
    MailboxAccount account,
    string password,
    CancellationToken cancellationToken = default) {
    _ = password;
    ArgumentNullException.ThrowIfNull(account);
    var path = account.DataFile;
    if (string.IsNullOrWhiteSpace(path))
      return Task.FromResult(Result.BadRequest("Outlook data file path is required."));
    if (!File.Exists(path))
      return Task.FromResult(Result.BadRequest("Outlook data file not found: " + path));

    return _io.RunAsync(() => {
      cancellationToken.ThrowIfCancellationRequested();
      DisposeStore();
      LastNotice = null;
      try {
        if (OutlookPst.IsAnsi(path)) {
          var copy = OutlookPst.UnicodeCopyPath(path);
          if (!File.Exists(copy)) {
            return Task.FromResult(ConvertToUnicodePst(
              path,
              copy,
              cancellationToken,
              "ANSI Outlook data files are copied to " + Path.GetFileName(copy) + "."));
          }

          path = copy;
          NotifyPath(path, "Reading the Unicode copy " + Path.GetFileName(copy) + ".");
        }

        OpenStore(path);
        return Task.FromResult(Result.Ok());
      }
      catch (Exception ex) {
        return Task.FromResult(Result.UnprocessableEntity(WriteError(ex)));
      }
    }, cancellationToken);
  }

  public Task<Result<IReadOnlyList<MailFolderInfo>>> ListFoldersAsync(
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => {
      cancellationToken.ThrowIfCancellationRequested();
      var store = RequireStore();
      if (store is null)
        return Task.FromResult(Result<IReadOnlyList<MailFolderInfo>>.UnprocessableEntity(null, "Not connected."));
      try {
        var folders = new List<MailFolderInfo>();
        foreach (var folder in store.FolderCatalog.All) {
          if (!IncludeFolder(folder))
            continue;
          var full = PathOf(store.FolderCatalog, folder);
          if (string.IsNullOrWhiteSpace(full))
            continue;
          folders.Add(new MailFolderInfo {
            FullName = full,
            Name = folder.Name.Trim(),
            Total = folder.ItemCount ?? 0
          });
        }

        return Task.FromResult(Result<IReadOnlyList<MailFolderInfo>>.Ok(MailFolderCatalog.Normalize(folders)));
      }
      catch (Exception ex) {
        return Task.FromResult(Result<IReadOnlyList<MailFolderInfo>>.UnprocessableEntity(null, ex.Message));
      }
    }, cancellationToken);

  public Task<Result<MailFolderSync>> ListMessagesAsync(
    string folder,
    IReadOnlySet<uint>? knownIds = null,
    CancellationToken cancellationToken = default,
    int itemBudget = 0) =>
    _io.RunAsync(
      () => Task.FromResult(ListMessagesCore(folder, knownIds, cancellationToken, itemBudget)),
      cancellationToken);

  public Task<Result<MailMessageBody>> GetMessageAsync(
    string folder,
    uint id,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => GetMessageCoreAsync(folder, id, cancellationToken), cancellationToken);

  public Task<Result<MailboxQuota?>> GetQuotaAsync(CancellationToken cancellationToken = default) {
    _ = cancellationToken;
    return Task.FromResult(Result<MailboxQuota?>.Ok(null));
  }

  public Task<Result<uint?>> AppendAsync(
    string folder,
    byte[] eml,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => AppendCoreAsync(folder, eml, cancellationToken), cancellationToken);

  public Task<Result<string>> SendAsync(MailSendRequest request, CancellationToken cancellationToken = default) {
    _ = request;
    _ = cancellationToken;
    return Task.FromResult(Result<string>.UnprocessableEntity(null, "This Outlook data file has no SMTP. Send from an IMAP mailbox."));
  }

  public Task<Result> MoveMessagesAsync(
    string fromFolder,
    IReadOnlyList<uint> ids,
    string toFolder,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => MutateAsync(fromFolder, ids, toFolder, cancellationToken, (tx, source, dest, itemIds) => {
      if (string.IsNullOrEmpty(dest))
        return Task.FromResult(Result.UnprocessableEntity("Folder not found."));
      foreach (var itemId in itemIds)
        tx.MoveItem(itemId, dest, cancellationToken);
      return Task.FromResult(Result.Ok());
    }), cancellationToken);

  public Task<Result> SetMessageFlagsAsync(
    string folder,
    IReadOnlyList<uint> ids,
    MailFlagUpdate update,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => SetFlagsCoreAsync(folder, ids, update, cancellationToken), cancellationToken);

  public Task<Result> CreateFolderAsync(
    string name,
    string? parentFolder,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => CreateFolderCoreAsync(name, parentFolder, cancellationToken), cancellationToken);

  public Task<Result> RenameFolderAsync(
    string folder,
    string? parentFolder,
    string name,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => RenameFolderCoreAsync(folder, parentFolder, name, cancellationToken), cancellationToken);

  public Task<Result> DeleteFolderAsync(string folder, CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => DeleteFolderCoreAsync(folder, cancellationToken), cancellationToken);

  public Task<Result> EmptyFolderAsync(
    string folder,
    string? trashFolder,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => EmptyFolderCoreAsync(folder, trashFolder, cancellationToken), cancellationToken);

  public Task<Result> SetFolderSeenAsync(
    string folder,
    bool seen,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => SetFolderSeenCoreAsync(folder, seen, cancellationToken), cancellationToken);

  public async ValueTask DisposeAsync() {
    await _io.RunAsync(() => {
      DisposeStore();
      return Task.CompletedTask;
    }, CancellationToken.None).ConfigureAwait(false);
    _io.Dispose();
  }

  private Result<MailFolderSync> ListMessagesCore(
    string folder,
    IReadOnlySet<uint>? knownIds,
    CancellationToken cancellationToken,
    int itemBudget) {
    var store = RequireStore();
    if (store is null)
      return Result<MailFolderSync>.UnprocessableEntity(null, "Not connected.");
    var node = FindFolder(store, folder);
    if (node is null)
      return Result<MailFolderSync>.UnprocessableEntity(null, "Folder not found.");
    try {
      var chunk = itemBudget > 0;
      var offset = 0;
      var map = new Dictionary<uint, string>();
      lock (_gate) {
        if (chunk) {
          _listOffset.TryGetValue(folder, out offset);
          if (_items.TryGetValue(folder, out var previous)) {
            foreach (var pair in previous)
              map[pair.Key] = pair.Value;
          }
        }
        else
          _listOffset.Remove(folder);
      }

      var headers = new List<MailMessageHeader>();
      var present = chunk ? null : new List<uint>();
      var known = knownIds ?? (IReadOnlySet<uint>)new HashSet<uint>();
      var seen = 0;
      var taken = 0;
      var incomplete = false;
      foreach (var reference in store.EnumerateItems(node.Key, false, false, FolderScan, cancellationToken)) {
        cancellationToken.ThrowIfCancellationRequested();
        if (reference.IsAssociated || reference.IsOrphaned)
          continue;
        var summary = reference.Summary;
        if (!IsMail(summary?.MessageClass, DisplaySubject(summary)))
          continue;
        if (seen < offset) {
          seen++;
          continue;
        }

        if (chunk && taken >= itemBudget) {
          incomplete = true;
          break;
        }

        seen++;
        var itemId = ItemId(reference);
        var uid = UidFromItemId(itemId, map);
        map[uid] = itemId;
        present?.Add(uid);
        var listedFrom = AddressText(summary?.From, summary?.Sender);
        if (!known.Contains(uid) || listedFrom.Length == 0) {
          headers.Add(ToHeader(
            folder,
            uid,
            summary,
            listedFrom.Length > 0 ? listedFrom : SenderOf(store, reference, summary, cancellationToken)));
        }

        taken++;
      }

      lock (_gate) {
        _items[folder] = map;
        if (incomplete)
          _listOffset[folder] = seen;
        else
          _listOffset.Remove(folder);
      }

      return Result<MailFolderSync>.Ok(new MailFolderSync {
        Headers = headers,
        Present = incomplete ? null : present ?? [.. map.Keys],
        Incomplete = incomplete
      });
    }
    catch (Exception ex) {
      return Result<MailFolderSync>.UnprocessableEntity(null, ex.Message);
    }
  }

  private async Task<Result<MailMessageBody>> GetMessageCoreAsync(
    string folder,
    uint id,
    CancellationToken cancellationToken) {
    var store = RequireStore();
    if (store is null)
      return Result<MailMessageBody>.UnprocessableEntity(null, "Not connected.");
    if (!TryReadItem(store, folder, id, cancellationToken, out var item, out var reference) || item is null || reference is null)
      return Result<MailMessageBody>.UnprocessableEntity(null, "Message not found.");

    try {
      var eml = ToEml(item.Document);
      using var stream = new MemoryStream(eml);
      var mime = await MimeMessage.LoadAsync(stream, cancellationToken).ConfigureAwait(false);
      RestoreFrom(mime, item.Document);
      mime.Subject = PstSubject.Display(
        mime.Subject,
        item.Document.MessageMetadata?.NormalizedSubject,
        item.Document.MessageMetadata?.SubjectPrefix);
      var seen = item.Document.MessageMetadata?.IsRead ?? reference.Summary?.IsRead ?? true;
      var flagged = item.Document.MessageMetadata?.FollowUp?.Status == OutlookFollowUpStatus.Flagged;
      return Result<MailMessageBody>.Ok(
        await MimeBody.FromMimeAsync(folder, id, mime, cancellationToken, seen, flagged).ConfigureAwait(false));
    }
    catch (Exception ex) {
      return Result<MailMessageBody>.UnprocessableEntity(null, ex.Message);
    }
  }

  private async Task<Result<uint?>> AppendCoreAsync(
    string folder,
    byte[] eml,
    CancellationToken cancellationToken) {
    if (string.IsNullOrWhiteSpace(folder) || eml is null || eml.Length == 0)
      return Result<uint?>.BadRequest(null, "Folder and message are required.");
    EmailDocument document;
    MimeMessage mime;
    try {
      document = EmailDocument.Load(eml);
      using var stream = new MemoryStream(eml, writable: false);
      mime = await MimeMessage.LoadAsync(stream, cancellationToken).ConfigureAwait(false);
    }
    catch (Exception ex) {
      return Result<uint?>.UnprocessableEntity(null, ex.Message);
    }

    FillAddresses(document, mime);
    var ready = await MutateAsync(tx => {
      var folderId = FindMutationFolder(tx, folder);
      if (folderId is null)
        return Task.FromResult(Result.UnprocessableEntity("Folder not found."));
      tx.AddItem(folderId, document, false, cancellationToken);
      return Task.FromResult(Result.Ok());
    }, cancellationToken).ConfigureAwait(false);
    if (!ready.IsSuccess)
      return Result<uint?>.UnprocessableEntity(null, string.Join(" ", ready.Messages));

    var listed = ListMessagesCore(folder, null, cancellationToken, 0);
    if (!listed.IsSuccess || listed.Value is null)
      return Result<uint?>.UnprocessableEntity(null, string.Join(" ", listed.Messages));
    var wanted = PstSubject.Display(mime.Subject);
    MailMessageHeader? match = null;
    foreach (var header in listed.Value.Headers) {
      if (!header.Subject.Equals(wanted, StringComparison.OrdinalIgnoreCase)
          && !header.Subject.Equals(mime.Subject ?? "", StringComparison.OrdinalIgnoreCase))
        continue;
      if (match is null || header.Date >= match.Date)
        match = header;
    }

    var uid = match?.Id
      ?? listed.Value.Present?.LastOrDefault()
      ?? 0;
    if (uid == 0)
      return Result<uint?>.UnprocessableEntity(null, "Message was written but could not be read back.");
    return Result<uint?>.Ok(uid);
  }

  private Task<Result> SetFlagsCoreAsync(
    string folder,
    IReadOnlyList<uint> ids,
    MailFlagUpdate update,
    CancellationToken cancellationToken) {
    ArgumentNullException.ThrowIfNull(update);
    if (ids.Count == 0)
      return Task.FromResult(Result.BadRequest("Select at least one message."));
    return MutateAsync(folder, ids, null, cancellationToken, (tx, _, _, itemIds) => {
      foreach (var itemId in itemIds) {
        if (update.Deleted) {
          tx.DeleteItem(itemId, cancellationToken);
          continue;
        }

        var patch = new EmailStoreItemPatch();
        if (update.Seen is bool seen)
          patch.SetReadState(seen);
        if (update.Flagged == true)
          patch.SetFollowUp("Follow up");
        if (update.Flagged == false)
          patch.ClearFollowUp();
        if (update.Priority == MailPriority.High)
          patch.SetImportance(EmailMessageImportance.High);
        else if (update.Priority == MailPriority.Low)
          patch.SetImportance(EmailMessageImportance.Low);
        else if (update.Priority == MailPriority.Normal)
          patch.SetImportance(EmailMessageImportance.Normal);
        if (!patch.IsEmpty)
          tx.PatchItem(itemId, patch, cancellationToken);
      }

      return Task.FromResult(Result.Ok());
    });
  }

  private Task<Result> CreateFolderCoreAsync(
    string name,
    string? parentFolder,
    CancellationToken cancellationToken) {
    var leaf = name.Trim();
    if (leaf.Length == 0)
      return Task.FromResult(Result.BadRequest("Folder name is required."));
    if (leaf.IndexOfAny(['/', '\\']) >= 0)
      return Task.FromResult(Result.BadRequest("Folder name cannot contain slashes."));
    return MutateAsync(tx => {
      var parentId = string.IsNullOrWhiteSpace(parentFolder)
        ? tx.RootFolderId
        : FindMutationFolder(tx, parentFolder);
      if (parentId is null)
        return Task.FromResult(Result.UnprocessableEntity("Folder not found."));
      tx.CreateFolder(leaf, parentId, OutlookPst.MailContainer);
      return Task.FromResult(Result.Ok());
    }, cancellationToken);
  }

  private Task<Result> RenameFolderCoreAsync(
    string folder,
    string? parentFolder,
    string name,
    CancellationToken cancellationToken) {
    if (string.IsNullOrWhiteSpace(folder))
      return Task.FromResult(Result.BadRequest("Folder is required."));
    if (MailFolderRole.IsSystemKind(MailFolderRole.Kind(null, folder)))
      return Task.FromResult(Result.BadRequest("System folders cannot be moved."));
    var leaf = string.IsNullOrWhiteSpace(name) ? MailFolderPath.Leaf(folder) : name.Trim();
    if (leaf.Length == 0)
      return Task.FromResult(Result.BadRequest("Folder name is required."));
    if (leaf.IndexOfAny(['/', '\\']) >= 0)
      return Task.FromResult(Result.BadRequest("Folder name cannot contain slashes."));
    if (MailFolderPath.IsSelfOrUnder(parentFolder, folder))
      return Task.FromResult(Result.BadRequest("A folder cannot be moved into itself."));
    return MutateAsync(tx => {
      var folderId = FindMutationFolder(tx, folder);
      if (folderId is null)
        return Task.FromResult(Result.UnprocessableEntity("Folder not found."));
      var parentId = string.IsNullOrWhiteSpace(parentFolder)
        ? tx.RootFolderId
        : FindMutationFolder(tx, parentFolder);
      if (parentId is null)
        return Task.FromResult(Result.UnprocessableEntity("Folder not found."));
      var byId = tx.Folders.ToDictionary(row => row.Id, StringComparer.Ordinal);
      if (!byId.TryGetValue(folderId, out var node))
        return Task.FromResult(Result.UnprocessableEntity("Folder not found."));
      if (parentId.Equals(folderId, StringComparison.Ordinal)
          || IsMutationAncestor(byId, parentId, folderId))
        return Task.FromResult(Result.BadRequest("A folder cannot be moved into itself."));
      if (!string.Equals(node.ParentId, parentId, StringComparison.Ordinal))
        tx.MoveFolder(folderId, parentId);
      if (!node.Name.Equals(leaf, StringComparison.Ordinal))
        tx.RenameFolder(folderId, leaf);
      return Task.FromResult(Result.Ok());
    }, cancellationToken);
  }

  private static bool IsMutationAncestor(
    IReadOnlyDictionary<string, EmailStorePstMutationFolder> byId,
    string? nodeId,
    string ancestorId) {
    var current = nodeId;
    for (var i = 0; i < 64 && !string.IsNullOrEmpty(current); i++) {
      if (current.Equals(ancestorId, StringComparison.Ordinal))
        return true;
      if (!byId.TryGetValue(current, out var node))
        break;
      current = node.ParentId;
    }

    return false;
  }

  private Task<Result> DeleteFolderCoreAsync(string folder, CancellationToken cancellationToken) {
    if (string.IsNullOrWhiteSpace(folder))
      return Task.FromResult(Result.BadRequest("Folder is required."));
    if (MailFolderRole.IsSystemKind(MailFolderRole.Kind(null, folder)))
      return Task.FromResult(Result.BadRequest("System folders cannot be deleted."));
    return MutateAsync(tx => {
      var folderId = FindMutationFolder(tx, folder);
      if (folderId is null)
        return Task.FromResult(Result.UnprocessableEntity("Folder not found."));
      tx.DeleteFolder(folderId, true, cancellationToken);
      return Task.FromResult(Result.Ok());
    }, cancellationToken);
  }

  private Task<Result> EmptyFolderCoreAsync(
    string folder,
    string? trashFolder,
    CancellationToken cancellationToken) {
    if (string.IsNullOrWhiteSpace(folder))
      return Task.FromResult(Result.BadRequest("Folder is required."));
    return MutateAsync(tx => {
      var folderId = FindMutationFolder(tx, folder);
      if (folderId is null)
        return Task.FromResult(Result.UnprocessableEntity("Folder not found."));
      var toTrash = !string.IsNullOrWhiteSpace(trashFolder)
        && !folder.Equals(trashFolder, StringComparison.OrdinalIgnoreCase);
      var trashId = toTrash ? FindMutationFolder(tx, trashFolder!) : null;
      foreach (var reference in tx.EnumerateItems()) {
        if (reference.IsAssociated || !string.Equals(FolderId(reference), folderId, StringComparison.Ordinal))
          continue;
        if (trashId is not null)
          tx.MoveItem(ItemId(reference), trashId, cancellationToken);
        else
          tx.DeleteItem(ItemId(reference), cancellationToken);
      }

      return Task.FromResult(Result.Ok());
    }, cancellationToken);
  }

  private Task<Result> SetFolderSeenCoreAsync(
    string folder,
    bool seen,
    CancellationToken cancellationToken) {
    if (string.IsNullOrWhiteSpace(folder))
      return Task.FromResult(Result.BadRequest("Folder is required."));
    return MutateAsync(tx => {
      var folderId = FindMutationFolder(tx, folder);
      if (folderId is null)
        return Task.FromResult(Result.UnprocessableEntity("Folder not found."));
      var patch = new EmailStoreItemPatch();
      patch.SetReadState(seen);
      foreach (var reference in tx.EnumerateItems()) {
        if (reference.IsAssociated || !string.Equals(FolderId(reference), folderId, StringComparison.Ordinal))
          continue;
        tx.PatchItem(ItemId(reference), patch, cancellationToken);
      }

      return Task.FromResult(Result.Ok());
    }, cancellationToken);
  }

  private async Task<Result> MutateAsync(
    string fromFolder,
    IReadOnlyList<uint> ids,
    string? toFolder,
    CancellationToken cancellationToken,
    Func<EmailStorePstMutationTransaction, string, string?, IReadOnlyList<string>, Task<Result>> work) {
    if (string.IsNullOrWhiteSpace(fromFolder))
      return Result.BadRequest("Folder is required.");
    if (ids.Count == 0)
      return Result.BadRequest("Select at least one message.");
    var itemIds = new List<string>();
    return await MutateAsync(tx => {
      if (itemIds.Count == 0)
        return Task.FromResult(Result.UnprocessableEntity("Message not found."));
      var source = FindMutationFolder(tx, fromFolder);
      if (source is null)
        return Task.FromResult(Result.UnprocessableEntity("Folder not found."));
      var dest = toFolder is null ? null : FindMutationFolder(tx, toFolder);
      return work(tx, source, dest, itemIds);
    }, cancellationToken, () => {
      EnsureListed(fromFolder, cancellationToken);
      itemIds.Clear();
      foreach (var id in ids) {
        var itemId = LookupItem(fromFolder, id);
        if (itemId is not null)
          itemIds.Add(itemId);
      }
    }).ConfigureAwait(false);
  }

  private Task<Result> MutateAsync(
    Func<EmailStorePstMutationTransaction, Task<Result>> work,
    CancellationToken cancellationToken) =>
    MutateAsync(work, cancellationToken, null);

  private async Task<Result> MutateAsync(
    Func<EmailStorePstMutationTransaction, Task<Result>> work,
    CancellationToken cancellationToken,
    Action? beforeClose) {
    var converted = false;
    for (var attempt = 0; attempt < 2; attempt++) {
      var prepared = PrepareWritable(cancellationToken);
      if (!prepared.IsSuccess)
        return prepared;
      beforeClose?.Invoke();
      var path = StorePath;
      DisposeStore();
      try {
        using var tx = EmailStorePstMutationTransaction.Open(path, MutationOptions(), cancellationToken);
        var result = await work(tx).ConfigureAwait(false);
        if (!result.IsSuccess) {
          OpenStore(path);
          return result;
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        OpenStore(path);
        return Result.Ok();
      }
      catch (Exception ex) when (!converted && NeedsUnicodeCopy(ex)) {
        converted = true;
        var copy = ConvertToUnicodePst(path, cancellationToken);
        if (!copy.IsSuccess)
          return copy;
      }
      catch (Exception ex) {
        try {
          OpenStore(path);
        }
        catch {
          // The original path may no longer open after a failed exclusive lock.
        }

        return Result.UnprocessableEntity(WriteError(ex));
      }
    }

    return Result.UnprocessableEntity("Could not write this Outlook data file.");
  }

  private Result PrepareWritable(CancellationToken cancellationToken) {
    var store = RequireStore();
    if (store is null)
      return Result.UnprocessableEntity("Not connected.");
    if (store.IsPstPasswordProtected)
      return Result.UnprocessableEntity("Password-protected PST files cannot be written.");
    if (store.Format == EmailStoreFormat.Ost || OutlookPst.LooksLikeOst(_path))
      return ConvertToUnicodePst(_path, cancellationToken);
    return Result.Ok();
  }

  private Result ConvertToUnicodePst(string source, CancellationToken cancellationToken) {
    var dest = OutlookPst.SiblingUnicodePstPath(source);
    return ConvertToUnicodePst(
      source,
      dest,
      cancellationToken,
      "Writes go to " + Path.GetFileName(dest) + ". OST files cannot be updated in place.");
  }

  private Result ConvertToUnicodePst(
    string source,
    string dest,
    CancellationToken cancellationToken,
    string notice) {
    DisposeStore();
    try {
      EmailStoreConverter.ConvertToPst(
        source,
        dest,
        conversionOptions: new EmailStorePstConversionOptions(
          failOnDataLoss: false,
          continueOnItemError: true,
          includeAssociatedItems: true,
          includeOrphanedItems: true,
          verifyAfterWrite: false),
        cancellationToken: cancellationToken);
    }
    catch (Exception ex) {
      try {
        OpenStore(source);
      }
      catch {
      }

      return Result.UnprocessableEntity(WriteError(ex));
    }

    NotifyPath(dest, notice);
    try {
      OpenStore(dest);
      return Result.Ok();
    }
    catch (Exception ex) {
      return Result.UnprocessableEntity(WriteError(ex));
    }
  }

  private void NotifyPath(string path, string notice) {
    lock (_gate)
      _path = path;
    LastNotice = notice;
    StorePathChanged?.Invoke(path);
  }

  private void OpenStore(string path) {
    var store = EmailStoreSession.Open(path);
    lock (_gate) {
      _store = store;
      _path = path;
      _items.Clear();
      _listOffset.Clear();
    }
  }

  private EmailStoreSession? RequireStore() {
    lock (_gate)
      return _store;
  }

  private void DisposeStore() {
    EmailStoreSession? store;
    lock (_gate) {
      store = _store;
      _store = null;
      _items.Clear();
      _listOffset.Clear();
    }

    store?.Dispose();
  }

  private void EnsureListed(string folder, CancellationToken cancellationToken) {
    lock (_gate) {
      if (_items.ContainsKey(folder))
        return;
    }

    ListMessagesCore(folder, null, cancellationToken, 0);
  }

  private string? LookupItem(string folder, uint id) {
    lock (_gate)
      return _items.TryGetValue(folder, out var map) && map.TryGetValue(id, out var itemId) ? itemId : null;
  }

  private void RememberItem(string folder, uint id, string itemId) {
    lock (_gate) {
      if (!_items.TryGetValue(folder, out var map)) {
        map = new Dictionary<uint, string>();
        _items[folder] = map;
      }

      map[id] = itemId;
    }
  }

  private bool TryReadItem(
    EmailStoreSession store,
    string folder,
    uint id,
    CancellationToken cancellationToken,
    out EmailStoreItem? item,
    out EmailStoreItemReference? reference) {
    item = null;
    reference = null;
    var mapped = LookupItem(folder, id);
    var found = !string.IsNullOrWhiteSpace(mapped)
      ? FindReference(store, folder, mapped, cancellationToken)
      : null;
    found ??= FindReferenceByUid(store, folder, id, cancellationToken);
    if (found is null)
      return false;
    try {
      item = store.ReadItem(
        found,
        new EmailStoreItemReadOptions(EmailStoreItemReadParts.All),
        cancellationToken);
      if (item?.Document is null)
        return false;
      reference = found;
      RememberItem(folder, id, ItemId(found));
      return true;
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch {
      item = null;
      reference = null;
      return false;
    }
  }

  private EmailStoreItemReference? FindReference(
    EmailStoreSession store,
    string folder,
    string itemId,
    CancellationToken cancellationToken) {
    var node = FindFolder(store, folder);
    if (node is null)
      return null;
    foreach (var reference in store.EnumerateItems(node.Key, false, false, FolderScan, cancellationToken)) {
      cancellationToken.ThrowIfCancellationRequested();
      if (ItemId(reference) == itemId)
        return reference;
    }

    return null;
  }

  private static EmailStoreItemReference? FindReferenceByUid(
    EmailStoreSession store,
    string folder,
    uint id,
    CancellationToken cancellationToken) {
    var node = FindFolder(store, folder);
    if (node is null)
      return null;
    var map = new Dictionary<uint, string>();
    foreach (var reference in store.EnumerateItems(node.Key, false, false, FolderScan, cancellationToken)) {
      cancellationToken.ThrowIfCancellationRequested();
      if (reference.IsAssociated || reference.IsOrphaned)
        continue;
      if (!IsMail(reference.Summary?.MessageClass, DisplaySubject(reference.Summary)))
        continue;
      var itemId = ItemId(reference);
      var uid = UidFromItemId(itemId, map);
      map[uid] = itemId;
      if (uid == id)
        return reference;
    }

    return null;
  }

  private static EmailStoreFolderInfo? FindFolder(EmailStoreSession store, string wanted) {
    EmailStoreFolderInfo? leaf = null;
    var leaves = 0;
    foreach (var folder in store.FolderCatalog.All) {
      if (!IncludeFolder(folder))
        continue;
      var full = PathOf(store.FolderCatalog, folder);
      if (full.Equals(wanted, StringComparison.OrdinalIgnoreCase))
        return folder;
      if (folder.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase)
          || Path.GetFileName(full.Replace('\\', '/')).Equals(wanted, StringComparison.OrdinalIgnoreCase)) {
        leaf = folder;
        leaves++;
      }
    }

    return leaves == 1 ? leaf : null;
  }

  private static string? FindMutationFolder(EmailStorePstMutationTransaction tx, string wanted) {
    var byId = tx.Folders.ToDictionary(folder => folder.Id, StringComparer.Ordinal);
    string? leaf = null;
    var leaves = 0;
    foreach (var folder in tx.Folders) {
      if (!IncludeMutationFolder(folder))
        continue;
      var full = MutationPath(byId, folder);
      if (full.Equals(wanted, StringComparison.OrdinalIgnoreCase))
        return folder.Id;
      if (folder.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase)
          || Path.GetFileName(full.Replace('\\', '/')).Equals(wanted, StringComparison.OrdinalIgnoreCase)) {
        leaf = folder.Id;
        leaves++;
      }
    }

    return leaves == 1 ? leaf : null;
  }

  private static string MutationPath(
    IReadOnlyDictionary<string, EmailStorePstMutationFolder> byId,
    EmailStorePstMutationFolder folder) {
    var parts = new List<string>();
    var current = folder;
    for (var i = 0; i < 64 && current is not null; i++) {
      if (IncludeMutationFolder(current) && !string.IsNullOrWhiteSpace(current.Name))
        parts.Add(current.Name.Trim());
      if (string.IsNullOrEmpty(current.ParentId) || !byId.TryGetValue(current.ParentId, out current))
        break;
    }

    parts.Reverse();
    return string.Join("/", parts);
  }

  private static bool IncludeFolder(EmailStoreFolderInfo folder) =>
    !folder.IsSearchFolder
    && !IsRootLike(folder.SpecialFolderKind, folder.Name)
    && !string.IsNullOrWhiteSpace(folder.Name);

  private static bool IncludeMutationFolder(EmailStorePstMutationFolder folder) =>
    !folder.IsSearchFolder
    && !IsRootLike(folder.SpecialFolderKind, folder.Name)
    && !string.IsNullOrWhiteSpace(folder.Name);

  private static bool IsRootLike(EmailStoreSpecialFolderKind kind, string? name) {
    if (kind is EmailStoreSpecialFolderKind.Root
        or EmailStoreSpecialFolderKind.IpmSubtree
        or EmailStoreSpecialFolderKind.SearchRoot
        or EmailStoreSpecialFolderKind.CommonViews
        or EmailStoreSpecialFolderKind.PersonalViews
        or EmailStoreSpecialFolderKind.Reminders
        or EmailStoreSpecialFolderKind.ToDo)
      return true;
    return name is not null
      && (name.Equals("Root", StringComparison.OrdinalIgnoreCase)
        || name.Equals("IPM_SUBTREE", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Top of Personal Folders", StringComparison.OrdinalIgnoreCase));
  }

  private static string PathOf(EmailStoreFolderCatalog catalog, EmailStoreFolderInfo folder) {
    var parts = new List<string>();
    foreach (var part in catalog.GetPath(folder.Key)) {
      if (IsRootLike(part.SpecialFolderKind, part.Name) || string.IsNullOrWhiteSpace(part.Name))
        continue;
      parts.Add(part.Name.Trim());
    }

    return parts.Count == 0 ? folder.Name.Trim() : string.Join("/", parts);
  }

  private static bool IsMail(string? messageClass, string? subject) {
    var klass = messageClass ?? "";
    var title = subject ?? "";
    if (klass.StartsWith("IPM.Rule", StringComparison.OrdinalIgnoreCase)
        || klass.StartsWith("IPM.Configuration", StringComparison.OrdinalIgnoreCase)
        || title.StartsWith("IPM.Rule", StringComparison.OrdinalIgnoreCase)
        || title.StartsWith("IPM.Configuration", StringComparison.OrdinalIgnoreCase))
      return false;
    return true;
  }

  private static uint UidFromItemId(string itemId, Dictionary<uint, string> used) {
    var uid = ParseItemUid(itemId);
    while (used.TryGetValue(uid, out var existing) && !existing.Equals(itemId, StringComparison.Ordinal))
      uid++;
    if (uid == 0)
      uid = 1;
    return uid;
  }

  private static uint ParseItemUid(string itemId) {
    if (uint.TryParse(itemId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n != 0)
      return n;
    if (itemId.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        && uint.TryParse(itemId.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out n)
        && n != 0)
      return n;
    unchecked {
      uint hash = 2166136261;
      foreach (var b in System.Text.Encoding.UTF8.GetBytes(itemId)) {
        hash ^= b;
        hash *= 16777619;
      }

      return hash == 0 ? 1 : hash;
    }
  }

  private static void FillAddresses(EmailDocument document, MimeMessage mime) {
    var from = MailboxOf(mime.From.Mailboxes.FirstOrDefault());
    var sender = MailboxOf(mime.Sender) ?? from;
    if (from is not null)
      document.From = from;
    if (sender is not null)
      document.Sender = sender;
    WriteSenderMapi(document, from ?? sender);
    EnsureFromHeader(document, mime);
  }

  private static void WriteSenderMapi(EmailDocument document, EmailAddress? address) {
    if (address is null || string.IsNullOrWhiteSpace(address.Address))
      return;
    var smtp = address.Address.Trim();
    var name = string.IsNullOrWhiteSpace(address.DisplayName) ? smtp : address.DisplayName.Trim();
    WriteSenderTag(document.Mapi, name, smtp);
    WriteSenderTag(document.MapiWritePatch, name, smtp);
  }

  private static void WriteSenderTag(MapiPropertyBag mapi, string name, string smtp) {
    mapi.Set(MapiKnownProperties.PidTag.SenderName, name);
    mapi.Set(MapiKnownProperties.PidTag.SenderAddressType, "SMTP");
    mapi.Set(MapiKnownProperties.PidTag.SenderEmailAddress, smtp);
    mapi.Set(MapiKnownProperties.PidTag.SenderSmtpAddress, smtp);
    mapi.Set(MapiKnownProperties.PidTag.SentRepresentingName, name);
    mapi.Set(MapiKnownProperties.PidTag.SentRepresentingAddressType, "SMTP");
    mapi.Set(MapiKnownProperties.PidTag.SentRepresentingEmailAddress, smtp);
    mapi.Set(MapiKnownProperties.PidTag.SentRepresentingSmtpAddress, smtp);
  }

  private static void WriteSenderTag(MapiPropertyPatch patch, string name, string smtp) {
    patch.Set(MapiKnownProperties.PidTag.SenderName, name);
    patch.Set(MapiKnownProperties.PidTag.SenderAddressType, "SMTP");
    patch.Set(MapiKnownProperties.PidTag.SenderEmailAddress, smtp);
    patch.Set(MapiKnownProperties.PidTag.SenderSmtpAddress, smtp);
    patch.Set(MapiKnownProperties.PidTag.SentRepresentingName, name);
    patch.Set(MapiKnownProperties.PidTag.SentRepresentingAddressType, "SMTP");
    patch.Set(MapiKnownProperties.PidTag.SentRepresentingEmailAddress, smtp);
    patch.Set(MapiKnownProperties.PidTag.SentRepresentingSmtpAddress, smtp);
  }

  private static void EnsureFromHeader(EmailDocument document, MimeMessage mime) {
    var line = mime.From.ToString();
    if (string.IsNullOrWhiteSpace(line))
      return;
    foreach (var header in document.Headers) {
      if (header.Name.Equals("From", StringComparison.OrdinalIgnoreCase))
        return;
    }

    document.Headers.Add(new EmailHeader("From", line, line));
  }

  private static EmailAddress? MailboxOf(MailboxAddress? mailbox) {
    if (mailbox is null || string.IsNullOrWhiteSpace(mailbox.Address))
      return null;
    var smtp = mailbox.Address.Trim();
    var name = string.IsNullOrWhiteSpace(mailbox.Name) ? smtp : mailbox.Name.Trim();
    return new EmailAddress(smtp, name, mailbox.ToString()) {
      AddressType = "SMTP"
    };
  }

  private static MailMessageHeader ToHeader(
    string folder,
    uint id,
    EmailStoreItemSummary? summary,
    string? from = null) {
    var date = summary?.ReceivedAt ?? summary?.SentAt ?? DateTimeOffset.MinValue;
    return MimeBody.ToListHeader(
      folder,
      id,
      DisplaySubject(summary),
      string.IsNullOrWhiteSpace(from) ? AddressText(summary?.From, summary?.Sender) : from,
      date,
      summary?.IsRead ?? true,
      false,
      summary?.HasAttachments ?? false,
      new EnvelopeInfo(),
      messageId: summary?.MessageId,
      inReplyTo: summary?.InReplyToId);
  }

  private static string SenderOf(
    EmailStoreSession store,
    EmailStoreItemReference reference,
    EmailStoreItemSummary? summary,
    CancellationToken cancellationToken) {
    var text = AddressText(summary?.From, summary?.Sender);
    if (text.Length > 0)
      return text;
    try {
      var item = store.ReadItem(
        reference,
        new EmailStoreItemReadOptions(EmailStoreItemReadParts.Metadata | EmailStoreItemReadParts.Recipients),
        cancellationToken);
      text = AddressText(item.Document.From, item.Document.Sender);
      if (text.Length > 0)
        return text;
      foreach (var header in item.Document.Headers) {
        if (header.Name.Equals("From", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(header.Value))
          return header.Value;
      }

      var mapi = item.Document.Mapi;
      var smtp = mapi.GetValueOrDefault(MapiKnownProperties.PidTag.SenderSmtpAddress)
        ?? mapi.GetValueOrDefault(MapiKnownProperties.PidTag.SenderEmailAddress)
        ?? mapi.GetValueOrDefault(MapiKnownProperties.PidTag.SentRepresentingSmtpAddress)
        ?? mapi.GetValueOrDefault(MapiKnownProperties.PidTag.SentRepresentingEmailAddress);
      if (!string.IsNullOrWhiteSpace(smtp))
        return smtp;
    }
    catch {
    }

    return "";
  }

  private static void RestoreFrom(MimeMessage mime, EmailDocument document) {
    if (mime.From.Count > 0)
      return;
    var address = document.From ?? document.Sender;
    if (address is not null && !string.IsNullOrWhiteSpace(address.Address)) {
      var display = address.DisplayName ?? "";
      if (display.Equals(address.Address, StringComparison.OrdinalIgnoreCase))
        display = "";
      mime.From.Add(new MailboxAddress(display, address.Address));
      return;
    }

    foreach (var header in document.Headers) {
      if (!header.Name.Equals("From", StringComparison.OrdinalIgnoreCase)
          || string.IsNullOrWhiteSpace(header.Value))
        continue;
      try {
        mime.From.AddRange(InternetAddressList.Parse(header.Value));
      }
      catch {
      }
      return;
    }
  }

  private static string AddressText(params EmailAddress?[] addresses) {
    foreach (var address in addresses) {
      if (address is null)
        continue;
      var smtp = address.Address?.Trim() ?? "";
      var name = address.DisplayName?.Trim() ?? "";
      if (smtp.Length > 0 && name.Length > 0 && !name.Equals(smtp, StringComparison.OrdinalIgnoreCase))
        return name + " <" + smtp + ">";
      var text = smtp.Length > 0 ? smtp : name.Length > 0 ? name : address.RawValue ?? "";
      if (!string.IsNullOrWhiteSpace(text))
        return text;
    }

    return "";
  }

  private static string DisplaySubject(EmailStoreItemSummary? summary) =>
    PstSubject.Display(summary?.Subject, summary?.NormalizedSubject);

  private static byte[] ToEml(EmailDocument document) {
    document.Subject = PstSubject.Display(
      document.Subject,
      document.MessageMetadata?.NormalizedSubject,
      document.MessageMetadata?.SubjectPrefix);
    try {
      return document.ToBytes(EmailFileFormat.Eml);
    }
    catch {
      using var stream = new MemoryStream();
      new EmailDocumentWriter().Write(document, stream, EmailFileFormat.Eml);
      return stream.ToArray();
    }
  }

  private static string ItemId(EmailStoreItemReference reference) =>
    reference.Key.Value;

  private static string FolderId(EmailStoreItemReference reference) =>
    reference.FolderKey.Value;

  private static EmailStorePstMutationOptions MutationOptions() =>
    new(
      failOnDataLoss: false,
      verifyAfterWrite: false,
      maxFolderCount: 100_000,
      maxItemCount: 1_000_000);

  private static bool NeedsUnicodeCopy(Exception ex) {
    var text = ex.Message;
    return text.Contains("ANSI", StringComparison.OrdinalIgnoreCase)
      || text.Contains("OST", StringComparison.OrdinalIgnoreCase)
      || text.Contains("Unicode", StringComparison.OrdinalIgnoreCase);
  }

  private static string WriteError(Exception ex) {
    var message = ex.Message;
    if (message.Contains("lock", StringComparison.OrdinalIgnoreCase)
        || message.Contains("being used", StringComparison.OrdinalIgnoreCase)
        || ex is IOException)
      return message + " Close Outlook or any other program that has this file open.";
    return message;
  }
}
