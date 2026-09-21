using Avalonia;
using Avalonia.Logging;
using Avalonia.Threading;


namespace MaksIT.PostClient.UI;


internal static class Program {
  [STAThread]
  public static void Main(string[] args) {
    AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandled;
    TaskScheduler.UnobservedTaskException += OnUnobservedTask;
    if (MailWorkerHost.IsWorkerProcess(args)) {
      try {
        Environment.Exit(MailWorkerHost.Run(args));
      }
      catch (Exception ex) {
        ErrorReport.Capture(ex);
        Environment.Exit(1);
      }
    }

    try {
      WebViewSetup.ConfigureProcess();
      BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
    catch (Exception ex) {
      ErrorDialog.ReportBlocking(ex);
    }
  }

  // Linux: X11/XWayland. Avalonia 12.1.2 native Wayland still hangs on GNOME's
  // xdg_toplevel.configure(0, 0) and never maps a window (GNOME app icon).
  public static AppBuilder BuildAvaloniaApp() =>
    AppBuilder.Configure<App>()
      .UsePlatformDetect()
      .LogToTrace(LogEventLevel.Warning)
      .AfterSetup(_ => Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandled);

  private static void OnDispatcherUnhandled(object? sender, DispatcherUnhandledExceptionEventArgs e) {
    e.Handled = true;
    if (IsWebViewFailure(e.Exception)) {
      ErrorReport.Capture(e.Exception);
      WebViewFailed?.Invoke(e.Exception);
      return;
    }

    ErrorDialog.Report(e.Exception);
  }

  private static void OnDomainUnhandled(object? sender, UnhandledExceptionEventArgs e) {
    if (e.ExceptionObject is not Exception ex)
      return;
    if (e.IsTerminating)
      ErrorDialog.ReportBlocking(ex);
    else
      ErrorDialog.Report(ex);
  }

  private static void OnUnobservedTask(object? sender, UnobservedTaskExceptionEventArgs e) {
    e.SetObserved();
    ErrorDialog.Report(e.Exception);
  }

  public static event Action<Exception>? WebViewFailed;

  private static bool IsWebViewFailure(Exception ex) {
    if (ex is UnauthorizedAccessException)
      return true;
    var text = ex.ToString();
    return text.Contains("WebView", StringComparison.OrdinalIgnoreCase)
      || text.Contains("webkit", StringComparison.OrdinalIgnoreCase)
      || (uint)ex.HResult == 0x80070005
      || ((uint)ex.HResult == 0x80070057
        && text.Contains("NavigateToString", StringComparison.OrdinalIgnoreCase));
  }
}
