namespace MaksIT.PostClient.Shared.Mail;


public static class MailLayout {
  public const string Stacked = "stacked";
  public const string Wide = "wide";

  public static string Normalize(string? value) {
    if (string.IsNullOrWhiteSpace(value))
      return Stacked;
    return value.Trim().ToLowerInvariant() switch {
      "wide" or "outlook" or "columns" or "three" or "3" => Wide,
      _ => Stacked
    };
  }

  public static bool IsWide(string? value) =>
    Normalize(value) == Wide;

  public static bool IsStacked(string? value) =>
    Normalize(value) == Stacked;
}
