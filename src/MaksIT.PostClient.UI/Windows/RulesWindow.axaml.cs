using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI.Windows;


public partial class RulesWindow : Window {
  public RulesWindow() {
    InitializeComponent();
    Opened += async (_, _) => {
      if (DataContext is RulesViewModel vm)
        await vm.PrepareAsync();
    };
    Closed += (_, _) => {
      if (DataContext is RulesViewModel vm)
        vm.Persist();
    };
  }

  private void OnCloseClick(object? sender, RoutedEventArgs e) =>
    Close();

  private async void OnImportClick(object? sender, RoutedEventArgs e) {
    if (DataContext is not RulesViewModel vm)
      return;
    var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
      Title = vm.Copy.ImportOutlookRules,
      AllowMultiple = false,
      FileTypeFilter = [
        new FilePickerFileType(vm.Copy.ImportRulesJson) { Patterns = ["*.json"] },
        new FilePickerFileType(vm.Copy.ImportRulesOutlook) { Patterns = ["*.rwz"] },
        FilePickerFileTypes.All
      ]
    });
    var path = files.FirstOrDefault()?.TryGetLocalPath();
    if (string.IsNullOrWhiteSpace(path))
      return;
    vm.Import(path);
  }

  private async void OnExportClick(object? sender, RoutedEventArgs e) {
    if (DataContext is not RulesViewModel vm)
      return;
    var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
      Title = vm.Copy.ExportRules,
      SuggestedFileName = "postclient-rules.json",
      DefaultExtension = "json",
      FileTypeChoices = [
        new FilePickerFileType("JSON") { Patterns = ["*.json"] }
      ]
    });
    var path = file?.TryGetLocalPath();
    if (string.IsNullOrWhiteSpace(path))
      return;
    vm.Export(path);
  }
}
