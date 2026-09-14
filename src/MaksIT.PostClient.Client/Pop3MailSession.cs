using MailKit.Net.Pop3;
using MimeKit;
using MimeKit.Utils;
using MaksIT.Results;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public sealed class Pop3MailSession : IMailSession {
  public const string Inbox = "INBOX";
  private readonly Lock _gate = new();
  private readonly MailSessionGate _io = new();
  private readonly IMailAuthService _auth;
  private Pop3Client? _pop;
  private MailboxAccount? _account;
  private MailAuthMaterial? _material;

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
    int itemBudget = 0) {
    _ = itemBudget;
    return _io.RunAsync(() => ListMessagesCoreAsync(folder, knownIds, cancellationToken), cancellationToken);
  }

  private async Task<Result<MailFolderSync>> ListMessagesCoreAsync(
    string folder,
    IReadOnlySet<uint>? knownIds,
    CancellationToken cancellationToken) {
    var client = RequirePop();
    if (client is null)
      return Result<MailFolderSync>.UnprocessableEntity(null, "Not connected.");
    _ = folder;
    _ = knownIds;

    try {
      var count = client.Count;
      if (count == 0)
        return Result<MailFolderSync>.Ok(new MailFolderSync { Present = [] });

      var indexes = Enumerable.Range(0, count).ToList();
      var headers = await client.GetMessageHeadersAsync(indexes, cancellationToken).ConfigureAwait(false);
      var rows = indexes
        .Select((index, i) => ToHeader(index, i < headers.Count ? headers[i] : new HeaderList()))
        .OrderByDescending(h => h.Date)
        .ThenByDescending(h => h.Id)
        .ToList();
      return Result<MailFolderSync>.Ok(new MailFolderSync {
        Headers = rows,
        Present = rows.Select(h => h.Id).ToList()
      });
    }
    catch (Exception ex) {
      return Result<MailFolderSync>.UnprocessableEntity(null, ex.Message);
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
    var client = RequirePop();
    if (client is null)
      return Result<MailMessageBody>.UnprocessableEntity(null, "Not connected.");

    try {
      var index = (int)id;
      var mime = await client.GetMessageAsync(index, cancellationToken).ConfigureAwait(false);
      return Result<MailMessageBody>.Ok(await MimeBody.FromMimeAsync(Inbox, id, mime, cancellationToken).ConfigureAwait(false));
    }
    catch (Exception ex) {
      return Result<MailMessageBody>.UnprocessableEntity(null, ex.Message);
    }
  }

  public Task<Result<MailboxQuota?>> GetQuotaAsync(CancellationToken cancellationToken = default) {
    _ = cancellationToken;
    return Task.FromResult(Result<MailboxQuota?>.Ok(null));
  }

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

  private static MailMessageHeader ToHeader(int index, HeaderList headers) {
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
        headers[HeaderId.References]));
  }

  private static string FirstNonEmpty(params string?[] values) {
    foreach (var value in values) {
      if (!string.IsNullOrWhiteSpace(value))
        return value.Trim();
    }

    return "";
  }
}
