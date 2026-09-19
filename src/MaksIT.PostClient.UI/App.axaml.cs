using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.ApplicationLifetimes;
using MaksIT.PostClient.Client;
using MaksIT.PostClient.Shared;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI;


public partial class App : Application {
  private IHost? _host;

  public override void Initialize() =>
    AvaloniaXamlLoader.Load(this);

  public override void OnFrameworkInitializationCompleted() {
    try {
      CompleteStartup();
    }
    catch (Exception ex) {
      ErrorDialog.ReportBlocking(ex);
    }

    base.OnFrameworkInitializationCompleted();
    ShowMain();
  }

  private void CompleteStartup() {
    AppPaths.EnsureDirectories();
    _host = Host.CreateDefaultBuilder()
      .ConfigureAppConfiguration(builder => {
        builder.SetBasePath(AppContext.BaseDirectory);
        builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        builder.AddJsonFile(AppPaths.SettingsFile(), optional: true, reloadOnChange: true);
      })
      .ConfigureServices((_, services) => {
        services.AddSingleton(_ => new ConfigurationFileService());
        services.AddPostClient();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
      })
      .Build();

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
      desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
      desktop.MainWindow = _host.Services.GetRequiredService<MainWindow>();
      ShowMain();
      ApplyTrayCopy();
      UiLocale.Changed += ApplyTrayCopy;
      desktop.ShutdownRequested += async (_, _) => {
        if (_host is null)
          return;

        await _host.Services.GetRequiredService<MainViewModel>().DisposeAsync();
        await _host.StopAsync();
        _host.Dispose();
        _host = null;
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
}
