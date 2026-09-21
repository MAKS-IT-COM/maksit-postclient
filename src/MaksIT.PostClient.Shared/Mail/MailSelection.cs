namespace MaksIT.PostClient.Shared.Mail;


public static class MailSelection {
  public static IReadOnlyList<(string MailboxId, string Folder)> Roots(
    IEnumerable<(string MailboxId, string Folder)> folders) {
    var list = new List<(string MailboxId, string Folder)>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var (mailboxId, folder) in folders) {
      if (string.IsNullOrWhiteSpace(mailboxId) || string.IsNullOrWhiteSpace(folder))
        continue;
      var key = mailboxId + "\n" + MailFolderPath.Normalize(folder);
      if (!seen.Add(key))
        continue;
      list.Add((mailboxId, folder));
    }

    return list
      .Where(item => !list.Any(other =>
        other.MailboxId.Equals(item.MailboxId, StringComparison.OrdinalIgnoreCase)
        && MailFolderPath.IsUnder(item.Folder, other.Folder)))
      .ToList();
  }
}
