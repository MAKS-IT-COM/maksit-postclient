using System.Globalization;


namespace MaksIT.PostClient.Shared.Localization;


public static class UiLocale {
  private static UiCopy copy = UiCopy.For(UiLanguage.En);

  public static string Id { get; private set; } = UiLanguage.En;

  public static UiCopy Copy =>
    copy;

  public static event Action? Changed;

  public static void Apply(string? language) {
    var id = UiLanguage.Normalize(language);
    ApplyCulture(id);
    if (id == Id)
      return;
    Id = id;
    copy = UiCopy.For(id);
    Changed?.Invoke();
  }

  private static void ApplyCulture(string id) {
    try {
      var culture = CultureInfo.GetCultureInfo(id);
      CultureInfo.CurrentCulture = culture;
      CultureInfo.CurrentUICulture = culture;
      CultureInfo.DefaultThreadCurrentCulture = culture;
      CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
    catch {
    }
  }
}
