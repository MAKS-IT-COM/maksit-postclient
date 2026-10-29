using Avalonia.Controls;
using CoreAbout = MaksIT.Core.UI.About.AboutWindow;
using CoreContact = MaksIT.Core.UI.About.AppContact;
using CoreInfo = MaksIT.Core.UI.About.DesktopAppInfo;
using MaksIT.Core.UI.About;


namespace MaksIT.PostClient.UI.Windows;

public static class AboutWindow {
  public static Window Create(Window owner) {
    ArgumentNullException.ThrowIfNull(owner);
    var info = Info();

    return new CoreAbout {
      Title = info.WindowTitle,
      DataContext = new AboutViewModel(info),
      WindowStartupLocation = WindowStartupLocation.CenterOwner,
      Icon = owner.Icon
    };
  }

  public static Task ShowAsync(Window owner) =>
    Create(owner).ShowDialog(owner);

  private static CoreInfo Info() {
    var copy = UiLocale.Copy;

    return new CoreInfo {
      Brand = AppInfo.Brand,
      ProductName = AppInfo.ProductName,
      Summary = copy.AboutBlurb,
      Version = AppVersion.Display(),
      Credits = AppInfo.Credits,
      License = copy.AboutLicense,
      Copyright = AppInfo.Copyright,
      Site = AppInfo.Site,
      SiteUri = AppInfo.SiteUri,
      Title = copy.AboutTitle,
      VersionLabel = copy.AboutVersion,
      CreditsLabel = copy.AboutCredits,
      LicenseLabel = copy.AboutLicenseLabel,
      CopyrightLabel = copy.AboutCopyright,
      CloseLabel = copy.Close,
      Contacts = [
        new CoreContact(AppInfo.Info.Role, AppInfo.Info.Address),
        new CoreContact(AppInfo.Privacy.Role, AppInfo.Privacy.Address),
        new CoreContact(AppInfo.Security.Role, AppInfo.Security.Address),
        new CoreContact(AppInfo.Support.Role, AppInfo.Support.Address)
      ]
    };
  }
}
