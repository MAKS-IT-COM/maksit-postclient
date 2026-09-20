using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI.Windows;


public partial class LogWindow : Window {
  public LogWindow() {
    InitializeComponent();
  }

  public static Task ShowAsync(Window owner) {
    var window = new LogWindow {
      DataContext = new LogViewModel(),
      WindowStartupLocation = WindowStartupLocation.CenterOwner
    };
    return window.ShowDialog(owner);
  }

  private async void OnCopyClick(object? sender, RoutedEventArgs e) {
    if (DataContext is not LogViewModel vm)
      return;
    var clipboard = Clipboard;
    if (clipboard is null)
      return;
    try {
      await clipboard.SetTextAsync(vm.Report);
      vm.MarkCopied();
    }
    catch {
    }
  }

  private void OnCloseClick(object? sender, RoutedEventArgs e) =>
    Close();
}
