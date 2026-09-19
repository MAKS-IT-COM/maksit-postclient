using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


internal static class MailFolderCatalog {
  public static IReadOnlyList<MailFolderInfo> Normalize(IEnumerable<MailFolderInfo> folders) =>
    Normalize(folders, MailFolderLayout.Imap);

  public static IReadOnlyList<MailFolderInfo> Normalize(
    IEnumerable<MailFolderInfo> folders,
    MailFolderLayout layout) {
    ArgumentNullException.ThrowIfNull(folders);
    ArgumentNullException.ThrowIfNull(layout);
    var winner = new Dictionary<string, MailFolderInfo>(StringComparer.OrdinalIgnoreCase);
    var labels = new List<MailFolderInfo>();
    foreach (var folder in folders) {
      if (layout.HideVirtualFolders && MailFolderRole.IsHidden(folder.Name, folder.FullName))
        continue;
      var kind = string.IsNullOrWhiteSpace(folder.Kind)
        ? MailFolderRole.Kind(folder.Name, folder.FullName)
        : folder.Kind;
      if (layout.HideVirtualFolders && kind is "all" or "flagged" or "important")
        continue;
      if (!MailFolderRole.IsSystemKind(kind)) {
        labels.Add(Rename(folder, layout));
        continue;
      }

      if (!winner.TryGetValue(kind, out var current)
          || (layout.PreferGmailSystemPaths && MailFolderRole.PreferOver(folder.FullName, current.FullName)))
        winner[kind] = folder;
    }

    var rows = new List<MailFolderInfo>();
    foreach (var folder in winner.Values)
      rows.Add(Rename(folder, layout));
    rows.AddRange(labels);
    return layout.InferMissingAncestors ? EnsureAncestors(rows, layout) : rows;
  }

  private static IReadOnlyList<MailFolderInfo> EnsureAncestors(
    IReadOnlyList<MailFolderInfo> rows,
    MailFolderLayout layout) {
    var byFull = new Dictionary<string, MailFolderInfo>(StringComparer.OrdinalIgnoreCase);
    foreach (var row in rows)
      byFull[row.FullName] = row;
    foreach (var row in rows) {
      var delimiter = FolderDelimiter(row, layout);
      var current = MailFolderPath.Parent(row.FullName, delimiter);
      while (!string.IsNullOrWhiteSpace(current)) {
        if (!layout.PreferGmailSystemPaths && MailFolderPath.IsGmailMailbox(current))
          break;
        if (!byFull.ContainsKey(current)
            && !(layout.HideVirtualFolders && MailFolderRole.IsHidden(null, current))) {
          var added = Rename(new MailFolderInfo {
            FullName = current,
            Name = MailFolderPath.Leaf(current, delimiter),
            Delimiter = delimiter
          }, layout);
          byFull[current] = added;
        }

        current = MailFolderPath.Parent(current, delimiter);
      }
    }

    return byFull.Values.ToList();
  }

  private static MailFolderInfo Rename(MailFolderInfo folder, MailFolderLayout layout) =>
    new() {
      FullName = folder.FullName,
      Name = MailFolderRole.DisplayName(folder.Name, folder.FullName),
      Unread = folder.Unread,
      Total = folder.Total,
      Kind = string.IsNullOrWhiteSpace(folder.Kind)
        ? MailFolderRole.Kind(folder.Name, folder.FullName)
        : folder.Kind,
      Delimiter = FolderDelimiter(folder, layout)
    };

  private static char FolderDelimiter(MailFolderInfo folder, MailFolderLayout layout) =>
    layout.Separator(folder.FullName, folder.Delimiter);
}
