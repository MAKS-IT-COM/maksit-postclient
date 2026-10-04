using System.Diagnostics;
using System.ComponentModel;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI.Views.Mail;


public partial class ReadingPaneView : UserControl {
  private MainViewModel? _model;
  private NativeWebView? _readingWeb;

  public ReadingPaneView() {
    InitializeComponent();
    DataContextChanged += (_, _) => Hook(DataContext as MainViewModel);
    Loaded += (_, _) => LoadReadingHtml();
    Hook(DataContext as MainViewModel);
  }

  private void Hook(MainViewModel? model) {
    if (ReferenceEquals(_model, model))
      return;

    if (_model is not null)
      _model.PropertyChanged -= OnModelPropertyChanged;

    _model = model;

    if (_model is null)
      return;

    _model.PropertyChanged += OnModelPropertyChanged;
    LoadReadingHtml();
  }

  private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e) {
    if (e.PropertyName is nameof(MainViewModel.ReadingHtmlDocument)
        or nameof(MainViewModel.ShowHtmlBody)
        or nameof(MainViewModel.HasReading)
        or nameof(MainViewModel.HtmlEngineReady))
      LoadReadingHtml();
  }

  private bool EnsureReadingWeb() {
    if (_readingWeb is not null)
      return true;

    if (DataContext is not MainViewModel vm || !vm.HtmlEngineReady)
      return false;

    try {
      var web = new NativeWebView();
      web.EnvironmentRequested += (_, args) => WebViewSetup.Apply(args);
      web.NavigationStarted += WebViewSetup.OpenExternalLink;
      web.Bind(IsVisibleProperty, new Binding(nameof(MainViewModel.ShowHtmlBody)));
      ReadingHtmlHost.Children.Add(web);
      _readingWeb = web;

      return true;
    }
    catch (Exception) {
      if (DataContext is MainViewModel model)
        model.DisableHtmlEngine();

      return false;
    }
  }

  private void LoadReadingHtml() {
    if (DataContext is not MainViewModel vm || !vm.ShowHtmlBody)
      return;

    if (!EnsureReadingWeb() || _readingWeb is null)
      return;

    var html = string.IsNullOrWhiteSpace(vm.ReadingHtmlDocument)
      ? MessageHtml.Document("")
      : vm.ReadingHtmlDocument;

    try {
      WebViewSetup.NavigateHtml(_readingWeb, html);
    }
    catch {
      vm.DisableHtmlEngine();
    }
  }

  private void OnViewFatturaPa(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm)
      vm.ViewFatturaPaCommand.Execute(null);
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
    catch (Exception ex) {
      if (DataContext is MainViewModel vm)
        vm.Status = ex.Message;
    }
  }

  private async void OnSaveAttachment(object? sender, RoutedEventArgs e) {
    if (sender is not Button { DataContext: MailFileAttachment file })
      return;

    if (TopLevel.GetTopLevel(this) is not { } top)
      return;

    var result = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
      Title = "Save attachment",
      SuggestedFileName = file.Name
    });
    var path = result?.TryGetLocalPath();

    if (string.IsNullOrWhiteSpace(path))
      return;

    await File.WriteAllBytesAsync(path, file.Bytes);
  }
}
