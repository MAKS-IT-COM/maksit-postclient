using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI.Windows;


public partial class ComposeWindow : Window {
  public ComposeWindow() {
    InitializeComponent();
  }

  public ComposeWindow(ComposeViewModel viewModel) : this() {
    DataContext = viewModel;
    viewModel.Sent += (_, _, _) => Close();
    Closed += (_, _) => viewModel.Detach();
  }

  private void OnCancelClick(object? sender, RoutedEventArgs e) =>
    Close();

  private void OnRemoveFileClick(object? sender, RoutedEventArgs e) {
    if (DataContext is ComposeViewModel vm && sender is Button { DataContext: ComposeFileRow row })
      vm.RemoveFileCommand.Execute(row);
  }

  private async void OnAttachClick(object? sender, RoutedEventArgs e) {
    if (DataContext is not ComposeViewModel vm)
      return;
    var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
      Title = vm.Copy.Attach,
      AllowMultiple = true,
      FileTypeFilter = [FilePickerFileTypes.All]
    });
    foreach (var file in files) {
      var path = file.TryGetLocalPath();
      if (string.IsNullOrWhiteSpace(path))
        continue;
      var error = vm.AddFile(path);
      if (error is not null)
        vm.Status = error;
    }
  }
}
