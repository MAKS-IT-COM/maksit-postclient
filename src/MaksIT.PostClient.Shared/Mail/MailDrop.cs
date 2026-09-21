namespace MaksIT.PostClient.Shared.Mail;


public static class MailDrop {
  public static bool AcceptsFolders(string? incomingProtocol) =>
    !MailProtocol.IsPop3(incomingProtocol);

  public static bool CanDropMessages(
    string? destProtocol,
    string destMailboxId,
    string? destFolder,
    string sourceMailboxId,
    string sourceFolder) {
    if (!AcceptsFolders(destProtocol)
        || string.IsNullOrWhiteSpace(destMailboxId)
        || string.IsNullOrWhiteSpace(sourceMailboxId)
        || string.IsNullOrWhiteSpace(sourceFolder))
      return false;
    if (string.IsNullOrWhiteSpace(destFolder))
      return true;
    return !destMailboxId.Equals(sourceMailboxId, StringComparison.OrdinalIgnoreCase)
      || !MailFolderPath.Normalize(destFolder)
        .Equals(MailFolderPath.Normalize(sourceFolder), StringComparison.OrdinalIgnoreCase);
  }

  public static bool CanDropFolder(
    string? destProtocol,
    string destMailboxId,
    string? destParent,
    string sourceMailboxId,
    string sourceFolder,
    bool sourceIsCustom) {
    if (!sourceIsCustom
        || !AcceptsFolders(destProtocol)
        || string.IsNullOrWhiteSpace(destMailboxId)
        || string.IsNullOrWhiteSpace(sourceMailboxId)
        || string.IsNullOrWhiteSpace(sourceFolder))
      return false;
    if (MailFolderPath.IsSelfOrUnder(destParent, sourceFolder))
      return false;
    return !destMailboxId.Equals(sourceMailboxId, StringComparison.OrdinalIgnoreCase)
      || !MailFolderPath.SameParent(sourceFolder, destParent);
  }
}
