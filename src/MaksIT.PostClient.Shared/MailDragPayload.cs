namespace MaksIT.PostClient.Shared;


public static class MailDragPayload {
  public const string MessageKind = "msg";
  public const string FolderKind = "folder";

  public static string PackMessages(string mailboxId, string folder, IEnumerable<uint> ids) =>
    MessageKind + "\n" + (mailboxId ?? "") + "\n" + (folder ?? "") + "\n"
    + string.Join(",", ids ?? []);

  public static string PackFolder(string mailboxId, string folder) =>
    FolderKind + "\n" + (mailboxId ?? "") + "\n" + (folder ?? "");

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
    mailboxId = "";
    folder = "";
    if (!TryParts(packed, FolderKind, 3, out var parts))
      return false;
    mailboxId = parts[1];
    folder = parts[2];
    return mailboxId.Length > 0 && folder.Length > 0;
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
