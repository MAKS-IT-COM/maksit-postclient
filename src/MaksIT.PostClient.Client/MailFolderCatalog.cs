using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


internal static class MailFolderCatalog {
  public static IReadOnlyList<MailFolderInfo> Normalize(IEnumerable<MailFolderInfo> folders) {
    ArgumentNullException.ThrowIfNull(folders);
    var winner = new Dictionary<string, MailFolderInfo>(StringComparer.OrdinalIgnoreCase);
    var labels = new List<MailFolderInfo>();
    foreach (var folder in folders) {
      if (MailFolderRole.IsHidden(folder.Name, folder.FullName))
        continue;
      var kind = string.IsNullOrWhiteSpace(folder.Kind)
        ? MailFolderRole.Kind(folder.Name, folder.FullName)
        : folder.Kind;
      if (kind is "all" or "flagged" or "important")
        continue;
      if (!MailFolderRole.IsSystemKind(kind)) {
        labels.Add(Rename(folder));
        continue;
      }

      if (!winner.TryGetValue(kind, out var current)
          || MailFolderRole.PreferOver(folder.FullName, current.FullName))
        winner[kind] = folder;
    }

    var rows = new List<MailFolderInfo>();
    foreach (var folder in winner.Values)
      rows.Add(Rename(folder));
    rows.AddRange(labels);
    return rows;
  }

  private static MailFolderInfo Rename(MailFolderInfo folder) =>
    new() {
      FullName = folder.FullName,
      Name = MailFolderRole.DisplayName(folder.Name, folder.FullName),
      Unread = folder.Unread,
      Total = folder.Total,
      Kind = string.IsNullOrWhiteSpace(folder.Kind)
        ? MailFolderRole.Kind(folder.Name, folder.FullName)
        : folder.Kind
    };
}
