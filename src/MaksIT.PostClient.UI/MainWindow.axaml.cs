using System.Diagnostics;
using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Controls.ApplicationLifetimes;
using MaksIT.PostClient.UI.Windows;
using MaksIT.PostClient.UI.ViewModels;
using MaksIT.PostClient.UI.Views.Mail;


namespace MaksIT.PostClient.UI;


public partial class MainWindow : Window {
  private ConfigurationFileService? _files;
  private LayoutPersistence? _layout;
  private Window? _accountWindow;

  public MainWindow() =>
    InitializeComponent();

  public MainWindow(MainViewModel viewModel, ConfigurationFileService files) : this() {
    DataContext = viewModel;
    _files = files;
    _layout = new LayoutPersistence(this, files);
    viewModel.ComposeRequested += OnComposeRequested;
    viewModel.MessageWindowRequested += OnMessageWindowRequested;
    viewModel.AccountSettingsRequested += OnAccountSettingsRequested;
    viewModel.FeaturesRequested += OnFeaturesRequested;
    viewModel.SemanticSearchRequested += OnSemanticSearchRequested;
    viewModel.RulesRequested += OnRulesRequested;
    viewModel.AboutRequested += OnAboutRequested;
    viewModel.LogsRequested += OnLogsRequested;
    viewModel.AccountSaved += OnAccountSaved;
    viewModel.ExportArchiveRequested += OnExportArchive;
    viewModel.PickBundleRequested += OnPickBundle;
    viewModel.ExportFascicoloRequested += OnExportFascicolo;
    viewModel.ImportEmlRequested += OnImportEml;
    viewModel.ImportPstRequested += OnImportPst;
    viewModel.AttachPstRequested += OnAttachPst;
    viewModel.CreatePstRequested += OnCreatePst;
    viewModel.PickStorePathRequested += OnPickStorePath;
    viewModel.MoveStorePathRequested += OnMoveStorePath;
    viewModel.RetentionRequested += OnRetentionRequested;
    viewModel.IdentityHubSignInRequested += OnIdentityHubSignIn;
    viewModel.ImportThunderbirdRequested += OnImportThunderbird;
    viewModel.PrintHtmlRequested += OnPrintHtml;
    viewModel.SavePdfRequested += OnSavePdf;
    viewModel.SaveAttachmentsZipRequested += OnSaveAttachmentsZip;
    viewModel.NoticeRequested += OnNotice;
    viewModel.PromptRequested += OnPromptRequested;
    Program.WebViewFailed += _ => viewModel.DisableHtmlEngine();
    _layout.Attach();
    Opened += (_, _) => {
      if (DataContext is MainViewModel ready) {
        ready.StartBackgroundWork();
        ready.RestoreFolderTreeSelection();
      }
    };
  }

  private void OnMessageWindowRequested(MessageWindowViewModel message) {
    var window = new MessageWindow(message) {
      WindowStartupLocation = WindowStartupLocation.CenterOwner
    };
    window.Show(this);
  }

  private void OnComposeRequested(ComposeViewModel compose) {
    var window = new ComposeWindow(compose) {
      WindowStartupLocation = WindowStartupLocation.CenterOwner
    };
    window.Show(this);
  }

  private async void OnAccountSettingsRequested() {
    if (DataContext is not MainViewModel vm)
      return;
    if (_accountWindow is not null) {
      _accountWindow.Activate();
      return;
    }

    vm.AccountSettingsOpen = true;
    _accountWindow = new AccountSettingsWindow {
      DataContext = vm,
      WindowStartupLocation = WindowStartupLocation.CenterOwner
    };
    try {
      await _accountWindow.ShowDialog(this);
    }
    finally {
      _accountWindow = null;
      vm.AccountSettingsOpen = false;
      if (vm.SelectedMailbox is not null)
        vm.ConnectCommand.Execute(null);
    }
  }

  private void OnAccountSaved() =>
    _accountWindow?.Close();

  private async void OnFeaturesRequested() {
    if (DataContext is not MainViewModel vm || _files is null)
      return;
    var window = new FeatureSettingsWindow {
      DataContext = new FeatureMatrixViewModel(_files, vm.NotifyFeatures),
      WindowStartupLocation = WindowStartupLocation.CenterOwner
    };
    await window.ShowDialog(this);
  }

  private async void OnSemanticSearchRequested() {
    if (DataContext is not MainViewModel vm || _files is null)
      return;
    var window = new SemanticSearchWindow {
      DataContext = new SemanticSearchViewModel(
        _files,
        vm.SemanticSearch,
        vm.SpamExamples,
        vm.DeleteSpamExample,
        vm.NotSpamExampleAsync),
      WindowStartupLocation = WindowStartupLocation.CenterOwner
    };
    await window.ShowDialog(this);
  }

  private async void OnRulesRequested() {
    if (_files is null || DataContext is not MainViewModel vm)
      return;
    var window = new RulesWindow {
      DataContext = new RulesViewModel(
        _files,
        vm.Mailboxes,
        vm.FoldersForRules,
        vm.EnsureFoldersForRulesAsync,
        vm.SelectedMailbox?.Id,
        vm.RunAllRulesAsync),
      WindowStartupLocation = WindowStartupLocation.CenterOwner
    };
    await window.ShowDialog(this);
  }

  private async Task<string?> OnPickBundle(bool save) {
    if (DataContext is not MainViewModel vm)
      return null;
    var kind = new FilePickerFileType(vm.Copy.EasyMigration) { Patterns = ["*.postbundle"] };
    Activate();
    await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
    if (save) {
      var result = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
        Title = vm.Copy.CreateBundle,
        DefaultExtension = "postbundle",
        SuggestedFileName = "Postclient-" + DateTime.Now.ToString("yyyyMMdd") + ".postbundle",
        FileTypeChoices = [kind]
      });
      return result?.TryGetLocalPath();
    }

    var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
      Title = vm.Copy.OpenBundle,
      AllowMultiple = false,
      FileTypeFilter = [kind, FilePickerFileTypes.All]
    });
    if (files.Count == 0)
      return null;
    return files[0].TryGetLocalPath();
  }

  private async void OnExportArchive() {
    var folder = await PickFolder("Export archive");
    if (folder is null || DataContext is not MainViewModel vm)
      return;
    vm.ExportArchiveTo(folder);
  }

  private async void OnExportFascicolo() {
    var folder = await PickFolder("Export EML fascicolo");
    if (folder is null || DataContext is not MainViewModel vm)
      return;
    await vm.ExportFascicoloToAsync(folder);
  }

  private async void OnImportEml() {
    var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
      Title = "Import EML",
      AllowMultiple = true,
      FileTypeFilter = [
        new FilePickerFileType("Email") { Patterns = ["*.eml"] },
        FilePickerFileTypes.All
      ]
    });
    if (files.Count == 0 || DataContext is not MainViewModel vm)
      return;
    var paths = files.Select(f => f.TryGetLocalPath()).Where(p => !string.IsNullOrWhiteSpace(p)).Cast<string>().ToList();
    await vm.ImportEmlPathsAsync(paths);
  }

  private async void OnImportPst() {
    var path = await PickPstFile(DataContext is MainViewModel vm ? vm.Copy.ImportPst : "Import Outlook PST");
    if (string.IsNullOrWhiteSpace(path) || DataContext is not MainViewModel main)
      return;
    var proposed = AppPaths.ProposedStoreDirectory(path);
    Directory.CreateDirectory(AppPaths.StoresDirectory());
    var dest = await PickFolder(main.Copy.CreateStore, proposed);
    await main.ImportPstPathAsync(path, dest);
  }

  private async void OnAttachPst() {
    if (DataContext is not MainViewModel main)
      return;
    var path = await PickFolder(main.Copy.AttachStore);
    if (!string.IsNullOrWhiteSpace(path))
      await main.AttachStorePathAsync(path);
  }

  private async void OnCreatePst() {
    if (DataContext is not MainViewModel main)
      return;
    var proposed = AppPaths.ProposedStoreDirectory("Mail");
    Directory.CreateDirectory(AppPaths.StoresDirectory());
    var path = await PickFolder(main.Copy.CreateStore, proposed);
    if (!string.IsNullOrWhiteSpace(path))
      await main.CreateStorePathAsync(path);
  }

  private Task<string?> OnPickStorePath() =>
    PickFolder(DataContext is MainViewModel vm ? vm.Copy.AttachStore : "Store");

  private Task<string?> OnMoveStorePath() =>
    PickFolder(DataContext is MainViewModel vm ? vm.Copy.MoveStore : "Move store");

  private async void OnRetentionRequested() {
    if (_files is null || DataContext is not MainViewModel vm)
      return;
    foreach (var box in vm.Mailboxes)
      await vm.EnsureFoldersForRulesAsync(box.Id);
    var window = new RetentionWindow {
      DataContext = new RetentionViewModel(
        _files,
        vm.Mailboxes,
        vm.FoldersForRules,
        vm.RunRetentionAsync),
      WindowStartupLocation = WindowStartupLocation.CenterOwner
    };
    await window.ShowDialog(this);
    vm.RefreshRetentionBadges();
  }

  private async void OnIdentityHubSignIn(string kind) {
    if (DataContext is not MainViewModel vm)
      return;
    try {
      var window = new IdentityHubLoginWindow(vm.HubLoginUrl(kind)) {
        WindowStartupLocation = WindowStartupLocation.CenterOwner
      };
      var json = await window.ShowDialog<string?>(_accountWindow ?? this);
      if (string.IsNullOrWhiteSpace(json))
        vm.CancelIdentityHubSignIn("Identity Hub sign-in cancelled.");
      else
        await vm.CompleteIdentityHubSignIn(kind, json);
    }
    catch (Exception ex) {
      vm.CancelIdentityHubSignIn(ex.Message);
    }
  }

  private async void OnImportThunderbird() {
    if (DataContext is MainViewModel vm)
      await vm.ImportThunderbirdAsync();
  }

  private void OnPrintHtml(string path) {
    try {
      Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
    catch (Exception ex) {
      if (DataContext is MainViewModel vm)
        vm.Status = ex.Message;
    }
  }

  private async void OnSavePdf(byte[] pdf, string name) {
    await SaveBytesAsync("Save PDF", name, "pdf", pdf);
  }

  private async void OnSaveAttachmentsZip(byte[] zip, string name) {
    await SaveBytesAsync("Save attachments ZIP", name, "zip", zip);
  }

  private async Task SaveBytesAsync(string title, string name, string extension, byte[] bytes) {
    var suffix = "." + extension;
    var suggested = name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? name : name + suffix;
    var result = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
      Title = title,
      SuggestedFileName = suggested,
      DefaultExtension = extension,
      FileTypeChoices = [
        new FilePickerFileType(extension.ToUpperInvariant() + " file") { Patterns = ["*" + suffix] }
      ]
    });
    var path = result?.TryGetLocalPath();
    if (string.IsNullOrWhiteSpace(path))
      return;
    await File.WriteAllBytesAsync(path, bytes);
  }

  private void OnNotice(string title, string body) {
    DesktopNotice.Show(title, body);
    if (WindowState == WindowState.Minimized)
      WindowState = WindowState.Normal;
  }

  private Task<string?> OnPromptRequested(PromptRequest request) {
    if (DataContext is not MainViewModel vm)
      return Task.FromResult<string?>(null);
    return PromptWindow.ShowAsync(
      this,
      request,
      string.IsNullOrWhiteSpace(request.ConfirmLabel) ? vm.Copy.Ok : request.ConfirmLabel,
      vm.Copy.Close);
  }

  private void OnAboutRequested() =>
    _ = AboutWindow.ShowAsync(this);

  private void OnLogsRequested() =>
    _ = LogWindow.ShowAsync(this);

  private async Task<string?> PickFolder(string title, string? suggested = null) {
    IStorageFolder? start = null;
    if (!string.IsNullOrWhiteSpace(suggested)) {
      try {
        Directory.CreateDirectory(suggested);
        start = await StorageProvider.TryGetFolderFromPathAsync(suggested);
      }
      catch {
      }
    }

    var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
      Title = title,
      AllowMultiple = false,
      SuggestedStartLocation = start
    });
    return folders.FirstOrDefault()?.TryGetLocalPath() ?? suggested;
  }

  private async Task<string?> PickPstFile(string title) {
    var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
      Title = title,
      AllowMultiple = false,
      FileTypeFilter = [
        new FilePickerFileType("Outlook data file") { Patterns = ["*.pst", "*.ost"] }
      ]
    });
    return files.FirstOrDefault()?.TryGetLocalPath();
  }

  private void OnExitClick(object? sender, RoutedEventArgs e) {
    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime life)
      life.Shutdown();
  }

  private void OnWindowKeyDown(object? sender, KeyEventArgs e) {
    if (e.Handled || e.Key != Key.Delete || IsTextInputTarget())
      return;

    if (IsFolderOrMessageFocus())
      return;

    TryDeleteMessages(e);
  }

  private bool IsFolderOrMessageFocus() {
    var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();

    for (var current = focused as Visual; current is not null; current = current.GetVisualParent()) {
      if (current is FolderPaneView or MessageListView)
        return true;
    }

    return false;
  }

  private void TryDeleteMessages(KeyEventArgs e) {
    if (DataContext is not MainViewModel vm || !vm.DeleteMessagesCommand.CanExecute(null))
      return;
    vm.DeleteMessagesCommand.Execute(null);
    e.Handled = true;
  }

  private bool IsTextInputTarget() {
    var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
    for (var current = focused as Visual; current is not null; current = current.GetVisualParent()) {
      if (current is TextBox or ComboBox or NativeWebView)
        return true;
    }

    return false;
  }
}
