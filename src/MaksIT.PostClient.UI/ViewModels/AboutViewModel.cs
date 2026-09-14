using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.UI.ViewModels;


public partial class AboutViewModel {
  public UiCopy Copy =>
    UiLocale.Copy;

  public string Brand => AppInfo.Brand;

  public string ProductName => AppInfo.ProductName;

  public string Summary => Copy.AboutBlurb;

  public string Version => AppVersion.Display();

  public string Credits => AppInfo.Credits;

  public string Email => AppInfo.Email;

  public string License => Copy.AboutLicense;

  public string Copyright => AppInfo.Copyright;

  public string Site => AppInfo.Site;

  [RelayCommand]
  private void OpenEmail() =>
    OpenUrl(AppInfo.EmailUri);

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
