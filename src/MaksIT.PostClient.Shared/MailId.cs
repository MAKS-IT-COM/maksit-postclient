namespace MaksIT.PostClient.Shared;


public static class MailId {
  public static string Normalize(string? value) =>
    ReceiptStatus.NormalizeId(value);

  public static IReadOnlyList<string> Tokens(string? value) {
    if (string.IsNullOrWhiteSpace(value))
      return [];
    var tokens = new List<string>();
    foreach (var part in value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
      var id = Normalize(part);
      if (id.Length == 0)
        continue;
      if (!tokens.Contains(id, StringComparer.OrdinalIgnoreCase))
        tokens.Add(id);
    }

    return tokens;
  }

  public static string Parent(string? inReplyTo, string? references = null) {
    var reply = Tokens(inReplyTo);
    if (reply.Count > 0)
      return reply[0];
    var refs = Tokens(references);
    return refs.Count > 0 ? refs[^1] : "";
  }

  public static string Bracket(string? value) {
    var id = Normalize(value);
    return id.Length == 0 ? "" : "<" + id + ">";
  }

  public static string ThreadLine(string? references, string? messageId) {
    var ids = Tokens(references).ToList();
    var last = Normalize(messageId);
    if (last.Length > 0 && !ids.Contains(last, StringComparer.OrdinalIgnoreCase))
      ids.Add(last);
    return string.Join(" ", ids.Select(Bracket));
  }
}
