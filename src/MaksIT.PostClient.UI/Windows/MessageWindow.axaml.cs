using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MaksIT.PostClient.Client;
using MaksIT.PostClient.Shared;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI.Windows;


public partial class MessageWindow : Window {
  private NativeWebView? _readingWeb;

  public MessageWindow() {
    InitializeComponent();
  }

  public MessageWindow(MessageWindowViewModel viewModel) : this() {
    DataContext = viewModel;
    viewModel.PropertyChanged += OnViewModelPropertyChanged;
    viewModel.SaveAttachmentsZipRequested += OnSaveAttachmentsZip;
    Opened += (_, _) => LoadReadingHtml();
    Closed += (_, _) => viewModel.Detach();
  }

  private void OnCloseClick(object? sender, RoutedEventArgs e) =>
    Close();

  private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) {
    if (e.PropertyName is nameof(MessageWindowViewModel.ReadingHtmlDocument)
        or nameof(MessageWindowViewModel.ShowHtmlBody)
        or nameof(MessageWindowViewModel.HtmlEngineReady))
      LoadReadingHtml();
  }

  private bool EnsureReadingWeb() {
    if (_readingWeb is not null)
      return true;
    if (DataContext is not MessageWindowViewModel vm || !vm.HtmlEngineReady)
      return false;
    try {
      var web = new NativeWebView();
      web.EnvironmentRequested += (_, args) => WebViewSetup.Apply(args);
      web.NavigationStarted += WebViewSetup.OpenExternalLink;
      web.Bind(IsVisibleProperty, new Binding(nameof(MessageWindowViewModel.ShowHtmlBody)));
      ReadingHtmlHost.Children.Add(web);
      _readingWeb = web;
      return true;
    }
    catch {
      if (DataContext is MessageWindowViewModel model)
        model.DisableHtmlEngine();
      return false;
    }
  }

  private void LoadReadingHtml() {
    if (DataContext is not MessageWindowViewModel vm || !vm.ShowHtmlBody)
      return;
    if (!EnsureReadingWeb() || _readingWeb is null)
      return;
    try {
      WebViewSetup.NavigateHtml(_readingWeb, vm.ReadingHtmlDocument);
    }
    catch {
      vm.DisableHtmlEngine();
    }
  }

  private void OnOpenAttachment(object? sender, RoutedEventArgs e) {
    if (sender is not Button { DataContext: MailFileAttachment file })
      return;
    try {
      var dir = Path.Combine(Path.GetTempPath(), "postclient-open");
      Directory.CreateDirectory(dir);
      var name = file.Name;
      foreach (var ch in Path.GetInvalidFileNameChars())
        name = name.Replace(ch, '_');
      if (string.IsNullOrWhiteSpace(name))
        name = "attachment";
      var path = Path.Combine(dir, name);
      File.WriteAllBytes(path, file.Bytes);
      Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
    catch {
    }
  }

  private async void OnSaveAttachment(object? sender, RoutedEventArgs e) {
    if (sender is not Button { DataContext: MailFileAttachment file })
      return;
    var result = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
      Title = DataContext is MessageWindowViewModel model ? model.Copy.Save : UiLocale.Copy.Save,
      SuggestedFileName = file.Name
    });
    var path = result?.TryGetLocalPath();
    if (string.IsNullOrWhiteSpace(path))
      return;
    await File.WriteAllBytesAsync(path, file.Bytes);
  }

  private async void OnSaveAttachmentsZip(byte[] zip, string name) {
    var suggested = name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? name : name + ".zip";
    var title = DataContext is MessageWindowViewModel model
      ? model.Copy.SaveAttachmentsZip
      : UiLocale.Copy.SaveAttachmentsZip;
    var result = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
      Title = title,
      SuggestedFileName = suggested,
      DefaultExtension = "zip",
      FileTypeChoices = [
        new FilePickerFileType("ZIP file") { Patterns = ["*.zip"] }
      ]
    });
    var path = result?.TryGetLocalPath();
    if (string.IsNullOrWhiteSpace(path))
      return;
    await File.WriteAllBytesAsync(path, zip);
  }
}
