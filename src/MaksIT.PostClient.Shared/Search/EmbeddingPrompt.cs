namespace MaksIT.PostClient.Shared.Search;


public static class EmbeddingPrompt {
  public const int MaxChars = 6000;

  public static string Query(string text) =>
    "task: search result | query: " + Clip(text);

  public static string Document(string? title, string? from, string? body, string? attachments) {
    var subject = string.IsNullOrWhiteSpace(title) ? "none" : Clip(title, 300);
    var bits = new List<string>();
    if (!string.IsNullOrWhiteSpace(from))
      bits.Add(from.Trim());
    if (!string.IsNullOrWhiteSpace(body))
      bits.Add(body.Trim());
    if (!string.IsNullOrWhiteSpace(attachments))
      bits.Add(attachments.Trim());
    var text = bits.Count == 0 ? "" : string.Join("\n", bits);
    return "title: " + subject + " | text: " + Clip(text);
  }

  public static string Clip(string? value, int max = MaxChars) {
    var text = (value ?? "").Trim();
    if (text.Length <= max)
      return text;
    return text[..max];
  }
}
