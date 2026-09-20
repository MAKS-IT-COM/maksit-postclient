namespace MaksIT.PostClient.Shared;


public static class MailRetention {
  public const string TrashFolder = "Deleted Items";

  public static bool IsTrash(string? name, string? fullName = null) =>
    MailFolderRole.Kind(name, fullName ?? name) == "trash";

  public static string ResolveTrash(IEnumerable<string>? folders) {
    foreach (var folder in folders ?? []) {
      if (string.IsNullOrWhiteSpace(folder))
        continue;
      if (IsTrash(folder))
        return folder;
    }

    return TrashFolder;
  }

  public static string ResolveTrash(IReadOnlyList<(string Name, string FullName)> folders) {
    foreach (var folder in folders) {
      if (string.IsNullOrWhiteSpace(folder.FullName))
        continue;
      if (MailFolderRole.Kind(folder.Name, folder.FullName) == "trash")
        return folder.FullName;
    }

    return TrashFolder;
  }

  public static string BindFolder(string? wanted, IEnumerable<string>? folders) {
    var list = (folders ?? [])
      .Where(folder => !string.IsNullOrWhiteSpace(folder))
      .Select(folder => (folder, folder))
      .ToList();
    return MailRuleEngine.ExactFolder(wanted, list)
      ?? MailRuleEngine.ResolveFolder(wanted, list)
      ?? (wanted ?? "").Trim();
  }

  public static int DaysFor(string mailboxId, string folder, IEnumerable<FolderRetention>? retention) {
    if (string.IsNullOrWhiteSpace(mailboxId) || string.IsNullOrWhiteSpace(folder))
      return 0;
    foreach (var row in Jobs(retention)) {
      if (!row.MailboxId.Equals(mailboxId, StringComparison.OrdinalIgnoreCase))
        continue;
      if (row.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase))
        return row.Days;
      if (IsTrash(folder) && IsTrash(row.Folder))
        return row.Days;
    }

    return 0;
  }

  public static IReadOnlyList<FolderRetention> Jobs(IEnumerable<FolderRetention>? retention) {
    var map = new Dictionary<string, FolderRetention>(StringComparer.OrdinalIgnoreCase);
    foreach (var row in retention ?? [])
      Put(map, row);
    return map.Values.Where(row => row.Days > 0).ToList();
  }

  private static void Put(Dictionary<string, FolderRetention> map, FolderRetention row) {
    if (string.IsNullOrWhiteSpace(row.MailboxId) || string.IsNullOrWhiteSpace(row.Folder))
      return;
    var mailboxId = row.MailboxId.Trim();
    var folder = row.Folder.Trim();
    var days = Math.Max(0, row.Days);
    if (days <= 0)
      return;
    var key = mailboxId + "\n" + folder;
    if (map.TryGetValue(key, out var existing)) {
      if (days < existing.Days)
        existing.Days = days;
      return;
    }

    map[key] = new FolderRetention {
      MailboxId = mailboxId,
      Folder = folder,
      Days = days
    };
  }
}
