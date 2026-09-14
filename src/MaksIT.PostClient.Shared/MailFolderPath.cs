namespace MaksIT.PostClient.Shared;


public static class MailFolderPath {
  public static string Normalize(string? path) =>
    (path ?? "").Replace('\\', '/').Trim().TrimEnd('/');

  public static string Leaf(string? path) {
    var value = Normalize(path);
    var slash = value.LastIndexOf('/');
    if (slash >= 0)
      return value[(slash + 1)..];
    var dot = value.LastIndexOf('.');
    return dot >= 0 ? value[(dot + 1)..] : value;
  }

  public static string? Parent(string? path) {
    var value = Normalize(path);
    var slash = value.LastIndexOf('/');
    if (slash > 0)
      return value[..slash];
    var dot = value.LastIndexOf('.');
    return dot > 0 ? value[..dot] : null;
  }

  public static string Combine(string? parent, string leaf) {
    var name = (leaf ?? "").Trim().Trim('/', '\\');
    var head = Normalize(parent);
    if (head.Length == 0 || name.Length == 0)
      return name;
    var sep = head.Contains('/') ? "/" : head.Contains('.') ? "." : "/";
    return head + sep + name;
  }

  public static string? Rewrite(string? stored, string from, string to) {
    if (string.IsNullOrWhiteSpace(stored) || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
      return null;
    var value = stored.Trim();
    if (Same(value, from) || (!LooksNested(value) && Same(value, Leaf(from))))
      return to;
    foreach (var sep in new[] { '/', '.', '\\' }) {
      var prefix = from + sep;
      if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        return to + sep + value[prefix.Length..];
    }

    var current = Normalize(value);
    var old = Normalize(from);
    if (current.Length > old.Length && current.StartsWith(old + "/", StringComparison.OrdinalIgnoreCase))
      return Normalize(to) + current[old.Length..];
    return null;
  }

  public static bool IsUnder(string? path, string? ancestor) {
    var current = Normalize(path);
    var root = Normalize(ancestor);
    if (current.Length == 0 || root.Length == 0 || Same(current, root))
      return false;
    if (current.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
      return true;
    return current.StartsWith(root + ".", StringComparison.OrdinalIgnoreCase);
  }

  public static bool IsSelfOrUnder(string? path, string? ancestor) {
    var current = Normalize(path);
    var root = Normalize(ancestor);
    if (current.Length == 0 || root.Length == 0)
      return false;
    return Same(current, root) || IsUnder(current, root);
  }

  public static bool SameParent(string? folder, string? destParent) {
    var parent = Parent(folder);
    if (string.IsNullOrWhiteSpace(parent))
      return string.IsNullOrWhiteSpace(destParent);
    return !string.IsNullOrWhiteSpace(destParent) && Same(parent, destParent);
  }

  public static string UniqueLeaf(string? wanted, IEnumerable<string> existing) {
    var name = (wanted ?? "").Trim().Trim('/', '\\', '.');
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

  private static bool LooksNested(string path) =>
    path.Contains('/', StringComparison.Ordinal)
    || path.Contains('\\', StringComparison.Ordinal)
    || path.Contains('.', StringComparison.Ordinal);

  private static bool Same(string left, string right) =>
    Normalize(left).Equals(Normalize(right), StringComparison.OrdinalIgnoreCase);
}
