namespace MaksIT.PostClient.Shared;


public static class MailFolderRole {
  public static int SortKey(string? name, string? fullName) =>
    Kind(name, fullName) switch {
      "inbox" => 0,
      "receipts" => 1,
      "drafts" => 2,
      "sent" => 3,
      "archives" => 4,
      "junk" => 5,
      "trash" => 6,
      _ => 100
    };

  public static string Glyph(string? name, string? fullName) =>
    Kind(name, fullName) switch {
      "inbox" => "📥",
      "receipts" => "☑",
      "drafts" => "✎",
      "sent" => "↗",
      "archives" => "▣",
      "junk" => "⚠",
      "trash" => "🗑",
      _ => "📁"
    };

  public static string DisplayName(string? name, string? fullName) {
    var copy = UiLocale.Copy;
    return Kind(name, fullName) switch {
      "inbox" => copy.Inbox,
      "drafts" => copy.Drafts,
      "sent" => copy.Sent,
      "junk" => copy.Junk,
      "trash" => copy.Trash,
      "archives" => copy.Archive,
      "receipts" => copy.Receipts,
      _ => string.IsNullOrWhiteSpace(name) ? Leaf(fullName) : name
    };
  }

  public static string Kind(string? name, string? fullName) {
    var leaf = Leaf(fullName);
    if (Matches(name, fullName, leaf, "INBOX", "Inbox", "Posta in arrivo"))
      return "inbox";
    if (Matches(name, fullName, leaf, "Ricevute", "Receipts", "PEC Ricevute"))
      return "receipts";
    if (Matches(name, fullName, leaf, "Drafts", "Draft", "Bozze"))
      return "drafts";
    if (Matches(
      name,
      fullName,
      leaf,
      "Sent",
      "Sent Items",
      "Sent Messages",
      "Sent Mail",
      "Posta inviata",
      "Messages envoyés",
      "Enviados",
      "Gesendet"))
      return "sent";
    if (Matches(
      name,
      fullName,
      leaf,
      "All Mail",
      "Tutti i messaggi",
      "Tous les messages",
      "Todos",
      "Alle Nachrichten"))
      return "all";
    if (Matches(name, fullName, leaf, "Starred", "Speciali", "Flagged", "Destacados", "Markiert", "Suivis"))
      return "flagged";
    if (Matches(name, fullName, leaf, "Important", "Importanti"))
      return "important";
    if (Matches(name, fullName, leaf, "Archives", "Archive", "Archivio"))
      return "archives";
    if (Matches(name, fullName, leaf, "Junk", "Spam", "Indesiderata", "Posta indesiderata", "Bulk Mail"))
      return "junk";
    if (Matches(name, fullName, leaf, "Trash", "Deleted", "Deleted Items", "Cestino", "Bin", "Corbeille", "Papelera", "Papierkorb"))
      return "trash";
    return "";
  }

  public static bool IsSystemKind(string? kind) =>
    kind is "inbox" or "receipts" or "drafts" or "sent" or "archives" or "junk" or "trash";

  public static bool IsCustom(string? name, string? fullName) {
    if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(fullName))
      return false;
    if (IsHidden(name, fullName))
      return false;
    return !IsSystemKind(Kind(name, fullName));
  }

  public static bool IsHidden(string? name, string? fullName) {
    if (IsNamespace(name, fullName))
      return true;
    return Kind(name, fullName) is "all" or "flagged" or "important";
  }

  public static bool IsNamespace(string? name, string? fullName) =>
    Eq(name, "[Gmail]")
    || Eq(name, "[Google Mail]")
    || Eq(fullName, "[Gmail]")
    || Eq(fullName, "[Google Mail]");

  public static bool IsGmailPath(string? fullName) {
    var value = (fullName ?? "").Trim();
    return value.StartsWith("[Gmail]", StringComparison.OrdinalIgnoreCase)
      || value.StartsWith("[Google Mail]", StringComparison.OrdinalIgnoreCase);
  }

  public static bool PreferOver(string? candidateFullName, string? currentFullName) {
    var gmailCandidate = IsGmailPath(candidateFullName);
    var gmailCurrent = IsGmailPath(currentFullName);
    return gmailCandidate && !gmailCurrent;
  }

  private static string Leaf(string? fullName) {
    var value = fullName ?? "";
    var slash = value.LastIndexOf('/');
    var dot = value.LastIndexOf('.');
    var i = Math.Max(slash, dot);
    return i < 0 ? value : value[(i + 1)..];
  }

  private static bool Matches(string? name, string? fullName, string leaf, params string[] aliases) {
    foreach (var alias in aliases) {
      if (Eq(name, alias) || Eq(fullName, alias) || Eq(leaf, alias))
        return true;
    }

    return false;
  }

  private static bool Eq(string? value, string alias) =>
    !string.IsNullOrWhiteSpace(value)
    && value.Equals(alias, StringComparison.OrdinalIgnoreCase);
}
