namespace MaksIT.PostClient.Shared;


public static class MailPriority {
  public const string High = "high";
  public const string Normal = "normal";
  public const string Low = "low";
  public const string KeywordHigh = "HighPriority";
  public const string KeywordLow = "LowPriority";

  public static string Resolve(
    IEnumerable<string>? keywords,
    string? xPriority,
    string? importance,
    string? priority) {
    var fromFlags = FromKeywords(keywords);
    if (fromFlags.Length > 0)
      return fromFlags;
    return FromHeaders(xPriority, importance, priority);
  }

  public static string FromKeywords(IEnumerable<string>? keywords) {
    if (keywords is null)
      return "";
    var high = false;
    var low = false;
    foreach (var keyword in keywords) {
      if (Is(keyword, KeywordHigh, "$HighPriority", "$MailPriorityHigh"))
        high = true;
      if (Is(keyword, KeywordLow, "$LowPriority", "$MailPriorityLow"))
        low = true;
    }

    if (high && !low)
      return High;
    if (low && !high)
      return Low;
    return "";
  }

  public static string FromHeaders(string? xPriority, string? importance, string? priority) {
    var fromX = FromRank(xPriority);
    if (fromX.Length > 0)
      return fromX;
    var fromImportance = FromWord(importance);
    if (fromImportance.Length > 0)
      return fromImportance;
    var fromPriority = FromWord(priority);
    if (fromPriority.Length > 0)
      return fromPriority;
    return Normal;
  }

  public static string Mark(string? priority) =>
    priority == High ? "!" : priority == Low ? "↓" : "";

  public static string Label(string? priority) {
    if (priority == High)
      return "High priority";
    if (priority == Low)
      return "Low priority";
    return "Normal priority";
  }

  public static IReadOnlyList<string> KeywordsFor(string? priority) {
    if (priority == High)
      return [KeywordHigh];
    if (priority == Low)
      return [KeywordLow];
    return [];
  }

  private static string FromRank(string? value) {
    if (string.IsNullOrWhiteSpace(value))
      return "";
    var token = value.Trim();
    var digit = token[0];
    if (digit is '1' or '2')
      return High;
    if (digit is '4' or '5')
      return Low;
    if (digit is '3')
      return Normal;
    return FromWord(token);
  }

  private static string FromWord(string? value) {
    if (string.IsNullOrWhiteSpace(value))
      return "";
    var token = value.Trim();
    if (Is(token, "high", "highest", "urgent", "u"))
      return High;
    if (Is(token, "low", "lowest", "non-urgent", "nonurgent", "n"))
      return Low;
    if (Is(token, "normal", "none"))
      return Normal;
    return "";
  }

  private static bool Is(string? value, params string[] aliases) {
    if (string.IsNullOrWhiteSpace(value))
      return false;
    foreach (var alias in aliases) {
      if (value.Equals(alias, StringComparison.OrdinalIgnoreCase))
        return true;
    }

    return false;
  }
}
