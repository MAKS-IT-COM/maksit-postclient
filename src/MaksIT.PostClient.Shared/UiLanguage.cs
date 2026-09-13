using System.Globalization;


namespace MaksIT.PostClient.Shared;


public static class UiLanguage {
  public const string En = "en";

  public const string It = "it";

  public static IReadOnlyList<string> All { get; } = [En, It];

  public static string Normalize(string? value) {
    var id = (value ?? "").Trim().ToLowerInvariant();
    if (id.StartsWith("it", StringComparison.Ordinal))
      return It;
    if (id.StartsWith("en", StringComparison.Ordinal))
      return En;
    return En;
  }

  public static string Detect() {
    try {
      var ui = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
      if (ui.Equals(It, StringComparison.OrdinalIgnoreCase))
        return It;
    }
    catch {
    }

    return En;
  }

  public static string Title(string? value) =>
    Normalize(value) == It ? "Italiano" : "English";
}
