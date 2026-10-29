using Microsoft.Extensions.DependencyInjection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.ApplicationLifetimes;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI;


public partial class App : Application {
  private ServiceProvider? _services;

  public override void Initialize() =>
    AvaloniaXamlLoader.Load(this);

  public override void OnFrameworkInitializationCompleted() {
    ScreenshotTourOptions? tour = null;

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime launch
        && !ScreenshotTourOptions.TryParse(launch.Args, out tour, out var tourError))
      FailScreenshotTour(tourError);

    try {
      CompleteStartup(tour);
    }
    catch (Exception ex) {
      ErrorDialog.ReportBlocking(ex);
    }

    base.OnFrameworkInitializationCompleted();
    Dispatcher.UIThread.Post(ShowMain, DispatcherPriority.Loaded);
  }

  private void CompleteStartup(ScreenshotTourOptions? tour) {
    AppPaths.EnsureDirectories();
    var services = new ServiceCollection();
    services.AddSingleton(_ => new ConfigurationFileService());
    services.AddPostClient();
    services.AddSingleton<MainViewModel>();
    services.AddSingleton(sp => new MainWindow(
      sp.GetRequiredService<MainViewModel>(),
      sp.GetRequiredService<ConfigurationFileService>(),
      tour));
    _services = services.BuildServiceProvider();

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
      desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
      desktop.MainWindow = _services.GetRequiredService<MainWindow>();
      ApplyTrayCopy();
      UiLocale.Changed += ApplyTrayCopy;
      desktop.ShutdownRequested += async (_, _) => {
        if (_services is null)
          return;

        await _services.GetRequiredService<MainViewModel>().DisposeAsync();
        await _services.DisposeAsync();
        _services = null;
      };
    }
  }

  private void OnTrayClicked(object? sender, EventArgs e) =>
    ShowMain();

  private void OnTrayOpenClick(object? sender, EventArgs e) =>
    ShowMain();

  private void OnTrayExitClick(object? sender, EventArgs e) {
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime life)
      life.Shutdown();
  }

  private void ShowMain() {
    if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
      return;
    window.Show();
    window.WindowState = WindowState.Normal;
    window.Activate();
  }

  private void ApplyTrayCopy() {
    var copy = UiLocale.Copy;
    NativeMenuItem? open = null;
    NativeMenuItem? exit = null;
    ResolveTrayItems(ref open, ref exit);
    if (open is not null)
      open.Header = copy.TrayOpen;
    if (exit is not null)
      exit.Header = copy.TrayExit;
  }

  private void ResolveTrayItems(ref NativeMenuItem? open, ref NativeMenuItem? exit) {
    var icons = TrayIcon.GetIcons(this);
    if (icons is null || icons.Count == 0 || icons[0].Menu is not { } menu)
      return;
    NativeMenuItem? first = null;
    NativeMenuItem? last = null;
    foreach (var item in menu.Items) {
      if (item is not NativeMenuItem native)
        continue;
      first ??= native;
      last = native;
    }

    open = first;
    if (last is not null && last != first)
      exit = last;
  }

  private static void FailScreenshotTour(string? error) {
    try {
      if (OperatingSystem.IsWindows())
        AttachConsole(uint.MaxValue);
    }
    catch (Exception) {
      // A windowed process may have no console.
    }

    Console.Error.WriteLine(error ?? "Invalid screenshot arguments.");
    Environment.Exit(2);
  }

  [System.Runtime.InteropServices.DllImport("kernel32.dll")]
  private static extern bool AttachConsole(uint processId);
}
