using Avalonia.Controls;
using Avalonia.Input;
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
    viewModel.ZipOptionsRequested += prompt => ZipSendWindow.ShowAsync(this, prompt);
    Closed += (_, _) => viewModel.Detach();
  }

  private void OnCancelClick(object? sender, RoutedEventArgs e) =>
    Close();

  private void OnRemoveFileClick(object? sender, RoutedEventArgs e) {
    if (DataContext is ComposeViewModel vm && sender is Button { DataContext: ComposeFileRow row })
      vm.RemoveFileCommand.Execute(row);
  }

  private void OnAttachDragOver(object? sender, DragEventArgs e) {
    e.DragEffects = HasFileDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
    e.Handled = true;
  }

  private void OnAttachDrop(object? sender, DragEventArgs e) {
    e.Handled = true;
    if (DataContext is not ComposeViewModel vm)
      return;
    foreach (var path in DroppedFiles(e)) {
      var error = vm.AddFile(path);
      if (error is not null)
        vm.Status = error;
    }
  }

  private static bool HasFileDrop(DragEventArgs e) =>
    e.DataTransfer?.Contains(DataFormat.File) == true;

  private static IEnumerable<string> DroppedFiles(DragEventArgs e) {
    var items = e.DataTransfer?.TryGetFiles();
    if (items is null)
      yield break;
    foreach (var item in items) {
      if (item is IStorageFolder)
        continue;
      var path = item.TryGetLocalPath();
      if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        yield return path;
    }
  }
}
