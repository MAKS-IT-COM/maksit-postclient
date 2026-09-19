namespace MaksIT.PostClient.Shared;


public sealed class MailFolderLayout {
  public bool HideVirtualFolders { get; }

  public bool PreferGmailSystemPaths { get; }

  public bool LiftSystemFoldersOffInbox { get; }

  public bool InferMissingAncestors { get; }

  public char DefaultDelimiter { get; }

  private MailFolderLayout(
    bool hideVirtualFolders,
    bool preferGmailSystemPaths,
    bool liftSystemFoldersOffInbox,
    bool inferMissingAncestors,
    char defaultDelimiter) {
    HideVirtualFolders = hideVirtualFolders;
    PreferGmailSystemPaths = preferGmailSystemPaths;
    LiftSystemFoldersOffInbox = liftSystemFoldersOffInbox;
    InferMissingAncestors = inferMissingAncestors;
    DefaultDelimiter = defaultDelimiter;
  }

  public static MailFolderLayout Gmail { get; } = new(
    hideVirtualFolders: true,
    preferGmailSystemPaths: true,
    liftSystemFoldersOffInbox: false,
    inferMissingAncestors: true,
    defaultDelimiter: MailFolderPath.Slash);

  public static MailFolderLayout Pec { get; } = new(
    hideVirtualFolders: false,
    preferGmailSystemPaths: false,
    liftSystemFoldersOffInbox: true,
    inferMissingAncestors: true,
    defaultDelimiter: MailFolderPath.Dot);

  public static MailFolderLayout Outlook { get; } = new(
    hideVirtualFolders: false,
    preferGmailSystemPaths: false,
    liftSystemFoldersOffInbox: true,
    inferMissingAncestors: true,
    defaultDelimiter: MailFolderPath.Slash);

  public static MailFolderLayout Imap { get; } = new(
    hideVirtualFolders: false,
    preferGmailSystemPaths: false,
    liftSystemFoldersOffInbox: true,
    inferMissingAncestors: true,
    defaultDelimiter: '\0');

  public static MailFolderLayout Store { get; } = new(
    hideVirtualFolders: false,
    preferGmailSystemPaths: false,
    liftSystemFoldersOffInbox: false,
    inferMissingAncestors: true,
    defaultDelimiter: MailFolderPath.Slash);

  public static MailFolderLayout For(MailboxAccount? box) =>
    For(box?.Provider, box?.CertifiedKind);

  public static MailFolderLayout For(string? provider, string? certifiedKind = null) {
    var id = MailProvider.Normalize(provider);
    if (id == MailProvider.Gmail)
      return Gmail;
    if (id == MailProvider.Outlook)
      return Outlook;
    if (MailProvider.IsLocalBucket(id))
      return Store;
    if (MailProvider.IsPec(id) || MailCertifiedKind.TracksReceipts(certifiedKind))
      return Pec;
    return Imap;
  }

  public char Separator(string? path, char hinted = '\0') {
    if (hinted is MailFolderPath.Slash or MailFolderPath.Dot)
      return hinted;
    if (DefaultDelimiter is MailFolderPath.Slash or MailFolderPath.Dot)
      return DefaultDelimiter;
    return MailFolderPath.Separator(path);
  }
}
