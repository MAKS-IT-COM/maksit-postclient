using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.UI;


internal static class WebViewSetup {
  public static void ConfigureProcess() {
    AppPaths.EnsureDirectories();
    Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", AppPaths.WebViewDirectory());
  }

  public static void Apply(WebViewEnvironmentRequestedEventArgs args) {
    ArgumentNullException.ThrowIfNull(args);
    var data = AppPaths.WebViewDirectory();
    var cache = Path.Combine(data, "cache");
    Directory.CreateDirectory(data);
    Directory.CreateDirectory(cache);
    switch (args) {
      case WindowsWebView2EnvironmentRequestedEventArgs webView2:
        webView2.UserDataFolder = data;
        webView2.ProfileName = AppPaths.ProductName;
        break;
      case GtkWebViewEnvironmentRequestedEventArgs gtk:
        gtk.BaseDataDirectory = data;
        gtk.BaseCacheDirectory = cache;
        gtk.ExperimentalOffscreen = true;
        gtk.ApplicationNameForUserAgent = AppPaths.ProductName;
        break;
      case LinuxWpeWebViewEnvironmentRequestedEventArgs wpe:
        wpe.DataDirectory = data;
        wpe.CacheDirectory = cache;
        break;
      case AppleWKWebViewEnvironmentRequestedEventArgs apple:
        apple.ApplicationNameForUserAgent = AppPaths.ProductName;
        break;
    }
  }

  public static void NavigateHtml(NativeWebView web, string html) {
    ArgumentNullException.ThrowIfNull(web);
    var document = string.IsNullOrWhiteSpace(html) ? MessageHtml.Document("") : html;
    try {
      web.NavigateToString(document);
    }
    catch {
      var path = Path.Combine(AppPaths.WebViewDirectory(), "reading-" + Guid.NewGuid().ToString("N") + ".html");
      File.WriteAllText(path, document);
      web.Navigate(new Uri(path));
    }
  }

  public static void OpenExternalLink(object? sender, WebViewNavigationStartingEventArgs e) {
    var uri = e.Request;
    if (uri is null)
      return;
    if (uri.Scheme is "about" or "data" or "blob")
      return;
    e.Cancel = true;
    if (uri.Scheme is not "http" and not "https")
      return;
    try {
      Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
    }
    catch {
    }
  }

  public static string MissingEngineHint() {
    if (OperatingSystem.IsLinux())
      return "HTML view needs WebKitGTK: sudo apt install libgtk-3-0 libwebkit2gtk-4.1-0 libsoup-3.0-0";
    if (OperatingSystem.IsWindows())
      return "HTML view needs Edge WebView2 and a writable profile under LocalAppData.";
    return "HTML view could not start. Showing text.";
  }
}
