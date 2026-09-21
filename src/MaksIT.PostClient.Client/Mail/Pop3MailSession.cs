using MailKit.Net.Pop3;
using MimeKit;
using MimeKit.Utils;
using MaksIT.Results;


namespace MaksIT.PostClient.Client.Mail;


public sealed class Pop3MailSession : IMailSession {
  public const string Inbox = "INBOX";
  private readonly Lock _gate = new();
  private readonly MailSessionGate _io = new();
  private readonly IMailAuthService _auth;
  private Pop3Client? _pop;
  private MailboxAccount? _account;
  private MailAuthMaterial? _material;
  private int _listOffset;
  private readonly HashSet<uint> _listedIds = [];

  public Pop3MailSession(IMailAuthService auth) {
    _auth = auth;
  }

  public bool IsConnected {
    get {
      lock (_gate)
        return _pop is { IsConnected: true, IsAuthenticated: true };
    }
  }

  public bool SupportsFolders =>
    false;

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
      var client = new Pop3Client();
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
        _pop = client;
        _account = account;
        _material = resolved.Value;
      }

      return Result.Ok();
    }, cancellationToken).ConfigureAwait(false);
  }

  public Task<Result<IReadOnlyList<MailFolderInfo>>> ListFoldersAsync(
    CancellationToken cancellationToken = default) {
    _ = cancellationToken;
    if (RequirePop() is null)
      return Task.FromResult(Result<IReadOnlyList<MailFolderInfo>>.UnprocessableEntity(null, "Not connected."));
    IReadOnlyList<MailFolderInfo> folders = [
      new MailFolderInfo { FullName = Inbox, Name = "Inbox", Unread = 0 }
    ];
    return Task.FromResult(Result<IReadOnlyList<MailFolderInfo>>.Ok(folders));
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
    var client = await EnsurePopAsync(cancellationToken).ConfigureAwait(false);
    if (client is null)
      return Result<MailFolderSync>.UnprocessableEntity(null, "Not connected.");
    _ = folder;
    var known = knownIds ?? (IReadOnlySet<uint>)new HashSet<uint>();
    var chunk = itemBudget > 0;
    if (!chunk) {
      _listOffset = 0;
      _listedIds.Clear();
    }

    try {
      var count = client.Count;
      if (count == 0)
        return Result<MailFolderSync>.Ok(new MailFolderSync { Present = [] });
      var messageSizes = await MessageSizesAsync(client, cancellationToken).ConfigureAwait(false);

      if (!chunk) {
        var indexes = Enumerable.Range(0, count).ToList();
        var headers = await client.GetMessageHeadersAsync(indexes, cancellationToken).ConfigureAwait(false);
        var rows = indexes
          .Select((index, i) => ToHeader(index, i < headers.Count ? headers[i] : new HeaderList(), SizeAt(messageSizes, index)))
          .OrderByDescending(h => h.Date)
          .ThenByDescending(h => h.Id)
          .ToList();
        return Result<MailFolderSync>.Ok(new MailFolderSync {
          Headers = rows,
          Present = rows.Select(h => h.Id).ToList()
        });
      }

      var range = MailListBudget.NewestRange(count, _listOffset, itemBudget);
      if (range.Take <= 0) {
        var present = _listedIds.ToList();
        _listOffset = 0;
        _listedIds.Clear();
        return Result<MailFolderSync>.Ok(new MailFolderSync { Present = present });
      }

      var slice = Enumerable.Range(range.Start, range.Take).ToList();
      var chunkHeaders = await client.GetMessageHeadersAsync(slice, cancellationToken).ConfigureAwait(false);
      var added = new List<MailMessageHeader>();
      for (var i = 0; i < slice.Count; i++) {
        var id = (uint)slice[i];
        _listedIds.Add(id);
        if (known.Contains(id))
          continue;
        added.Add(ToHeader(slice[i], i < chunkHeaders.Count ? chunkHeaders[i] : new HeaderList(), SizeAt(messageSizes, slice[i])));
      }

      _listOffset = range.NextOffset;
      if (!range.Incomplete) {
        var present = _listedIds.ToList();
        _listOffset = 0;
        _listedIds.Clear();
        return Result<MailFolderSync>.Ok(new MailFolderSync {
          Headers = added,
          Present = present
        });
      }

      return Result<MailFolderSync>.Ok(new MailFolderSync {
        Headers = added,
        Incomplete = true
      });
    }
    catch (Exception ex) {
      return Result<MailFolderSync>.UnprocessableEntity(null, ex.Message);
    }
  }

  public Task<Result<MailMessageBody>> GetMessageAsync(
    string folder,
    uint id,
    CancellationToken cancellationToken = default,
    bool interactive = true) =>
    _io.RunAsync(() => GetMessageCoreAsync(folder, id, cancellationToken, interactive), cancellationToken, interactive);

  private async Task<Result<MailMessageBody>> GetMessageCoreAsync(
    string folder,
    uint id,
    CancellationToken cancellationToken,
    bool interactive) {
    var client = await EnsurePopAsync(cancellationToken).ConfigureAwait(false);
    if (client is null)
      return Result<MailMessageBody>.UnprocessableEntity(null, "Not connected.");

    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    using var abort = linked.Token.Register(DropSocket);
    if (!interactive)
      linked.CancelAfter(MailFetch.BackfillTimeout);
    MimeMessage mime;
    try {
      var index = (int)id;
      mime = await client.GetMessageAsync(index, linked.Token).ConfigureAwait(false);
    }
    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
      return Result<MailMessageBody>.UnprocessableEntity(null, "Timed out.");
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      return Result<MailMessageBody>.UnprocessableEntity(null, ex.Message);
    }

    abort.Unregister();
    return Result<MailMessageBody>.Ok(await MimeBody.FromMimeAsync(Inbox, id, mime, cancellationToken).ConfigureAwait(false));
  }

  public Task<Result<MailboxQuota?>> GetQuotaAsync(CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => {
      var client = RequirePop();
      MailboxAccount? account;
      lock (_gate)
        account = _account;
      if (client is null || account is null)
        return Task.FromResult(Result<MailboxQuota?>.Ok(null));
      try {
        long bytes = 0;
        var count = client.Count;
        for (var i = 0; i < count; i++)
          bytes += client.GetMessageSize(i);
        var kb = (uint)Math.Min(uint.MaxValue, bytes / 1024);
        return Task.FromResult(Result<MailboxQuota?>.Ok(new MailboxQuota {
          Label = account.Label,
          Host = account.ImapHost,
          UsedKb = kb
        }));
      }
      catch (Exception ex) {
        return Task.FromResult(Result<MailboxQuota?>.UnprocessableEntity(null, ex.Message));
      }
    }, cancellationToken);

  public Task<Result<uint?>> AppendAsync(
    string folder,
    byte[] eml,
    CancellationToken cancellationToken = default) {
    _ = folder;
    _ = eml;
    _ = cancellationToken;
    return Task.FromResult(Result<uint?>.UnprocessableEntity(null, "POP3 cannot append messages."));
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
    CancellationToken cancellationToken = default) {
    _ = fromFolder;
    _ = ids;
    _ = toFolder;
    _ = cancellationToken;
    return Task.FromResult(Result.UnprocessableEntity("POP3 cannot move messages between folders."));
  }

  public Task<Result> SetMessageFlagsAsync(
    string folder,
    IReadOnlyList<uint> ids,
    MailFlagUpdate update,
    CancellationToken cancellationToken = default) {
    _ = folder;
    _ = ids;
    _ = update;
    _ = cancellationToken;
    return Task.FromResult(Result.UnprocessableEntity("POP3 cannot store flags."));
  }

  public Task<Result> CreateFolderAsync(
    string name,
    string? parentFolder,
    CancellationToken cancellationToken = default) {
    _ = name;
    _ = parentFolder;
    _ = cancellationToken;
    return Task.FromResult(Result.UnprocessableEntity("POP3 cannot create folders."));
  }

  public Task<Result> RenameFolderAsync(
    string folder,
    string? parentFolder,
    string name,
    CancellationToken cancellationToken = default) {
    _ = folder;
    _ = parentFolder;
    _ = name;
    _ = cancellationToken;
    return Task.FromResult(Result.UnprocessableEntity("POP3 cannot move folders."));
  }

  public Task<Result> DeleteFolderAsync(string folder, CancellationToken cancellationToken = default) {
    _ = folder;
    _ = cancellationToken;
    return Task.FromResult(Result.UnprocessableEntity("POP3 cannot delete folders."));
  }

  public Task<Result> EmptyFolderAsync(
    string folder,
    string? trashFolder,
    CancellationToken cancellationToken = default) {
    _ = folder;
    _ = trashFolder;
    _ = cancellationToken;
    return Task.FromResult(Result.UnprocessableEntity("POP3 cannot empty folders."));
  }

  public Task<Result> SetFolderSeenAsync(
    string folder,
    bool seen,
    CancellationToken cancellationToken = default) {
    _ = folder;
    _ = seen;
    _ = cancellationToken;
    return Task.FromResult(Result.UnprocessableEntity("POP3 cannot store flags."));
  }

  public async ValueTask DisposeAsync() {
    await _io.RunAsync(DisposeClientAsync, CancellationToken.None).ConfigureAwait(false);
    _io.Dispose();
  }

  private Pop3Client? RequirePop() {
    lock (_gate)
      return _pop is { IsConnected: true, IsAuthenticated: true } ? _pop : null;
  }

  private async Task<Pop3Client?> EnsurePopAsync(CancellationToken cancellationToken) {
    var live = RequirePop();
    if (live is not null)
      return live;
    MailboxAccount? account;
    MailAuthMaterial? material;
    lock (_gate) {
      account = _account;
      material = _material;
    }

    if (account is null || material is null || string.IsNullOrWhiteSpace(account.ImapHost))
      return null;
    var client = new Pop3Client();
    try {
      await client.ConnectAsync(
        account.ImapHost.Trim(),
        account.ImapPort,
        MailSocket.Incoming(account),
        cancellationToken).ConfigureAwait(false);
      await MailKitAuth.AuthenticateAsync(client, account, material, cancellationToken).ConfigureAwait(false);
    }
    catch {
      client.Dispose();
      return null;
    }

    lock (_gate) {
      if (_pop is { IsConnected: true, IsAuthenticated: true } current) {
        client.Dispose();
        return current;
      }

      _pop = client;
    }

    return client;
  }

  private void DropSocket() {
    Pop3Client? client;
    lock (_gate) {
      client = _pop;
      _pop = null;
    }

    if (client is null)
      return;
    try {
      client.Dispose();
    }
    catch {
    }
  }

  private async Task DisposeClientAsync() {
    Pop3Client? client;
    lock (_gate) {
      client = _pop;
      _pop = null;
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

  private static MailMessageHeader ToHeader(int index, HeaderList headers, long size) {
    var date = DateTimeOffset.MinValue;
    var rawDate = headers[HeaderId.Date];
    if (!string.IsNullOrWhiteSpace(rawDate))
      DateUtils.TryParse(rawDate, out date);
    var contentType = headers[HeaderId.ContentType] ?? "";
    var disposition = headers[HeaderId.ContentDisposition] ?? "";
    var info = EnvelopeClassifier.Classify(
      headers.Select(h => new KeyValuePair<string, string>(h.Field, h.Value)),
      []);
    var hasAttachments = contentType.Contains("multipart/mixed", StringComparison.OrdinalIgnoreCase)
      || contentType.Contains("name=", StringComparison.OrdinalIgnoreCase)
      || disposition.Contains("attachment", StringComparison.OrdinalIgnoreCase)
      || info.HasInnerMessage;
    return MimeBody.ToListHeader(
      Inbox,
      (uint)index,
      headers[HeaderId.Subject] ?? "",
      headers[HeaderId.From] ?? "",
      date,
      true,
      false,
      hasAttachments,
      info,
      MailPriority.FromHeaders(
        headers[HeaderId.XPriority],
        headers[HeaderId.Importance],
        headers[HeaderId.Priority]),
      headers[HeaderId.MessageId],
      MailId.Parent(
        FirstNonEmpty(headers["X-Riferimento-Message-ID"], headers[HeaderId.InReplyTo]),
        headers[HeaderId.References]),
      size);
  }

  private static async Task<IList<int>?> MessageSizesAsync(Pop3Client client, CancellationToken cancellationToken) {
    try {
      return await client.GetMessageSizesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch {
      return null;
    }
  }

  private static long SizeAt(IList<int>? sizes, int index) =>
    sizes is not null && index >= 0 && index < sizes.Count ? sizes[index] : 0;

  private static string FirstNonEmpty(params string?[] values) {
    foreach (var value in values) {
      if (!string.IsNullOrWhiteSpace(value))
        return value.Trim();
    }

    return "";
  }
}
