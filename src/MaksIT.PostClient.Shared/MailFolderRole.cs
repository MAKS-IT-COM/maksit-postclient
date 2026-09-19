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
      "namespace" => 8,
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
      "namespace" => NamespaceTitle(name, fullName),
      _ => CustomDisplayName(name, fullName)
    };
  }

  public static string Kind(string? name, string? fullName) {
    if (IsNamespace(name, fullName))
      return "namespace";
    var leaf = MailFolderPath.Leaf(fullName);
    if (Matches(
      name,
      fullName,
      leaf,
      "INBOX",
      "Inbox",
      "Posta in arrivo",
      "Boîte de réception",
      "Posteingang",
      "Bandeja de entrada"))
      return "inbox";
    if (Matches(name, fullName, leaf, "Ricevute", "Receipts", "PEC Ricevute", "Avis", "Nachweise", "Acuses"))
      return "receipts";
    if (Matches(name, fullName, leaf, "Drafts", "Draft", "Bozze", "Brouillons", "Entwürfe", "Borradores"))
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
    if (Matches(name, fullName, leaf, "Archives", "Archive", "Archivio", "Archiv", "Archivo"))
      return "archives";
    if (Matches(
      name,
      fullName,
      leaf,
      "Junk",
      "Spam",
      "Indesiderata",
      "Posta indesiderata",
      "Bulk Mail",
      "Indésirable",
      "Courrier indésirable",
      "No deseado",
      "Correo no deseado"))
      return "junk";
    if (Matches(name, fullName, leaf, "Trash", "Deleted", "Deleted Items", "Cestino", "Bin", "Corbeille", "Papelera", "Papierkorb"))
      return "trash";
    return "";
  }

  public static bool IsSystemKind(string? kind) =>
    kind is "inbox" or "receipts" or "drafts" or "sent" or "archives" or "junk" or "trash" or "namespace";

  public static bool IsCustom(string? name, string? fullName) {
    if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(fullName))
      return false;
    if (IsHidden(name, fullName) || IsNamespace(name, fullName))
      return false;
    return !IsSystemKind(Kind(name, fullName));
  }

  public static bool CanHoldFolders(string? name, string? fullName) =>
    IsNamespace(name, fullName) || IsCustom(name, fullName);

  public static string? DisplayParent(
    string? name,
    string? fullName,
    IEnumerable<string> known,
    MailFolderLayout layout,
    char hinted = '\0') {
    ArgumentNullException.ThrowIfNull(layout);
    var parent = MailFolderPath.TreeParent(fullName, known, layout.Separator(fullName, hinted));
    if (string.IsNullOrWhiteSpace(parent))
      return null;
    if (!layout.LiftSystemFoldersOffInbox || Kind(null, parent) != "inbox")
      return parent;
    var kind = Kind(name, fullName);
    if (kind.Length > 0 && kind != "inbox" && IsSystemKind(kind))
      return null;
    return parent;
  }

  public static bool IsHidden(string? name, string? fullName) =>
    Kind(name, fullName) is "all" or "flagged" or "important";

  public static bool IsNamespace(string? name, string? fullName) {
    var leaf = MailFolderPath.Normalize(name);
    var path = MailFolderPath.Normalize(fullName);
    return IsReservedMailbox(leaf) || IsReservedMailbox(path);
  }

  public static bool IsGmailPath(string? fullName) =>
    MailFolderPath.IsGmailMailbox(fullName);

  public static bool PreferOver(string? candidateFullName, string? currentFullName) {
    var gmailCandidate = IsGmailPath(candidateFullName);
    var gmailCurrent = IsGmailPath(currentFullName);
    return gmailCandidate && !gmailCurrent;
  }

  private static string NamespaceTitle(string? name, string? fullName) {
    var path = MailFolderPath.Normalize(fullName);
    if (IsReservedMailbox(path))
      return path;
    var leaf = MailFolderPath.Normalize(name);
    return IsReservedMailbox(leaf) ? leaf : "[Gmail]";
  }

  private static bool IsReservedMailbox(string? value) =>
    Eq(value, "[Gmail]")
    || Eq(value, "[Google Mail]")
    || Eq(value, "[GoogleMail]");

  private static string CustomDisplayName(string? name, string? fullName) {
    var leaf = MailFolderPath.Leaf(fullName);
    if (!string.IsNullOrWhiteSpace(leaf))
      return leaf;
    return (name ?? "").Trim();
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
