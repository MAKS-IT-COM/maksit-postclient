namespace MaksIT.PostClient.Shared.Auth;


public static class MailAuthKind {
  public const string Password = "password";
  public const string Google = "google";
  public const string Microsoft = "microsoft";

  public static bool IsOAuth(string? value) {
    var kind = Normalize(value);
    return kind == Google || kind == Microsoft;
  }

  public static string Normalize(string? value) {
    if (string.IsNullOrWhiteSpace(value))
      return Password;
    return value.Trim().ToLowerInvariant() switch {
      "google" or "gmail" or "oauth-google" => Google,
      "microsoft" or "outlook" or "office365" or "oauth-microsoft" => Microsoft,
      _ => Password
    };
  }
}
