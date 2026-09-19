using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI.Windows;


public partial class ErrorWindow : Window {
  public ErrorWindow() {
    InitializeComponent();
  }

  private async void OnCopyClick(object? sender, RoutedEventArgs e) {
    if (DataContext is not ErrorReportViewModel vm)
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
