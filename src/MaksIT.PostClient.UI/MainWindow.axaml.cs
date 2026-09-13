using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Controls.ApplicationLifetimes;
using MaksIT.PostClient.Client;
using MaksIT.PostClient.Shared;
using MaksIT.PostClient.UI.Windows;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI;


public partial class MainWindow : Window {
  private static readonly DataFormat<string> MessageDragFormat =
    DataFormat.CreateInProcessFormat<string>("postclient-message-ids");
  private ConfigurationFileService? _files;
  private LayoutPersistence? _layout;
  private Window? _accountWindow;
  private NativeWebView? _readingWeb;
  private Point _dragOrigin;
  private MessageRowViewModel? _dragRow;
  private PointerPressedEventArgs? _dragPress;
  private bool _dragging;

  public MainWindow() {
    InitializeComponent();
    MessagesGrid.AddHandler(
      InputElement.PointerPressedEvent,
      OnMessagesPointerPressed,
      RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
      true);
    MessagesGrid.AddHandler(
      InputElement.PointerMovedEvent,
      OnMessagesPointerMoved,
      RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
      true);
  }

  public MainWindow(MainViewModel viewModel, ConfigurationFileService files) : this() {
    DataContext = viewModel;
    _files = files;
    _layout = new LayoutPersistence(this, files, viewModel, ApplyMailLayout);
    viewModel.ComposeRequested += OnComposeRequested;
    viewModel.MessageWindowRequested += OnMessageWindowRequested;
    viewModel.AccountSettingsRequested += OnAccountSettingsRequested;
    viewModel.AccountSaved += OnAccountSaved;
    viewModel.ExportArchiveRequested += OnExportArchive;
    viewModel.ExportFascicoloRequested += OnExportFascicolo;
    viewModel.ImportEmlRequested += OnImportEml;
    viewModel.ImportPstRequested += OnImportPst;
    viewModel.ImportThunderbirdRequested += OnImportThunderbird;
    viewModel.PrintHtmlRequested += OnPrintHtml;
    viewModel.SavePdfRequested += OnSavePdf;
    viewModel.SaveAttachmentsZipRequested += OnSaveAttachmentsZip;
    viewModel.NoticeRequested += OnNotice;
    viewModel.PromptRequested += OnPromptRequested;
    viewModel.PropertyChanged += OnViewModelPropertyChanged;
    Program.WebViewFailed += _ => viewModel.DisableHtmlEngine();
    ApplyMailLayout();
    Opened += (_, _) => {
      _layout.Attach();
      LoadReadingHtml();
    };
  }

  private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) {
    if (e.PropertyName is nameof(MainViewModel.ReadingHtmlDocument)
        or nameof(MainViewModel.ShowHtmlBody)
        or nameof(MainViewModel.HasReading)
        or nameof(MainViewModel.HtmlEngineReady))
      LoadReadingHtml();
    if (e.PropertyName is nameof(MainViewModel.ReadingLayout)
        or nameof(MainViewModel.IsWideLayout))
      ApplyMailLayout();
  }

  private void ApplyMailLayout() {
    if (DataContext is not MainViewModel vm)
      return;
    using var _ = _layout?.SuspendSave();
    var layout = _files?.Current.Layout ?? new LayoutSettings();
    layout.Normalize();
    var folder = layout.ResolvedFolderWidth();
    if (vm.IsWideLayout) {
      var list = layout.ResolvedListWidth();
      MailGrid.ColumnDefinitions = ColumnDefinitions.Parse(
        string.Create(CultureInfo.InvariantCulture, $"{folder:0.##},4,{list:0.##},4,*"));
      MailGrid.ColumnDefinitions[0].MinWidth = 160;
      MailGrid.ColumnDefinitions[2].MinWidth = 220;
      MailGrid.ColumnDefinitions[4].MinWidth = 280;
      MailGrid.RowDefinitions = RowDefinitions.Parse("*");
      Grid.SetRowSpan(FolderPane, 1);
      Grid.SetRowSpan(FolderSplitter, 1);
      Grid.SetColumn(ListPane, 2);
      Grid.SetRow(ListPane, 0);
      StackedSplitter.IsVisible = false;
      WideSplitter.IsVisible = true;
      Grid.SetColumn(ReadingPane, 4);
      Grid.SetRow(ReadingPane, 0);
    }
    else {
      MailGrid.ColumnDefinitions = ColumnDefinitions.Parse(
        string.Create(CultureInfo.InvariantCulture, $"{folder:0.##},4,*"));
      MailGrid.ColumnDefinitions[0].MinWidth = 160;
      MailGrid.ColumnDefinitions[2].MinWidth = 280;
      if (layout.ListHeight >= 120) {
        var listHeight = layout.ResolvedListHeight();
        MailGrid.RowDefinitions = RowDefinitions.Parse(
          string.Create(CultureInfo.InvariantCulture, $"{listHeight:0.##},4,*"));
      }
      else {
        MailGrid.RowDefinitions = RowDefinitions.Parse("*,4,1.15*");
      }
      MailGrid.RowDefinitions[0].MinHeight = 120;
      MailGrid.RowDefinitions[2].MinHeight = 160;
      Grid.SetRowSpan(FolderPane, 3);
      Grid.SetRowSpan(FolderSplitter, 3);
      Grid.SetColumn(ListPane, 2);
      Grid.SetRow(ListPane, 0);
      StackedSplitter.IsVisible = true;
      WideSplitter.IsVisible = false;
      Grid.SetColumn(ReadingPane, 2);
      Grid.SetRow(ReadingPane, 2);
    }
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

  private void OnFolderTreeSelectionChanged(object? sender, SelectionChangedEventArgs e) {
    if (DataContext is MainViewModel vm && sender is TreeView { SelectedItem: FolderNodeViewModel node })
      vm.SelectedFolderNode = node;
  }

  private void OnFolderTreeContextRequested(object? sender, ContextRequestedEventArgs e) {
    if (DataContext is not MainViewModel vm)
      return;
    var current = e.Source as Visual;
    while (current is not null) {
      if (current is Control { DataContext: FolderNodeViewModel node } && !node.IsAccount) {
        vm.SelectedFolderNode = node;
        return;
      }

      current = current.GetVisualParent();
    }
  }

  private void OnMessagesSelectionChanged(object? sender, SelectionChangedEventArgs e) {
    if (DataContext is MainViewModel vm && sender is DataGrid grid)
      vm.SetSelectedMessages(grid.SelectedItems.OfType<MessageRowViewModel>());
  }

  private void OnToggleFlagClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm && sender is Button { DataContext: MessageRowViewModel row })
      vm.ToggleFlagCommand.Execute(row);
  }

  private void OnMessagesDoubleTapped(object? sender, TappedEventArgs e) {
    if (e.Source is Button)
      return;
    var row = RowAt(e.Source);
    if (DataContext is MainViewModel vm && row is not null)
      vm.OpenMessageWindowCommand.Execute(row);
  }

  private void OnMessagesPointerPressed(object? sender, PointerPressedEventArgs e) {
    _dragging = false;
    _dragRow = null;
    _dragPress = null;
    if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
      return;
    if (e.Source is Button)
      return;
    if (DataContext is not MainViewModel vm || vm.SelectedFolder is null)
      return;
    var row = RowAt(e.Source);
    if (row is null)
      return;
    if (!vm.SelectedMessages.Contains(row) && !ReferenceEquals(vm.SelectedMessage, row))
      return;
    _dragOrigin = e.GetPosition(this);
    _dragRow = row;
    _dragPress = e;
  }

  private async void OnMessagesPointerMoved(object? sender, PointerEventArgs e) {
    if (_dragRow is null || _dragging || _dragPress is null)
      return;
    if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) {
      _dragRow = null;
      _dragPress = null;
      return;
    }

    var point = e.GetPosition(this);
    if (Math.Abs(point.X - _dragOrigin.X) < 8 && Math.Abs(point.Y - _dragOrigin.Y) < 8)
      return;
    if (DataContext is not MainViewModel vm || vm.SelectedFolder is null) {
      _dragRow = null;
      _dragPress = null;
      return;
    }

    _dragging = true;
    var ids = vm.SelectedMessages.Select(r => r.Header.Id).ToList();
    if (ids.Count == 0)
      ids.Add(_dragRow.Header.Id);
    var transfer = new DataTransfer();
    transfer.Add(DataTransferItem.Create(
      MessageDragFormat,
      vm.SelectedFolder.FullName + "\n" + string.Join(",", ids)));
    try {
      await DragDrop.DoDragDropAsync(_dragPress, transfer, DragDropEffects.Move);
    }
    finally {
      _dragging = false;
      _dragRow = null;
      _dragPress = null;
    }
  }

  private void OnFolderDragOver(object? sender, DragEventArgs e) {
    e.DragEffects = CanDrop(e, out _) ? DragDropEffects.Move : DragDropEffects.None;
    e.Handled = true;
  }

  private async void OnFolderDrop(object? sender, DragEventArgs e) {
    e.Handled = true;
    if (DataContext is not MainViewModel vm || !CanDrop(e, out var dest) || dest is null)
      return;
    var packed = e.DataTransfer?.TryGetValue(MessageDragFormat);
    if (!TryUnpack(packed, out var from, out var ids))
      return;
    await vm.MoveDroppedAsync(dest.FullName, ids, from);
  }

  private bool CanDrop(DragEventArgs e, out FolderRowViewModel? dest) {
    dest = null;
    if (DataContext is not MainViewModel vm)
      return false;
    if (e.DataTransfer is null || !e.DataTransfer.Contains(MessageDragFormat))
      return false;
    var node = FolderNodeAt(e.Source);
    if (node?.Folder is null)
      return false;
    dest = node.Folder;
    if (node.Mailbox is null || vm.SelectedMailbox is null
        || !node.Mailbox.Id.Equals(vm.SelectedMailbox.Id, StringComparison.OrdinalIgnoreCase))
      return false;
    return vm.SelectedFolder is null
      || !dest.FullName.Equals(vm.SelectedFolder.FullName, StringComparison.OrdinalIgnoreCase);
  }

  private void OnMessageMenuOpening(object? sender, EventArgs e) {
    if (sender is not MenuFlyout flyout || DataContext is not MainViewModel vm)
      return;
    MenuItem? move = null;
    foreach (var item in flyout.Items) {
      if (item is MenuItem { Header: "Move To" } menu)
        move = menu;
    }

    if (move is null)
      return;
    move.Items.Clear();
    foreach (var folder in vm.Folders) {
      move.Items.Add(new MenuItem {
        Header = folder.Line,
        Command = vm.MoveToFolderCommand,
        CommandParameter = folder
      });
    }
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
    var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
      Title = "Import Outlook PST",
      AllowMultiple = false,
      FileTypeFilter = [
        new FilePickerFileType("Outlook PST") { Patterns = ["*.pst", "*.ost"] }
      ]
    });
    var path = files.FirstOrDefault()?.TryGetLocalPath();
    if (string.IsNullOrWhiteSpace(path) || DataContext is not MainViewModel vm)
      return;
    await vm.ImportPstPathAsync(path);
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
    return PromptWindow.ShowAsync(this, request, vm.Copy.Ok, vm.Copy.Close);
  }

  private async Task<string?> PickFolder(string title) {
    var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
      Title = title,
      AllowMultiple = false
    });
    return folders.FirstOrDefault()?.TryGetLocalPath();
  }

  protected override void OnClosing(WindowClosingEventArgs e) {
    if (e.IsProgrammatic)
      return;
    e.Cancel = true;
    Hide();
  }

  private void OnExitClick(object? sender, RoutedEventArgs e) {
    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime life)
      life.Shutdown();
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
    var result = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
      Title = "Save attachment",
      SuggestedFileName = file.Name
    });
    var path = result?.TryGetLocalPath();
    if (string.IsNullOrWhiteSpace(path))
      return;
    await File.WriteAllBytesAsync(path, file.Bytes);
  }

  private static MessageRowViewModel? RowAt(object? source) {
    for (var current = source as Control; current is not null; current = current.Parent as Control) {
      if (current.DataContext is MessageRowViewModel row)
        return row;
    }

    return null;
  }

  private static FolderNodeViewModel? FolderNodeAt(object? source) {
    for (var current = source as Control; current is not null; current = current.Parent as Control) {
      if (current.DataContext is FolderNodeViewModel node)
        return node;
    }

    return null;
  }

  private static bool TryUnpack(string? packed, out string folder, out List<uint> ids) {
    folder = "";
    ids = [];
    if (string.IsNullOrWhiteSpace(packed))
      return false;
    var parts = packed.Split('\n', 2);
    folder = parts[0];
    if (parts.Length < 2)
      return false;
    foreach (var token in parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
      if (uint.TryParse(token, out var id))
        ids.Add(id);
    }

    return ids.Count > 0;
  }
}
