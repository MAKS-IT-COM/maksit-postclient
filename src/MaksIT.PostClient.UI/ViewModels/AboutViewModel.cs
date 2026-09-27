using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;


namespace MaksIT.PostClient.UI.ViewModels;


public partial class AboutViewModel {
  public UiCopy Copy =>
    UiLocale.Copy;

  public string Brand => AppInfo.Brand;

  public string ProductName => AppInfo.ProductName;

  public string Summary => Copy.AboutBlurb;

  public string Version => AppVersion.Display();

  public string Credits => AppInfo.Credits;

  public AppContact InfoContact => AppInfo.Info;

  public AppContact PrivacyContact => AppInfo.Privacy;

  public AppContact SecurityContact => AppInfo.Security;

  public AppContact SupportContact => AppInfo.Support;

  public string License => Copy.AboutLicense;

  public string Copyright => AppInfo.Copyright;

  public string Site => AppInfo.Site;

  [RelayCommand]
  private void OpenContact(string? uri) {
    if (string.IsNullOrWhiteSpace(uri))
      return;
    OpenUrl(uri);
  }

  [RelayCommand]
  private void OpenSite() =>
    OpenUrl(AppInfo.SiteUri);

  private static void OpenUrl(string url) {
    try {
      Process.Start(new ProcessStartInfo {
        FileName = url,
        UseShellExecute = true
      });
    }
    catch {
    }
  }
}
