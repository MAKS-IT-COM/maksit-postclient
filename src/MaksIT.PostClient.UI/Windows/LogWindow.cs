using Avalonia.Controls;
using CoreLog = MaksIT.Core.UI.Logs.LogWindow;
using MaksIT.Core.UI.Logs;


namespace MaksIT.PostClient.UI.Windows;


public static class LogWindow {
  public static Window Create(Window owner, string? directory = null) {
    ArgumentNullException.ThrowIfNull(owner);
    var options = Options(directory);
    var text = options.ResolveText();

    return new CoreLog {
      Title = text.Title,
      Icon = owner.Icon,
      DataContext = new LogViewModel(options.Directory, text),
      WindowStartupLocation = WindowStartupLocation.CenterOwner
    };
  }

  public static Task ShowAsync(Window owner) =>
    Create(owner).ShowDialog(owner);

  private static DesktopLogOptions Options(string? directory) =>
    new() {
      Directory = string.IsNullOrWhiteSpace(directory) ? LogsDirectory : () => directory,
      Text = () => {
        var copy = UiLocale.Copy;

        return new DesktopLogText {
          Title = copy.LogsTitle,
          CopyLabel = copy.CopyDetails,
          CloseLabel = copy.Close,
          CopiedLabel = copy.Copied
        };
      }
    };

  private static string LogsDirectory() {
    AppPaths.EnsureDirectories();

    return AppPaths.LogsDirectory();
  }
}
