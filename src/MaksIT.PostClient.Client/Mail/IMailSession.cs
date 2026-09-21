using MaksIT.Results;


namespace MaksIT.PostClient.Client.Mail;


public interface IMailSession : IAsyncDisposable {
  bool IsConnected { get; }

  bool SupportsFolders { get; }

  Task<Result> ConnectAsync(MailboxAccount account, string password, CancellationToken cancellationToken = default);

  Task<Result<IReadOnlyList<MailFolderInfo>>> ListFoldersAsync(CancellationToken cancellationToken = default);

  Task<Result<MailFolderSync>> ListMessagesAsync(
    string folder,
    IReadOnlySet<uint>? knownIds = null,
    CancellationToken cancellationToken = default,
    int itemBudget = 0);

  Task<Result<MailMessageBody>> GetMessageAsync(
    string folder,
    uint id,
    CancellationToken cancellationToken = default,
    bool interactive = true);

  Task<Result<MailboxQuota?>> GetQuotaAsync(CancellationToken cancellationToken = default);

  Task<Result<uint?>> AppendAsync(
    string folder,
    byte[] eml,
    CancellationToken cancellationToken = default);

  Task<Result<string>> SendAsync(MailSendRequest request, CancellationToken cancellationToken = default);

  Task<Result> MoveMessagesAsync(
    string fromFolder,
    IReadOnlyList<uint> ids,
    string toFolder,
    CancellationToken cancellationToken = default);

  Task<Result> SetMessageFlagsAsync(
    string folder,
    IReadOnlyList<uint> ids,
    MailFlagUpdate update,
    CancellationToken cancellationToken = default);

  Task<Result> CreateFolderAsync(
    string name,
    string? parentFolder,
    CancellationToken cancellationToken = default);

  Task<Result> RenameFolderAsync(
    string folder,
    string? parentFolder,
    string name,
    CancellationToken cancellationToken = default);

  Task<Result> DeleteFolderAsync(string folder, CancellationToken cancellationToken = default);

  Task<Result> EmptyFolderAsync(
    string folder,
    string? trashFolder,
    CancellationToken cancellationToken = default);

  Task<Result> SetFolderSeenAsync(
    string folder,
    bool seen,
    CancellationToken cancellationToken = default);
}


public interface IMailSessionFactory {
  IMailSession Create(MailboxAccount account);
}
