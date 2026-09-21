namespace MaksIT.PostClient.Shared.App;


public static class AppInfo {
  public const string Brand = "MaksIT";
  public const string ProductName = "Postclient";
  public const string Credits = "Maksym Sadovnychyy";
  public const string Email = "maksym.sadovnychyy@gmail.com";
  public const string EmailUri = "mailto:maksym.sadovnychyy@gmail.com";
  public const string Site = "maks-it.com";
  public const string SiteUri = "https://maks-it.com";
  public const string License = "Apache License 2.0";
  public const string Summary = "Desktop mail client for PEC, REM, and IMAP. Mail stays on this PC.";

  public static string Copyright =>
    $"Copyright {DateTime.UtcNow.Year} Maksym Sadovnychyy (MAKS-IT)";
}
