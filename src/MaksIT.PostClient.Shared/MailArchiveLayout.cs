namespace MaksIT.PostClient.Shared;


public static class MailArchiveLayout {
  public static readonly string[] SystemFolders = [
    "Inbox",
    "Drafts",
    "Sent Items",
    "Deleted Items"
  ];

  public static string DatabasePath(MailboxAccount account, IReadOnlyList<MailboxAccount> mailboxes) {
    ArgumentNullException.ThrowIfNull(account);
    if (account.IsLocalStore && !string.IsNullOrWhiteSpace(account.StorePath))
      return Path.Combine(account.StorePath, "mail.db");
    var store = BoundStore(account, mailboxes);
    if (store is not null)
      return Path.Combine(store.StorePath, "accounts", account.Id, "mail.db");
    return AppPaths.AccountDatabase(account.Id);
  }

  public static string MailRoot(MailboxAccount account, IReadOnlyList<MailboxAccount> mailboxes) {
    ArgumentNullException.ThrowIfNull(account);
    if (account.IsLocalStore && !string.IsNullOrWhiteSpace(account.StorePath))
      return account.StorePath;
    var store = BoundStore(account, mailboxes);
    if (store is not null)
      return Path.Combine(store.StorePath, "accounts", account.Id);
    return Path.Combine(AppPaths.AccountsDirectory(), account.Id);
  }

  public static string EmlPath(
    MailboxAccount account,
    IReadOnlyList<MailboxAccount> mailboxes,
    string folder,
    uint uid) {
    var root = MailRoot(account, mailboxes);
    return ArchiveFiles.StoreEmlPath(root, folder, uid);
  }

  public static MailboxAccount? BoundStore(MailboxAccount account, IReadOnlyList<MailboxAccount> mailboxes) {
    if (account.IsLocalStore || string.IsNullOrWhiteSpace(account.ArchiveStoreId))
      return null;
    return mailboxes.FirstOrDefault(m =>
      m.IsLocalStore
      && m.Id.Equals(account.ArchiveStoreId, StringComparison.OrdinalIgnoreCase)
      && !string.IsNullOrWhiteSpace(m.StorePath)
      && Directory.Exists(m.StorePath));
  }

  public static void EnsureSystemFolders(string storeDirectory) {
    Directory.CreateDirectory(storeDirectory);
    foreach (var folder in SystemFolders)
      Directory.CreateDirectory(Path.Combine(storeDirectory, SanitizeFolder(folder)));
  }

  public static string FolderDirectory(string mailRoot, string folder) =>
    Path.Combine(mailRoot, SanitizeFolder(folder));

  public static string SanitizeFolder(string folder) {
    var name = string.IsNullOrWhiteSpace(folder) ? "Inbox" : folder.Trim();
    foreach (var c in Path.GetInvalidFileNameChars())
      name = name.Replace(c, '_');
    return name.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
  }
}
