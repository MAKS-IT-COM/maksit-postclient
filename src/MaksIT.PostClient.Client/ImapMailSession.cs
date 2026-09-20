using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using MaksIT.Results;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public sealed class ImapMailSession : IMailSession {
  private readonly Lock _gate = new();
  private readonly MailSessionGate _io = new();
  private readonly IMailAuthService _auth;
  private ImapClient? _imap;
  private MailboxAccount? _account;
  private MailAuthMaterial? _material;
  private readonly Dictionary<string, int> _listOffset = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, HashSet<uint>> _listedUids = new(StringComparer.OrdinalIgnoreCase);

  private const MessageSummaryItems FlagItems =
    MessageSummaryItems.UniqueId | MessageSummaryItems.Flags;

  private const MessageSummaryItems ListItems =
    MessageSummaryItems.UniqueId
    | MessageSummaryItems.Envelope
    | MessageSummaryItems.Flags
    | MessageSummaryItems.InternalDate;

  public ImapMailSession(IMailAuthService auth) {
    _auth = auth;
  }

  public bool IsConnected {
    get {
      lock (_gate)
        return _imap is { IsConnected: true, IsAuthenticated: true };
    }
  }

  public bool SupportsFolders =>
    true;

  public async Task<Result> ConnectAsync(
    MailboxAccount account,
    string password,
    CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(account);
    if (string.IsNullOrWhiteSpace(account.ImapHost))
      return Result.BadRequest("Incoming host is required.");

    var resolved = await _auth.ResolveAsync(account, password, cancellationToken).ConfigureAwait(false);
    if (!resolved.IsSuccess || resolved.Value is null)
      return Result.UnprocessableEntity(string.Join(" ", resolved.Messages));

    return await _io.RunAsync(async () => {
      await DisposeClientAsync().ConfigureAwait(false);
      var client = new ImapClient();
      try {
        await client.ConnectAsync(
          account.ImapHost.Trim(),
          account.ImapPort,
          MailSocket.Incoming(account),
          cancellationToken).ConfigureAwait(false);
        await MailKitAuth.AuthenticateAsync(client, account, resolved.Value, cancellationToken).ConfigureAwait(false);
      }
      catch (Exception ex) {
        client.Dispose();
        return Result.UnprocessableEntity(MailAuthHint.Incoming(account, ex.Message));
      }

      lock (_gate) {
        _imap = client;
        _account = account;
        _material = resolved.Value;
      }

      return Result.Ok();
    }, cancellationToken).ConfigureAwait(false);
  }

  public Task<Result<IReadOnlyList<MailFolderInfo>>> ListFoldersAsync(
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => ListFoldersCoreAsync(cancellationToken), cancellationToken);

  private async Task<Result<IReadOnlyList<MailFolderInfo>>> ListFoldersCoreAsync(CancellationToken cancellationToken) {
    var client = RequireImap();
    if (client is null)
      return Result<IReadOnlyList<MailFolderInfo>>.UnprocessableEntity(null, "Not connected.");

    try {
      var layout = AccountLayout();
      var folders = new List<MailFolderInfo>();
      await AddFolderAsync(client.Inbox, folders, layout, cancellationToken).ConfigureAwait(false);
      foreach (var ns in client.PersonalNamespaces) {
        foreach (var folder in await ListNamespaceAsync(client, ns, cancellationToken).ConfigureAwait(false))
          await AddFolderAsync(folder, folders, layout, cancellationToken).ConfigureAwait(false);
      }

      return Result<IReadOnlyList<MailFolderInfo>>.Ok(MailFolderCatalog.Normalize(folders, layout));
    }
    catch (Exception ex) {
      return Result<IReadOnlyList<MailFolderInfo>>.UnprocessableEntity(null, ex.Message);
    }
  }

  public Task<Result<MailFolderSync>> ListMessagesAsync(
    string folder,
    IReadOnlySet<uint>? knownIds = null,
    CancellationToken cancellationToken = default,
    int itemBudget = 0) =>
    _io.RunAsync(() => ListMessagesCoreAsync(folder, knownIds, cancellationToken, itemBudget), cancellationToken);

  private async Task<Result<MailFolderSync>> ListMessagesCoreAsync(
    string folder,
    IReadOnlySet<uint>? knownIds,
    CancellationToken cancellationToken,
    int itemBudget) {
    var client = RequireImap();
    if (client is null)
      return Result<MailFolderSync>.UnprocessableEntity(null, "Not connected.");
    if (string.IsNullOrWhiteSpace(folder))
      return Result<MailFolderSync>.BadRequest(null, "Folder is required.");

    try {
      var imapFolder = await GetMailFolderAsync(client, folder, cancellationToken).ConfigureAwait(false);
      await OpenReadOnlyAsync(imapFolder, cancellationToken).ConfigureAwait(false);
      if (imapFolder.Count == 0)
        return Result<MailFolderSync>.Ok(new MailFolderSync { Present = [] });

      var extraHeaders = ImapFetchPolicy.ExtraHeaders(AccountProvider());
      var known = knownIds ?? (IReadOnlySet<uint>)new HashSet<uint>();
      var chunk = itemBudget > 0;
      if (!chunk)
        ClearListCursor(folder);
      if (known.Count > 0 && known.Count >= imapFolder.Count / 2) {
        var flagRows = await FetchSummariesAsync(
          imapFolder,
          FlagItems,
          extraHeaders: null,
          cancellationToken).ConfigureAwait(false);
        var present = IdsOf(flagRows);
        if (present.Count > 0) {
          var flags = FlagsOf(flagRows);
          var unknown = flagRows
            .Where(s => s.UniqueId.IsValid && !known.Contains(s.UniqueId.Id))
            .OrderByDescending(s => s.UniqueId.Id)
            .Select(s => s.UniqueId)
            .ToList();
          if (unknown.Count == 0)
            return Result<MailFolderSync>.Ok(new MailFolderSync { Flags = flags, Present = present });

          var take = unknown;
          var incomplete = false;
          if (chunk && unknown.Count > itemBudget) {
            take = unknown.Take(itemBudget).ToList();
            incomplete = true;
          }

          var added = await FetchByUidsAsync(imapFolder, take, extraHeaders, cancellationToken)
            .ConfigureAwait(false);
          return Result<MailFolderSync>.Ok(new MailFolderSync {
            Headers = HeadersOf(folder, added),
            Flags = flags,
            Present = incomplete ? null : present,
            Incomplete = incomplete
          });
        }
      }

      if (!chunk) {
        var summaries = await FetchSummariesAsync(
          imapFolder,
          ListItems,
          extraHeaders,
          cancellationToken).ConfigureAwait(false);
        var headers = HeadersOf(folder, summaries);
        var ids = IdsOf(summaries);
        if (ids.Count == 0 && imapFolder.Count > 0)
          return Result<MailFolderSync>.Ok(new MailFolderSync { Headers = headers, Present = null });
        return Result<MailFolderSync>.Ok(new MailFolderSync {
          Headers = headers,
          Present = ids
        });
      }

      int offset;
      HashSet<uint> listed;
      lock (_gate) {
        _listOffset.TryGetValue(folder, out offset);
        if (!_listedUids.TryGetValue(folder, out listed!)) {
          listed = [];
          _listedUids[folder] = listed;
        }
      }

      var range = MailListBudget.NewestRange(imapFolder.Count, offset, itemBudget);
      if (range.Take <= 0) {
        ClearListCursor(folder);
        return Result<MailFolderSync>.Ok(new MailFolderSync { Present = listed.ToList() });
      }

      var chunkRows = await FetchRangeAsync(
        imapFolder,
        range.Start,
        range.End,
        ListItems,
        extraHeaders,
        cancellationToken).ConfigureAwait(false);
      foreach (var id in IdsOf(chunkRows))
        listed.Add(id);
      lock (_gate) {
        if (range.Incomplete)
          _listOffset[folder] = range.NextOffset;
        else
          ClearListCursor(folder);
      }

      return Result<MailFolderSync>.Ok(new MailFolderSync {
        Headers = HeadersOf(folder, chunkRows),
        Present = range.Incomplete ? null : listed.ToList(),
        Incomplete = range.Incomplete
      });
    }
    catch (Exception ex) {
      return Result<MailFolderSync>.UnprocessableEntity(null, ex.Message);
    }
  }

  private void ClearListCursor(string folder) {
    lock (_gate) {
      _listOffset.Remove(folder);
      _listedUids.Remove(folder);
    }
  }

  public Task<Result<MailMessageBody>> GetMessageAsync(
    string folder,
    uint id,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => GetMessageCoreAsync(folder, id, cancellationToken), cancellationToken);

  private async Task<Result<MailMessageBody>> GetMessageCoreAsync(
    string folder,
    uint id,
    CancellationToken cancellationToken) {
    var client = RequireImap();
    if (client is null)
      return Result<MailMessageBody>.UnprocessableEntity(null, "Not connected.");

    try {
      var imapFolder = await GetMailFolderAsync(client, folder, cancellationToken).ConfigureAwait(false);
      await OpenReadOnlyAsync(imapFolder, cancellationToken).ConfigureAwait(false);
      var mime = await imapFolder.GetMessageAsync(new UniqueId(id), cancellationToken).ConfigureAwait(false);
      return Result<MailMessageBody>.Ok(await MimeBody.FromMimeAsync(folder, id, mime, cancellationToken).ConfigureAwait(false));
    }
    catch (Exception ex) {
      return Result<MailMessageBody>.UnprocessableEntity(null, ex.Message);
    }
  }

  public Task<Result<MailboxQuota?>> GetQuotaAsync(CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => GetQuotaCoreAsync(cancellationToken), cancellationToken);

  private async Task<Result<MailboxQuota?>> GetQuotaCoreAsync(CancellationToken cancellationToken) {
    var client = RequireImap();
    MailboxAccount? account;
    lock (_gate)
      account = _account;
    if (client is null)
      return Result<MailboxQuota?>.UnprocessableEntity(null, "Not connected.");
    if (!client.Capabilities.HasFlag(ImapCapabilities.Quota))
      return Result<MailboxQuota?>.Ok(null);
    try {
      var quota = await client.Inbox.GetQuotaAsync(cancellationToken).ConfigureAwait(false);
      if (quota is null)
        return Result<MailboxQuota?>.Ok(null);
      return Result<MailboxQuota?>.Ok(new MailboxQuota {
        Label = account?.Label ?? "",
        Host = account?.ImapHost ?? "",
        UsedKb = quota.CurrentStorageSize,
        LimitKb = quota.StorageLimit
      });
    }
    catch {
      return Result<MailboxQuota?>.Ok(null);
    }
  }

  public Task<Result<uint?>> AppendAsync(
    string folder,
    byte[] eml,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => AppendCoreAsync(folder, eml, cancellationToken), cancellationToken);

  private async Task<Result<uint?>> AppendCoreAsync(
    string folder,
    byte[] eml,
    CancellationToken cancellationToken) {
    var client = RequireImap();
    if (client is null)
      return Result<uint?>.UnprocessableEntity(null, "Not connected.");
    if (string.IsNullOrWhiteSpace(folder) || eml.Length == 0)
      return Result<uint?>.BadRequest(null, "Folder and message are required.");
    try {
      await using var stream = new MemoryStream(eml, writable: false);
      var mime = await MimeMessage.LoadAsync(stream, cancellationToken).ConfigureAwait(false);
      IMailFolder? imapFolder;
      try {
        imapFolder = await GetMailFolderAsync(client, folder, cancellationToken).ConfigureAwait(false);
      }
      catch {
        var root = await ResolveCreateParentAsync(client, null, cancellationToken).ConfigureAwait(false);
        imapFolder = await root.CreateAsync(MailFolderPath.Leaf(folder), true, cancellationToken)
          .ConfigureAwait(false);
      }

      if (imapFolder is null)
        return Result<uint?>.UnprocessableEntity(null, "Folder is not available.");
      await imapFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
      var uid = await imapFolder.AppendAsync(new AppendRequest(mime), cancellationToken).ConfigureAwait(false);
      return Result<uint?>.Ok(uid?.Id);
    }
    catch (Exception ex) {
      return Result<uint?>.UnprocessableEntity(null, ex.Message);
    }
  }

  public async Task<Result<string>> SendAsync(MailSendRequest request, CancellationToken cancellationToken = default) {
    MailboxAccount? account;
    MailAuthMaterial? material;
    lock (_gate) {
      account = _account;
      material = _material;
    }

    if (account is null)
      return Result<string>.UnprocessableEntity(null, "Not connected.");
    var resolved = await _auth.ResolveAsync(account, material?.Password, cancellationToken).ConfigureAwait(false);
    if (!resolved.IsSuccess || resolved.Value is null)
      return Result<string>.UnprocessableEntity(null, string.Join(" ", resolved.Messages));
    lock (_gate)
      _material = resolved.Value;
    return await SmtpSender.SendAsync(account, resolved.Value, request, cancellationToken).ConfigureAwait(false);
  }

  public Task<Result> MoveMessagesAsync(
    string fromFolder,
    IReadOnlyList<uint> ids,
    string toFolder,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => MoveMessagesCoreAsync(fromFolder, ids, toFolder, cancellationToken), cancellationToken);

  private async Task<Result> MoveMessagesCoreAsync(
    string fromFolder,
    IReadOnlyList<uint> ids,
    string toFolder,
    CancellationToken cancellationToken) {
    var client = RequireImap();
    if (client is null)
      return Result.UnprocessableEntity("Not connected.");
    if (string.IsNullOrWhiteSpace(fromFolder) || string.IsNullOrWhiteSpace(toFolder))
      return Result.BadRequest("Folder is required.");
    if (fromFolder.Equals(toFolder, StringComparison.OrdinalIgnoreCase))
      return Result.Ok();
    var uids = Uids(ids);
    if (uids.Count == 0)
      return Result.BadRequest("Select at least one message.");

    try {
      var source = await GetMailFolderAsync(client, fromFolder, cancellationToken).ConfigureAwait(false);
      var dest = await GetMailFolderAsync(client, toFolder, cancellationToken).ConfigureAwait(false);
      await source.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
      try {
        if (client.Capabilities.HasFlag(ImapCapabilities.Move))
          await source.MoveToAsync(uids, dest, cancellationToken).ConfigureAwait(false);
        else
          await CopyThenDeleteAsync(source, dest, uids, cancellationToken).ConfigureAwait(false);
      }
      catch (Exception) when (client.Capabilities.HasFlag(ImapCapabilities.Move)) {
        await CopyThenDeleteAsync(source, dest, uids, cancellationToken).ConfigureAwait(false);
      }

      return Result.Ok();
    }
    catch (Exception ex) {
      return Result.UnprocessableEntity(ex.Message);
    }
  }

  private static async Task CopyThenDeleteAsync(
    IMailFolder source,
    IMailFolder dest,
    IList<UniqueId> uids,
    CancellationToken cancellationToken) {
    await source.CopyToAsync(uids, dest, cancellationToken).ConfigureAwait(false);
    await source.StoreAsync(
      uids,
      new StoreFlagsRequest(StoreAction.Add, MessageFlags.Deleted) { Silent = true },
      cancellationToken).ConfigureAwait(false);
    await source.ExpungeAsync(uids, cancellationToken).ConfigureAwait(false);
  }

  public Task<Result> SetMessageFlagsAsync(
    string folder,
    IReadOnlyList<uint> ids,
    MailFlagUpdate update,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => SetMessageFlagsCoreAsync(folder, ids, update, cancellationToken), cancellationToken);

  private async Task<Result> SetMessageFlagsCoreAsync(
    string folder,
    IReadOnlyList<uint> ids,
    MailFlagUpdate update,
    CancellationToken cancellationToken) {
    ArgumentNullException.ThrowIfNull(update);
    var client = RequireImap();
    if (client is null)
      return Result.UnprocessableEntity("Not connected.");
    var uids = Uids(ids);
    if (uids.Count == 0)
      return Result.BadRequest("Select at least one message.");

    try {
      var imapFolder = await GetMailFolderAsync(client, folder, cancellationToken).ConfigureAwait(false);
      await imapFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
      if (update.Seen == true)
        await StoreFlagsAsync(imapFolder, uids, StoreAction.Add, MessageFlags.Seen, cancellationToken).ConfigureAwait(false);
      if (update.Seen == false)
        await StoreFlagsAsync(imapFolder, uids, StoreAction.Remove, MessageFlags.Seen, cancellationToken).ConfigureAwait(false);
      if (update.Flagged == true)
        await StoreFlagsAsync(imapFolder, uids, StoreAction.Add, MessageFlags.Flagged, cancellationToken).ConfigureAwait(false);
      if (update.Flagged == false)
        await StoreFlagsAsync(imapFolder, uids, StoreAction.Remove, MessageFlags.Flagged, cancellationToken).ConfigureAwait(false);
      if (update.Deleted)
        await StoreFlagsAsync(imapFolder, uids, StoreAction.Add, MessageFlags.Deleted, cancellationToken).ConfigureAwait(false);
      if (update.Priority is not null)
        await StorePriorityAsync(imapFolder, uids, update.Priority, cancellationToken).ConfigureAwait(false);
      if (update.Deleted)
        await imapFolder.ExpungeAsync(uids, cancellationToken).ConfigureAwait(false);
      return Result.Ok();
    }
    catch (Exception ex) {
      return Result.UnprocessableEntity(ex.Message);
    }
  }

  public Task<Result> CreateFolderAsync(
    string name,
    string? parentFolder,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => CreateFolderCoreAsync(name, parentFolder, cancellationToken), cancellationToken);

  private async Task<Result> CreateFolderCoreAsync(
    string name,
    string? parentFolder,
    CancellationToken cancellationToken) {
    var client = RequireImap();
    if (client is null)
      return Result.UnprocessableEntity("Not connected.");
    var leaf = name.Trim();
    if (leaf.Length == 0)
      return Result.BadRequest("Folder name is required.");
    if (leaf.IndexOfAny(['/', '\\']) >= 0)
      return Result.BadRequest("Folder name cannot contain slashes.");

    try {
      var parent = await ResolveCreateParentAsync(client, parentFolder, cancellationToken)
        .ConfigureAwait(false);
      await parent.CreateAsync(leaf, true, cancellationToken).ConfigureAwait(false);
      return Result.Ok();
    }
    catch (Exception ex) {
      return Result.UnprocessableEntity(ex.Message);
    }
  }

  public Task<Result> RenameFolderAsync(
    string folder,
    string? parentFolder,
    string name,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => RenameFolderCoreAsync(folder, parentFolder, name, cancellationToken), cancellationToken);

  private async Task<Result> RenameFolderCoreAsync(
    string folder,
    string? parentFolder,
    string name,
    CancellationToken cancellationToken) {
    var client = RequireImap();
    if (client is null)
      return Result.UnprocessableEntity("Not connected.");
    if (string.IsNullOrWhiteSpace(folder))
      return Result.BadRequest("Folder is required.");
    if (MailFolderRole.IsSystemKind(MailFolderRole.Kind(null, folder)))
      return Result.BadRequest("System folders cannot be moved.");
    var leaf = string.IsNullOrWhiteSpace(name) ? MailFolderPath.Leaf(folder) : name.Trim();
    if (leaf.Length == 0)
      return Result.BadRequest("Folder name is required.");
    if (leaf.IndexOfAny(['/', '\\']) >= 0)
      return Result.BadRequest("Folder name cannot contain slashes.");

    try {
      var imapFolder = await GetMailFolderAsync(client, folder, cancellationToken).ConfigureAwait(false);
      if (imapFolder.IsNamespace || imapFolder.Attributes.HasFlag(FolderAttributes.Inbox))
        return Result.BadRequest("System folders cannot be moved.");
      if (MailFolderPath.IsSelfOrUnder(parentFolder, folder))
        return Result.BadRequest("A folder cannot be moved into itself.");
      if (imapFolder.IsOpen)
        await imapFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
      await OpenReadOnlyAsync(client.Inbox, cancellationToken).ConfigureAwait(false);
      imapFolder = await GetMailFolderAsync(client, folder, cancellationToken).ConfigureAwait(false);
      if (imapFolder.IsOpen)
        await imapFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
      var parent = await ResolveCreateParentAsync(client, parentFolder, cancellationToken)
        .ConfigureAwait(false);
      await imapFolder.RenameAsync(parent, leaf, cancellationToken).ConfigureAwait(false);
      return Result.Ok();
    }
    catch (Exception ex) {
      return Result.UnprocessableEntity(ex.Message);
    }
  }

  public Task<Result> DeleteFolderAsync(string folder, CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => DeleteFolderCoreAsync(folder, cancellationToken), cancellationToken);

  private async Task<Result> DeleteFolderCoreAsync(string folder, CancellationToken cancellationToken) {
    var client = RequireImap();
    if (client is null)
      return Result.UnprocessableEntity("Not connected.");
    if (string.IsNullOrWhiteSpace(folder))
      return Result.BadRequest("Folder is required.");

    try {
      var imapFolder = await GetMailFolderAsync(client, folder, cancellationToken).ConfigureAwait(false);
      if (imapFolder.IsNamespace || imapFolder.Attributes.HasFlag(FolderAttributes.Inbox))
        return Result.BadRequest("System folders cannot be deleted.");

      try {
        await imapFolder.StatusAsync(StatusItems.Count, cancellationToken).ConfigureAwait(false);
      }
      catch {
      }

      if (imapFolder.Count > 0) {
        var emptied = await EmptyFolderCoreAsync(folder, null, cancellationToken).ConfigureAwait(false);
        if (!emptied.IsSuccess)
          return emptied;
        imapFolder = await GetMailFolderAsync(client, folder, cancellationToken).ConfigureAwait(false);
      }

      if (imapFolder.IsOpen)
        await imapFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
      await OpenReadOnlyAsync(client.Inbox, cancellationToken).ConfigureAwait(false);
      imapFolder = await GetMailFolderAsync(client, folder, cancellationToken).ConfigureAwait(false);
      if (imapFolder.IsOpen)
        await imapFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
      if (imapFolder.IsSubscribed) {
        try {
          await imapFolder.UnsubscribeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch {
        }
      }

      try {
        await imapFolder.DeleteAsync(cancellationToken).ConfigureAwait(false);
      }
      catch (InvalidOperationException) {
        if (imapFolder.IsOpen)
          await imapFolder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
        await OpenReadOnlyAsync(client.Inbox, cancellationToken).ConfigureAwait(false);
        await imapFolder.DeleteAsync(cancellationToken).ConfigureAwait(false);
      }

      return Result.Ok();
    }
    catch (Exception ex) {
      return Result.UnprocessableEntity(ex.Message);
    }
  }

  public Task<Result> EmptyFolderAsync(
    string folder,
    string? trashFolder,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => EmptyFolderCoreAsync(folder, trashFolder, cancellationToken), cancellationToken);

  private async Task<Result> EmptyFolderCoreAsync(
    string folder,
    string? trashFolder,
    CancellationToken cancellationToken) {
    var client = RequireImap();
    if (client is null)
      return Result.UnprocessableEntity("Not connected.");
    if (string.IsNullOrWhiteSpace(folder))
      return Result.BadRequest("Folder is required.");

    try {
      var imapFolder = await GetMailFolderAsync(client, folder, cancellationToken).ConfigureAwait(false);
      try {
        await imapFolder.StatusAsync(StatusItems.Count, cancellationToken).ConfigureAwait(false);
      }
      catch {
      }

      if (imapFolder.Count == 0)
        return Result.Ok();

      await imapFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
      var uids = await imapFolder.SearchAsync(SearchQuery.All, cancellationToken).ConfigureAwait(false);
      if (uids.Count == 0)
        return Result.Ok();

      var toTrash = !string.IsNullOrWhiteSpace(trashFolder)
        && !folder.Equals(trashFolder, StringComparison.OrdinalIgnoreCase);
      if (toTrash) {
        var dest = await GetMailFolderAsync(client, trashFolder!, cancellationToken).ConfigureAwait(false);
        if (client.Capabilities.HasFlag(ImapCapabilities.Move))
          await imapFolder.MoveToAsync(uids, dest, cancellationToken).ConfigureAwait(false);
        else {
          await imapFolder.CopyToAsync(uids, dest, cancellationToken).ConfigureAwait(false);
          await StoreFlagsAsync(imapFolder, uids.ToList(), StoreAction.Add, MessageFlags.Deleted, cancellationToken)
            .ConfigureAwait(false);
          await imapFolder.ExpungeAsync(uids, cancellationToken).ConfigureAwait(false);
        }
      }
      else {
        await StoreFlagsAsync(imapFolder, uids.ToList(), StoreAction.Add, MessageFlags.Deleted, cancellationToken)
          .ConfigureAwait(false);
        await imapFolder.ExpungeAsync(uids, cancellationToken).ConfigureAwait(false);
      }

      return Result.Ok();
    }
    catch (Exception ex) {
      return Result.UnprocessableEntity(ex.Message);
    }
  }

  public Task<Result> SetFolderSeenAsync(
    string folder,
    bool seen,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => SetFolderSeenCoreAsync(folder, seen, cancellationToken), cancellationToken);

  private async Task<Result> SetFolderSeenCoreAsync(
    string folder,
    bool seen,
    CancellationToken cancellationToken) {
    var client = RequireImap();
    if (client is null)
      return Result.UnprocessableEntity("Not connected.");
    if (string.IsNullOrWhiteSpace(folder))
      return Result.BadRequest("Folder is required.");

    try {
      var imapFolder = await GetMailFolderAsync(client, folder, cancellationToken).ConfigureAwait(false);
      await imapFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
      var uids = await imapFolder.SearchAsync(SearchQuery.All, cancellationToken).ConfigureAwait(false);
      if (uids.Count == 0)
        return Result.Ok();
      await StoreFlagsAsync(
        imapFolder,
        uids.ToList(),
        seen ? StoreAction.Add : StoreAction.Remove,
        MessageFlags.Seen,
        cancellationToken).ConfigureAwait(false);
      return Result.Ok();
    }
    catch (Exception ex) {
      return Result.UnprocessableEntity(ex.Message);
    }
  }

  public async ValueTask DisposeAsync() {
    await _io.RunAsync(DisposeClientAsync, CancellationToken.None).ConfigureAwait(false);
    _io.Dispose();
  }

  private ImapClient? RequireImap() {
    lock (_gate)
      return _imap is { IsConnected: true, IsAuthenticated: true } ? _imap : null;
  }

  private string? AccountProvider() {
    lock (_gate)
      return _account?.Provider;
  }

  private async Task DisposeClientAsync() {
    ImapClient? client;
    lock (_gate) {
      client = _imap;
      _imap = null;
    }

    if (client is null)
      return;
    try {
      if (client.IsConnected)
        await client.DisconnectAsync(true).ConfigureAwait(false);
    }
    catch {
    }

    client.Dispose();
  }

  private static async Task<IMailFolder> GetMailFolderAsync(
    ImapClient client,
    string folder,
    CancellationToken cancellationToken) {
    if (ImapFetchPolicy.IsInboxPath(folder))
      return client.Inbox;
    return await client.GetFolderAsync(folder, cancellationToken).ConfigureAwait(false);
  }

  private static async Task<IMailFolder> ResolveCreateParentAsync(
    ImapClient client,
    string? parentFolder,
    CancellationToken cancellationToken) {
    if (!string.IsNullOrWhiteSpace(parentFolder))
      return await GetMailFolderAsync(client, parentFolder, cancellationToken).ConfigureAwait(false);
    if (client.PersonalNamespaces.Count > 0)
      return client.GetFolder(client.PersonalNamespaces[0]);
    return client.Inbox.ParentFolder ?? client.Inbox;
  }

  private static async Task<IReadOnlyList<IMailFolder>> ListNamespaceAsync(
    ImapClient client,
    FolderNamespace ns,
    CancellationToken cancellationToken) {
    try {
      var all = await client.GetFoldersAsync(ns, StatusItems.Unread | StatusItems.Count, false, cancellationToken)
        .ConfigureAwait(false);
      if (all.Count > 0)
        return all.ToList();
    }
    catch {
    }

    try {
      var subscribed = await client
        .GetFoldersAsync(ns, StatusItems.Unread | StatusItems.Count, true, cancellationToken)
        .ConfigureAwait(false);
      if (subscribed.Count > 0)
        return subscribed.ToList();
    }
    catch {
    }

    var folders = new List<IMailFolder>();
    await WalkAsync(client.GetFolder(ns), folders, cancellationToken).ConfigureAwait(false);
    return folders;
  }

  private static async Task WalkAsync(
    IMailFolder root,
    List<IMailFolder> folders,
    CancellationToken cancellationToken) {
    IList<IMailFolder> children;
    try {
      children = await root.GetSubfoldersAsync(false, cancellationToken).ConfigureAwait(false);
    }
    catch {
      return;
    }

    foreach (var child in children) {
      folders.Add(child);
      await WalkAsync(child, folders, cancellationToken).ConfigureAwait(false);
    }
  }

  private static async Task AddFolderAsync(
    IMailFolder folder,
    List<MailFolderInfo> folders,
    MailFolderLayout layout,
    CancellationToken cancellationToken) {
    if (ShouldSkip(folder, layout))
      return;
    if (folders.Any(f => f.FullName.Equals(folder.FullName, StringComparison.OrdinalIgnoreCase)))
      return;
    var unread = 0;
    var total = 0;
    try {
      if (folder.Exists) {
        try {
          await folder.StatusAsync(StatusItems.Unread | StatusItems.Count, cancellationToken)
            .ConfigureAwait(false);
        }
        catch {
        }

        unread = folder.Unread;
        total = folder.Count;
      }
    }
    catch {
    }

    folders.Add(new MailFolderInfo {
      FullName = folder.FullName,
      Name = folder.Name,
      Unread = unread,
      Total = total,
      Kind = KindFrom(folder),
      Delimiter = FolderDelimiter(folder)
    });
  }

  private MailFolderLayout AccountLayout() {
    lock (_gate)
      return MailFolderLayout.For(_account);
  }

  private static bool ShouldSkip(IMailFolder folder, MailFolderLayout layout) {
    var attrs = folder.Attributes;
    if (attrs.HasFlag(FolderAttributes.NonExistent))
      return true;
    if (layout.HideVirtualFolders
        && (attrs.HasFlag(FolderAttributes.All)
          || attrs.HasFlag(FolderAttributes.Flagged)
          || attrs.HasFlag(FolderAttributes.Important)))
      return true;
    return layout.HideVirtualFolders && MailFolderRole.IsHidden(folder.Name, folder.FullName);
  }

  private static char FolderDelimiter(IMailFolder folder) {
    var sep = folder.DirectorySeparator;
    return sep is MailFolderPath.Slash or MailFolderPath.Dot ? sep : MailFolderPath.Slash;
  }

  private static string KindFrom(IMailFolder folder) {
    var attrs = folder.Attributes;
    if (attrs.HasFlag(FolderAttributes.Inbox))
      return "inbox";
    if (attrs.HasFlag(FolderAttributes.Drafts))
      return "drafts";
    if (attrs.HasFlag(FolderAttributes.Sent))
      return "sent";
    if (attrs.HasFlag(FolderAttributes.Junk))
      return "junk";
    if (attrs.HasFlag(FolderAttributes.Trash))
      return "trash";
    if (attrs.HasFlag(FolderAttributes.Archive))
      return "archives";
    if (attrs.HasFlag(FolderAttributes.All))
      return "all";
    if (attrs.HasFlag(FolderAttributes.Flagged))
      return "flagged";
    if (attrs.HasFlag(FolderAttributes.Important))
      return "important";
    return MailFolderRole.Kind(folder.Name, folder.FullName);
  }

  private static async Task OpenReadOnlyAsync(IMailFolder folder, CancellationToken cancellationToken) {
    if (folder.IsOpen && folder.Access != FolderAccess.ReadOnly)
      await folder.CloseAsync(false, cancellationToken).ConfigureAwait(false);
    if (!folder.IsOpen)
      await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);
  }

  private static async Task<List<IMessageSummary>> FetchSummariesAsync(
    IMailFolder folder,
    MessageSummaryItems items,
    string[]? extraHeaders,
    CancellationToken cancellationToken) {
    try {
      var chunk = await FetchRangeAsync(folder, 0, -1, items, extraHeaders, cancellationToken)
        .ConfigureAwait(false);
      var rows = DistinctUids(chunk);
      if (rows.Count > 0 || folder.Count == 0)
        return rows;
    }
    catch {
    }

    const int batch = 500;
    var summaries = new List<IMessageSummary>();
    for (var end = folder.Count - 1; end >= 0; end -= batch) {
      var start = Math.Max(0, end - batch + 1);
      var chunk = await FetchRangeAsync(folder, start, end, items, extraHeaders, cancellationToken)
        .ConfigureAwait(false);
      summaries.AddRange(chunk);
    }

    return DistinctUids(summaries);
  }

  private static async Task<List<IMessageSummary>> FetchByUidsAsync(
    IMailFolder folder,
    IList<UniqueId> uids,
    string[]? extraHeaders,
    CancellationToken cancellationToken) {
    const int batch = 250;
    var summaries = new List<IMessageSummary>(uids.Count);
    for (var start = 0; start < uids.Count; start += batch) {
      var slice = uids.Skip(start).Take(batch).ToList();
      summaries.AddRange(await FetchSliceAsync(folder, slice, extraHeaders, cancellationToken).ConfigureAwait(false));
    }

    return DistinctUids(summaries);
  }

  private static async Task<IList<IMessageSummary>> FetchSliceAsync(
    IMailFolder folder,
    IList<UniqueId> uids,
    string[]? extraHeaders,
    CancellationToken cancellationToken) {
    if (extraHeaders is { Length: > 0 }) {
      try {
        var rows = await folder.FetchAsync(uids, new FetchRequest(ListItems, extraHeaders), cancellationToken)
          .ConfigureAwait(false);
        if (rows.Count > 0)
          return rows;
      }
      catch {
      }
    }

    return await folder.FetchAsync(uids, ListItems, cancellationToken).ConfigureAwait(false);
  }

  private static async Task<IList<IMessageSummary>> FetchRangeAsync(
    IMailFolder folder,
    int start,
    int end,
    MessageSummaryItems items,
    string[]? extraHeaders,
    CancellationToken cancellationToken) {
    if (extraHeaders is { Length: > 0 }) {
      try {
        var rows = await folder.FetchAsync(start, end, new FetchRequest(items, extraHeaders), cancellationToken)
          .ConfigureAwait(false);
        if (rows.Count > 0)
          return rows;
      }
      catch {
      }
    }

    return await folder.FetchAsync(start, end, items, cancellationToken).ConfigureAwait(false);
  }

  private static List<IMessageSummary> DistinctUids(IEnumerable<IMessageSummary> summaries) {
    var map = new Dictionary<uint, IMessageSummary>();
    foreach (var summary in summaries) {
      if (!summary.UniqueId.IsValid)
        continue;
      if (map.TryGetValue(summary.UniqueId.Id, out var current)
          && !ImapFetchPolicy.PreferComplete(
            summary.Envelope is not null,
            current.Envelope is not null,
            summary.InternalDate.HasValue || summary.Envelope?.Date is not null,
            current.InternalDate.HasValue || current.Envelope?.Date is not null))
        continue;
      map[summary.UniqueId.Id] = summary;
    }

    return map.Values.ToList();
  }

  private static List<MailMessageHeader> HeadersOf(string folder, IEnumerable<IMessageSummary> summaries) =>
    summaries
      .Where(s => s.UniqueId.IsValid)
      .OrderByDescending(s => s.Envelope?.Date ?? s.InternalDate ?? DateTimeOffset.MinValue)
      .Select(s => ToHeader(folder, s))
      .ToList();

  private static List<uint> IdsOf(IEnumerable<IMessageSummary> summaries) =>
    summaries.Where(s => s.UniqueId.IsValid).Select(s => s.UniqueId.Id).Distinct().ToList();

  private static List<MailFlagState> FlagsOf(IEnumerable<IMessageSummary> summaries) =>
    summaries
      .Where(s => s.UniqueId.IsValid)
      .Select(s => new MailFlagState {
        Id = s.UniqueId.Id,
        IsSeen = s.Flags?.HasFlag(MessageFlags.Seen) == true,
        IsFlagged = s.Flags?.HasFlag(MessageFlags.Flagged) == true
      })
      .ToList();

  private static MailMessageHeader ToHeader(string folder, IMessageSummary summary) {
    var envelope = summary.Envelope;
    var names = new List<string>();
    CollectPartNames(summary.Body, names);
    var extra = new List<KeyValuePair<string, string>>();
    if (summary.Headers is not null) {
      foreach (var header in summary.Headers)
        extra.Add(new KeyValuePair<string, string>(header.Field, header.Value));
    }

    var info = EnvelopeClassifier.Classify(extra, names);
    var keywords = summary.Keywords;
    var xPriority = HeaderValue(summary, "X-Priority");
    var importance = HeaderValue(summary, "Importance");
    var priorityHeader = HeaderValue(summary, "Priority");
    return MimeBody.ToListHeader(
      folder,
      summary.UniqueId.Id,
      envelope?.Subject ?? "",
      envelope?.From?.ToString() ?? "",
      envelope?.Date ?? summary.InternalDate ?? DateTimeOffset.MinValue,
      summary.Flags?.HasFlag(MessageFlags.Seen) == true,
      summary.Flags?.HasFlag(MessageFlags.Flagged) == true,
      summary.Attachments.Any() || names.Any(EnvelopeClassifier.IsPostacertName),
      info,
      MailPriority.Resolve(keywords, xPriority, importance, priorityHeader),
      envelope?.MessageId,
      MailId.Parent(
        FirstNonEmpty(
          HeaderValue(summary, "X-Riferimento-Message-ID"),
          envelope?.InReplyTo,
          HeaderValue(summary, "In-Reply-To")),
        HeaderValue(summary, "References")));
  }

  private static string FirstNonEmpty(params string?[] values) {
    foreach (var value in values) {
      if (!string.IsNullOrWhiteSpace(value))
        return value.Trim();
    }

    return "";
  }

  private static string HeaderValue(IMessageSummary summary, string name) {
    if (summary.Headers is null)
      return "";
    foreach (var header in summary.Headers) {
      if (header.Field.Equals(name, StringComparison.OrdinalIgnoreCase))
        return header.Value ?? "";
    }

    return "";
  }

  private static IList<UniqueId> Uids(IReadOnlyList<uint> ids) =>
    ids.Where(id => id > 0).Select(id => new UniqueId(id)).Distinct().ToList();

  private static async Task StorePriorityAsync(
    IMailFolder folder,
    IList<UniqueId> uids,
    string priority,
    CancellationToken cancellationToken) {
    var remove = new StoreFlagsRequest(StoreAction.Remove, new[] { MailPriority.KeywordHigh, MailPriority.KeywordLow }) {
      Silent = true
    };
    await folder.StoreAsync(uids, remove, cancellationToken).ConfigureAwait(false);
    var add = MailPriority.KeywordsFor(priority);
    if (add.Count == 0)
      return;
    var request = new StoreFlagsRequest(StoreAction.Add, add) { Silent = true };
    await folder.StoreAsync(uids, request, cancellationToken).ConfigureAwait(false);
  }

  private static Task StoreFlagsAsync(
    IMailFolder folder,
    IList<UniqueId> uids,
    StoreAction action,
    MessageFlags flags,
    CancellationToken cancellationToken) =>
    folder.StoreAsync(uids, new StoreFlagsRequest(action, flags) { Silent = true }, cancellationToken);

  private static void CollectPartNames(BodyPart? part, List<string> names) {
    if (part is null)
      return;
    if (part is BodyPartBasic basic && !string.IsNullOrWhiteSpace(basic.FileName))
      names.Add(basic.FileName);
    if (part is BodyPartMultipart multi) {
      foreach (var child in multi.BodyParts)
        CollectPartNames(child, names);
    }
  }
}
