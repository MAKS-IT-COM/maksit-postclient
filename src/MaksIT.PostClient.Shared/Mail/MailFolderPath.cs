namespace MaksIT.PostClient.Shared.Mail;


public static class MailFolderPath {
  public const char Slash = '/';
  public const char Dot = '.';

  public static string Normalize(string? path) =>
    (path ?? "").Replace('\\', Slash).Trim().TrimEnd(Slash);

  public static char Separator(string? path, char hinted = '\0') {
    if (hinted is Slash or Dot)
      return hinted;
    var value = Normalize(path);
    if (value.Contains(Slash) || IsGmailMailbox(value) || !StartsWithInboxDot(value))
      return Slash;
    return Dot;
  }

  public static bool IsGmailMailbox(string? path) {
    var value = (path ?? "").Trim();
    return value.StartsWith("[Gmail]", StringComparison.OrdinalIgnoreCase)
      || value.StartsWith("[Google Mail]", StringComparison.OrdinalIgnoreCase)
      || value.StartsWith("[GoogleMail]", StringComparison.OrdinalIgnoreCase);
  }

  public static string Leaf(string? path, char hinted = '\0') {
    var value = Normalize(path);
    var at = LastSeparator(value, hinted);
    return at >= 0 ? value[(at + 1)..] : value;
  }

  public static string? Parent(string? path, char hinted = '\0') {
    var value = Normalize(path);
    var at = LastSeparator(value, hinted);
    return at > 0 ? value[..at] : null;
  }

  public static string? UserParent(string? path, char hinted = '\0') =>
    Parent(path, hinted);

  public static string? TreeParent(string? path, IEnumerable<string> known, char hinted = '\0') {
    var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var row in known)
      taken.Add(row);
    var current = UserParent(path, hinted);
    while (!string.IsNullOrWhiteSpace(current)) {
      if (taken.Contains(current))
        return current;
      current = UserParent(current, hinted);
    }

    return null;
  }

  public static string Combine(string? parent, string leaf, char hinted = '\0') {
    var name = (leaf ?? "").Trim().Trim(Slash, '\\');
    var head = Normalize(parent);
    if (head.Length == 0 || name.Length == 0)
      return name;
    return head + Separator(head, hinted) + name;
  }

  public static string? Rewrite(string? stored, string from, string to, char hinted = '\0') {
    if (string.IsNullOrWhiteSpace(stored) || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
      return null;
    var value = stored.Trim();
    if (Same(value, from) || (!LooksNested(value, hinted) && Same(value, Leaf(from, hinted))))
      return to;
    var sep = Separator(from, hinted);
    var prefix = from + sep;
    if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
      return to + sep + value[prefix.Length..];
    var current = Normalize(value);
    var old = Normalize(from);
    if (sep == Slash
        && current.Length > old.Length
        && current.StartsWith(old + Slash, StringComparison.OrdinalIgnoreCase))
      return Normalize(to) + current[old.Length..];
    return null;
  }

  public static bool IsUnder(string? path, string? ancestor, char hinted = '\0') {
    var current = Normalize(path);
    var root = Normalize(ancestor);
    if (current.Length == 0 || root.Length == 0 || Same(current, root))
      return false;
    return current.StartsWith(root + Separator(current, hinted), StringComparison.OrdinalIgnoreCase);
  }

  public static bool IsSelfOrUnder(string? path, string? ancestor, char hinted = '\0') {
    var current = Normalize(path);
    var root = Normalize(ancestor);
    if (current.Length == 0 || root.Length == 0)
      return false;
    return Same(current, root) || IsUnder(current, root, hinted);
  }

  public static bool SameParent(string? folder, string? destParent, char hinted = '\0') {
    var parent = UserParent(folder, hinted);
    var dest = string.IsNullOrWhiteSpace(destParent) ? null : Normalize(destParent);
    if (string.IsNullOrWhiteSpace(parent))
      return dest is null;
    return dest is not null && Same(parent, dest);
  }

  public static string UniqueLeaf(string? wanted, IEnumerable<string> existing) {
    var name = (wanted ?? "").Trim().Trim(Slash, '\\');
    if (name.Length == 0)
      name = "Folder";
    var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var row in existing)
      taken.Add(Leaf(row));
    if (!taken.Contains(name))
      return name;
    for (var i = 2; i < 1000; i++) {
      var candidate = name + " (" + i + ")";
      if (!taken.Contains(candidate))
        return candidate;
    }

    return name + " " + Guid.NewGuid().ToString("N")[..8];
  }

  private static int LastSeparator(string value, char hinted) {
    var sep = Separator(value, hinted);
    return value.LastIndexOf(sep);
  }

  private static bool StartsWithInboxDot(string value) =>
    value.StartsWith("INBOX.", StringComparison.OrdinalIgnoreCase);

  private static bool LooksNested(string path, char hinted) {
    var value = Normalize(path);
    return value.Contains(Separator(value, hinted));
  }

  private static bool Same(string left, string right) =>
    Normalize(left).Equals(Normalize(right), StringComparison.OrdinalIgnoreCase);
}
