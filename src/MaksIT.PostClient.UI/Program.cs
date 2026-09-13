using Avalonia;
using Avalonia.Logging;
using Avalonia.Threading;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.UI;


internal static class Program {
  [STAThread]
  public static void Main(string[] args) {
    WebViewSetup.ConfigureProcess();
    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
  }

  // Linux: X11/XWayland. Avalonia 12.1.2 native Wayland still hangs on GNOME's
  // xdg_toplevel.configure(0, 0) and never maps a window (GNOME app icon).
  public static AppBuilder BuildAvaloniaApp() =>
    AppBuilder.Configure<App>()
      .UsePlatformDetect()
      .LogToTrace(LogEventLevel.Warning)
      .AfterSetup(_ => Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandled);

  private static void OnDispatcherUnhandled(object? sender, DispatcherUnhandledExceptionEventArgs e) {
    if (!IsWebViewFailure(e.Exception))
      return;
    e.Handled = true;
    WebViewFailed?.Invoke(e.Exception);
  }

  public static event Action<Exception>? WebViewFailed;

  private static bool IsWebViewFailure(Exception ex) {
    if (ex is UnauthorizedAccessException)
      return true;
    var text = ex.ToString();
    return text.Contains("WebView", StringComparison.OrdinalIgnoreCase)
      || text.Contains("webkit", StringComparison.OrdinalIgnoreCase)
      || (uint)ex.HResult == 0x80070005;
  }
}
