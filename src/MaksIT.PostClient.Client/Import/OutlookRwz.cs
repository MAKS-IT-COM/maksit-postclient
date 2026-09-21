using System.Text;
using System.Text.RegularExpressions;


namespace MaksIT.PostClient.Client.Import;


public static class OutlookRwz {
  public static bool IsModern(byte[] bytes) =>
    bytes.Length >= 48
    && bytes[0] == 0
    && bytes[1] == 0
    && bytes[2] == 0x14
    && !OleCompound.IsOle(bytes);

  public static IReadOnlyList<MailRule> Parse(byte[] bytes) {
    if (bytes.Length == 0)
      return [];
    var names = new List<string>();
    var froms = new List<string>();
    var folders = new List<string>();
    for (var i = 0; i + 3 < bytes.Length; i++) {
      if (TryName(bytes, i, out var name, out var next)) {
        if (!name.Contains('@', StringComparison.Ordinal))
          names.Add(name);
        i = next - 1;
        continue;
      }

      if (TryFrom(bytes, i, out var from, out next)) {
        froms.Add(from);
        i = next - 1;
        continue;
      }

      if (TryFolderAfterStore(bytes, i, out var folder, out next)) {
        folders.Add(folder);
        i = next - 1;
      }
    }

    if (folders.Count == names.Count + 1)
      folders.RemoveAt(0);
    var count = Math.Min(names.Count, Math.Min(froms.Count, folders.Count == 0 ? int.MaxValue : folders.Count));
    if (count == 0)
      count = Math.Min(names.Count, froms.Count);
    var rules = new List<MailRule>();
    for (var n = 0; n < count; n++) {
      var name = n < names.Count ? names[n] : "";
      var from = n < froms.Count ? froms[n] : "";
      var folder = n < folders.Count ? folders[n] : "";
      var rule = Build(n + 1, name, from, folder);
      if (rule is not null)
        rules.Add(rule);
    }

    return rules;
  }

  private static MailRule? Build(int sequence, string name, string from, string folder) {
    var values = PhraseValues(name);
    if (values.Count <= 1 && !string.IsNullOrWhiteSpace(from))
      values = PhraseValues(from);
    values = values
      .Select(v => v.Trim().Trim('\''))
      .Where(v => v.Length > 0 && !LooksPath(v))
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToList();
    if (values.Count == 0)
      return null;
    var title = string.IsNullOrWhiteSpace(name) || name.Contains('\'', StringComparison.Ordinal)
      ? values[0]
      : name.Trim();
    var dest = string.IsNullOrWhiteSpace(folder) ? title : folder.Trim();
    return new MailRule {
      Name = title,
      Sequence = sequence,
      Logic = values.Count > 1 ? "or" : "and",
      Action = MailRuleAction.Move,
      Folder = dest,
      Conditions = values
        .Select(v => new MailRuleCondition { Field = "from", Op = "contains", Value = v })
        .ToList()
    };
  }

  private static List<string> PhraseValues(string text) {
    if (string.IsNullOrWhiteSpace(text))
      return [];
    var quoted = Regex.Matches(text, "'([^']+)'")
      .Select(m => m.Groups[1].Value.Trim())
      .Where(v => v.Length > 0)
      .ToList();
    if (quoted.Count > 0)
      return quoted;
    if (text.Contains(" or ", StringComparison.OrdinalIgnoreCase))
      return text.Split(" or ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .ToList();
    return [text.Trim()];
  }

  private static bool TryName(byte[] bytes, int i, out string text, out int next) {
    text = "";
    next = i;
    if (bytes[i] != 0x14 || bytes[i + 1] != 0)
      return false;
    var chars = bytes[i + 2];
    if (!TryUtf16(bytes, i + 3, chars, out text) || LooksPath(text))
      return false;
    next = i + 3 + chars * 2;
    return true;
  }

  private static bool TryFrom(byte[] bytes, int i, out string text, out int next) {
    text = "";
    next = i;
    if (i + 16 >= bytes.Length
        || bytes[i] != 0x01
        || bytes[i + 1] != 0x80
        || bytes[i + 2] != 0xE6
        || bytes[i + 3] != 0)
      return false;
    var chars = bytes[i + 14];
    if (!TryUtf16(bytes, i + 15, chars, out text) || LooksPath(text))
      return false;
    next = i + 15 + chars * 2;
    return true;
  }

  private static bool TryFolderAfterStore(byte[] bytes, int i, out string text, out int next) {
    text = "";
    next = i;
    if (!StartsExt(bytes, i, ".ost") && !StartsExt(bytes, i, ".pst"))
      return false;
    var p = i + 8;
    if (p + 3 >= bytes.Length || bytes[p] != 0 || bytes[p + 1] != 0)
      return false;
    var chars = bytes[p + 2];
    if (!TryUtf16(bytes, p + 3, chars, out text) || LooksPath(text) || text.Contains('.', StringComparison.Ordinal))
      return false;
    next = p + 3 + chars * 2;
    return true;
  }

  private static bool StartsExt(byte[] bytes, int i, string ext) {
    var raw = Encoding.Unicode.GetBytes(ext);
    if (i + raw.Length > bytes.Length)
      return false;
    for (var n = 0; n < raw.Length; n++) {
      if (bytes[i + n] != raw[n])
        return false;
    }

    return true;
  }

  private static bool TryUtf16(byte[] bytes, int offset, int chars, out string text) {
    text = "";
    if (chars is < 1 or > 80 || offset < 0 || offset + chars * 2 > bytes.Length)
      return false;
    text = Encoding.Unicode.GetString(bytes, offset, chars * 2);
    if (text.Contains('\0') || text.Any(c => char.IsControl(c)))
      return false;
    return text.Trim().Length > 0;
  }

  private static bool LooksPath(string text) =>
    text.Contains(@"\", StringComparison.Ordinal)
    || text.Contains('/', StringComparison.Ordinal)
    || text.EndsWith(".ost", StringComparison.OrdinalIgnoreCase)
    || text.EndsWith(".pst", StringComparison.OrdinalIgnoreCase);
}
