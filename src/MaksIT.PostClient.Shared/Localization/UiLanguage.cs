using System.Globalization;


namespace MaksIT.PostClient.Shared.Localization;


public static class UiLanguage {
  public const string En = "en";

  public const string It = "it";

  public const string Fr = "fr";

  public const string De = "de";

  public const string Es = "es";

  public static IReadOnlyList<string> All { get; } = [En, It, Fr, De, Es];

  public static string Normalize(string? value) {
    var id = (value ?? "").Trim().ToLowerInvariant();
    foreach (var known in All) {
      if (id.StartsWith(known, StringComparison.Ordinal))
        return known;
    }

    return En;
  }

  public static string Detect() {
    try {
      var ui = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
      foreach (var known in All) {
        if (ui.Equals(known, StringComparison.OrdinalIgnoreCase))
          return known;
      }
    }
    catch {
    }

    return En;
  }

  public static string Title(string? value) =>
    Normalize(value) switch {
      It => "Italiano",
      Fr => "Français",
      De => "Deutsch",
      Es => "Español",
      _ => "English"
    };
}
