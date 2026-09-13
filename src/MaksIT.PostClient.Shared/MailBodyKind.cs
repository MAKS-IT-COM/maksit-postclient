namespace MaksIT.PostClient.Shared;


public static class MailBodyKind {
  public const string Html = "html";
  public const string Text = "text";
  public const string Source = "source";
  public const string Fattura = "fattura";

  public static string Normalize(string? value) {
    if (string.IsNullOrWhiteSpace(value))
      return Html;
    return value.Trim().ToLowerInvariant() switch {
      "text" or "plain" => Text,
      "source" or "eml" or "raw" => Source,
      "fattura" or "fatturapa" => Fattura,
      _ => Html
    };
  }

  public static bool IsHtml(string? value) =>
    Normalize(value) == Html;

  public static bool IsText(string? value) =>
    Normalize(value) == Text;

  public static bool IsSource(string? value) =>
    Normalize(value) == Source;

  public static bool IsFattura(string? value) =>
    Normalize(value) == Fattura;

  public static string Preference(string? value) =>
    IsText(value) ? Text : Html;

  public static string DefaultView(string? html, string? text, string? preference) {
    var markup = MessageHtml.HasMarkup(html);
    var plain = !string.IsNullOrWhiteSpace(text);
    if (markup && plain)
      return Preference(preference);
    return markup ? Html : Text;
  }
}
