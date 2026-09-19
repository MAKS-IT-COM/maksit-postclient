namespace MaksIT.PostClient.Shared;


public static class MailDragPayload {
  public const string MessageKind = "msg";
  public const string FolderKind = "folder";

  public static string PackMessages(string mailboxId, string folder, IEnumerable<uint> ids) =>
    MessageKind + "\n" + (mailboxId ?? "") + "\n" + (folder ?? "") + "\n"
    + string.Join(",", ids ?? []);

  public static string PackFolder(string mailboxId, string folder) =>
    PackFolders(mailboxId, [folder ?? ""]);

  public static string PackFolders(string mailboxId, IEnumerable<string> folders) {
    var names = new List<string>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var folder in folders ?? []) {
      if (string.IsNullOrWhiteSpace(folder) || !seen.Add(folder))
        continue;
      names.Add(folder);
    }

    return FolderKind + "\n" + (mailboxId ?? "") + "\n" + string.Join("\n", names);
  }

  public static bool TryUnpackMessages(
    string? packed,
    out string mailboxId,
    out string folder,
    out List<uint> ids) {
    mailboxId = "";
    folder = "";
    ids = [];
    if (!TryParts(packed, MessageKind, 4, out var parts))
      return false;
    mailboxId = parts[1];
    folder = parts[2];
    foreach (var token in parts[3].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
      if (uint.TryParse(token, out var id))
        ids.Add(id);
    }

    return mailboxId.Length > 0 && folder.Length > 0 && ids.Count > 0;
  }

  public static bool TryUnpackFolder(string? packed, out string mailboxId, out string folder) {
    folder = "";
    if (!TryUnpackFolders(packed, out mailboxId, out var folders) || folders.Count == 0)
      return false;
    folder = folders[0];
    return true;
  }

  public static bool TryUnpackFolders(string? packed, out string mailboxId, out List<string> folders) {
    mailboxId = "";
    folders = [];
    if (string.IsNullOrWhiteSpace(packed))
      return false;
    var parts = packed.Split('\n');
    if (parts.Length < 3 || !parts[0].Equals(FolderKind, StringComparison.Ordinal))
      return false;
    mailboxId = parts[1];
    for (var i = 2; i < parts.Length; i++) {
      if (parts[i].Length > 0)
        folders.Add(parts[i]);
    }

    return mailboxId.Length > 0 && folders.Count > 0;
  }

  private static bool TryParts(string? packed, string kind, int count, out string[] parts) {
    parts = [];
    if (string.IsNullOrWhiteSpace(packed))
      return false;
    parts = packed.Split('\n', count, StringSplitOptions.None);
    return parts.Length >= count
      && parts[0].Equals(kind, StringComparison.Ordinal);
  }
}
