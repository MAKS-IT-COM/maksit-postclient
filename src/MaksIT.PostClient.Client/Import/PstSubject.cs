namespace MaksIT.PostClient.Client.Import;


public static class PstSubject {
  public static string Display(string? subject, string? normalized = null, string? prefix = null) {
    var raw = StripMetadata(subject);
    if (LooksReadable(raw))
      return raw.Trim();
    var topic = (normalized ?? "").Trim();
    if (topic.Length == 0)
      return raw.Trim();
    var lead = (prefix ?? "").Trim();
    if (lead.Length == 0)
      return topic;
    return lead.EndsWith(':') ? lead + " " + topic : lead + " " + topic;
  }

  public static string StripMetadata(string? subject) {
    if (string.IsNullOrEmpty(subject))
      return "";
    if (subject[0] != '\u0001' || subject.Length < 2)
      return subject;
    return subject[2..];
  }

  private static bool LooksReadable(string text) {
    if (string.IsNullOrWhiteSpace(text))
      return false;
    var start = text.TrimStart();
    return start.Length > 0 && start[0] >= 32;
  }
}
