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
  private static readonly DataFormat<string> FolderDragFormat =
    DataFormat.CreateInProcessFormat<string>("postclient-folder");
  private ConfigurationFileService? _files;
  private LayoutPersistence? _layout;
  private Window? _accountWindow;
  private NativeWebView? _readingWeb;
  private Point _dragOrigin;
  private MessageRowViewModel? _dragRow;
  private FolderNodeViewModel? _dragFolder;
  private PointerPressedEventArgs? _dragPress;
  private bool _dragging;
  private bool _syncingGridSelection;
  private bool _syncingFolderSelection;

  public MainWindow() {
    InitializeComponent();
    MessagesGrid.AddHandler(
      InputElement.PointerPressedEvent,
      OnMessagesPointerPressed,
      RoutingStrategies.Tunnel,
      true);
    MessagesGrid.AddHandler(
      InputElement.PointerMovedEvent,
      OnMessagesPointerMoved,
      RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
      true);
    MessagesGrid.AddHandler(
      InputElement.PointerReleasedEvent,
      OnMessagesPointerReleased,
      RoutingStrategies.Bubble,
      true);
    FolderTree.AddHandler(
      InputElement.PointerPressedEvent,
      OnFolderTreePointerPressed,
      RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
      true);
    FolderTree.AddHandler(
      InputElement.PointerMovedEvent,
      OnFolderTreePointerMoved,
      RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
      true);
    MessagesGrid.AddHandler(InputElement.KeyDownEvent, OnMessagesKeyDown, RoutingStrategies.Tunnel);
    FolderTree.AddHandler(InputElement.KeyDownEvent, OnFolderTreeKeyDown, RoutingStrategies.Tunnel);
  }

  public MainWindow(MainViewModel viewModel, ConfigurationFileService files) : this() {
    DataContext = viewModel;
    _files = files;
    _layout = new LayoutPersistence(this, files, viewModel, ApplyMailLayout);
    viewModel.ComposeRequested += OnComposeRequested;
    viewModel.MessageWindowRequested += OnMessageWindowRequested;
    viewModel.AccountSettingsRequested += OnAccountSettingsRequested;
    viewModel.FeaturesRequested += OnFeaturesRequested;
    viewModel.SemanticSearchRequested += OnSemanticSearchRequested;
    viewModel.MessageListSelectionRestoreRequested += RestoreMessageGridSelection;
    viewModel.FolderTreeSelectionRestoreRequested += RestoreFolderTreeSelection;
    viewModel.RulesRequested += OnRulesRequested;
    viewModel.AboutRequested += OnAboutRequested;
    viewModel.LogsRequested += OnLogsRequested;
    viewModel.AccountSaved += OnAccountSaved;
    viewModel.ExportArchiveRequested += OnExportArchive;
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
    viewModel.PropertyChanged += OnViewModelPropertyChanged;
    Program.WebViewFailed += _ => viewModel.DisableHtmlEngine();
    ApplyMailLayout();
    ApplyColumnHeaders();
    Opened += (_, _) => {
      _layout.Attach();
      if (DataContext is MainViewModel ready)
        ready.StartBackgroundWork();
      LoadReadingHtml();
      RestoreFolderTreeSelection();
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
    if (e.PropertyName is nameof(MainViewModel.Copy)
        or nameof(MainViewModel.UseDeliveryColumn)
        or nameof(MainViewModel.UseTypeColumn))
      ApplyColumnHeaders();
  }

  private void ApplyColumnHeaders() {
    if (DataContext is not MainViewModel vm)
      return;
    var copy = vm.Copy;
    foreach (var column in MessagesGrid.Columns) {
      column.Header = (column.Tag as string) switch {
        "Unread" => HeaderGlyph("●", copy.Unread),
        "Flag" => HeaderGlyph("★", copy.Flag),
        "Priority" => HeaderGlyph("!", copy.Priority),
        "Attachments" => HeaderGlyph("📎", copy.Attachments),
        "Delivery" => HeaderGlyph("✓", copy.Delivery),
        "Type" => copy.Type,
        "From" => copy.From,
        "Label" => copy.Label,
        "Subject" => copy.Subject,
        "Date" => copy.Date,
        _ => column.Header
      };
      column.IsVisible = (column.Tag as string) switch {
        "Delivery" => vm.UseDeliveryColumn,
        "Type" => vm.UseTypeColumn,
        _ => true
      };
    }
  }

  private static TextBlock HeaderGlyph(string glyph, string tip) {
    var block = new TextBlock {
      Text = glyph,
      HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
      VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
    };
    ToolTip.SetTip(block, tip);
    return block;
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
      DataContext = new SemanticSearchViewModel(_files, vm.SemanticSearch),
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

  private void OnFolderTreeSelectionChanged(object? sender, SelectionChangedEventArgs e) {
    if (_syncingFolderSelection || DataContext is not MainViewModel vm)
      return;
    vm.SetSelectedFolders(
      FolderTree.SelectedItems.OfType<FolderNodeViewModel>(),
      FolderTree.SelectedItem as FolderNodeViewModel);
  }

  private void RestoreFolderTreeSelection() {
    if (DataContext is not MainViewModel vm)
      return;
    _syncingFolderSelection = true;
    try {
      FolderTree.SelectedItems.Clear();
      foreach (var node in vm.SelectedFolderNodes)
        FolderTree.SelectedItems.Add(node);
      if (vm.SelectedFolderNode is not null
          && !FolderTree.SelectedItems.Contains(vm.SelectedFolderNode))
        FolderTree.SelectedItems.Add(vm.SelectedFolderNode);
    }
    finally {
      _syncingFolderSelection = false;
    }
  }

  private void OnFolderTreeContextRequested(object? sender, ContextRequestedEventArgs e) {
    if (DataContext is not MainViewModel vm)
      return;
    var current = e.Source as Visual;
    while (current is not null) {
      if (current is Control { DataContext: FolderNodeViewModel node }) {
        if (!vm.SelectedFolderNodes.Contains(node))
          vm.SetSelectedFolders([node], node);
        else
          vm.RefreshFolderMenu();
        RestoreFolderTreeSelection();
        return;
      }

      current = current.GetVisualParent();
    }
  }

  private void OnMessagesSelectionChanged(object? sender, SelectionChangedEventArgs e) {
    if (_syncingGridSelection)
      return;
    SyncSelectionFromGrid();
  }

  private void RestoreMessageGridSelection() {
    if (DataContext is not MainViewModel vm)
      return;
    _syncingGridSelection = true;
    try {
      MessagesGrid.SelectedItems.Clear();
      if (vm.SelectedMessage is not null)
        MessagesGrid.SelectedItems.Add(vm.SelectedMessage);
      foreach (var row in vm.SelectedMessages) {
        if (!MessagesGrid.SelectedItems.Contains(row))
          MessagesGrid.SelectedItems.Add(row);
      }
    }
    finally {
      _syncingGridSelection = false;
    }
  }

  private void SyncSelectionFromGrid() {
    if (DataContext is not MainViewModel vm)
      return;
    vm.SetSelectedMessages(MessagesGrid.SelectedItems.OfType<MessageRowViewModel>());
  }

  private void OnMessagesPointerReleased(object? sender, PointerReleasedEventArgs e) {
    if (_dragging || _syncingGridSelection)
      return;
    SyncSelectionFromGrid();
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
    _dragFolder = null;
    _dragPress = null;
    if (e.Source is Button)
      return;
    if (DataContext is not MainViewModel vm || vm.SelectedFolder is null)
      return;
    if (HasSelectionModifier(e.KeyModifiers))
      return;
    if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
      return;
    var row = RowAt(e.Source);
    if (row is null)
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
    if (DataContext is not MainViewModel vm || vm.SelectedMailbox is null || vm.SelectedFolder is null) {
      _dragRow = null;
      _dragPress = null;
      return;
    }

    _dragging = true;
    var ids = vm.DragMessageIds(_dragRow);
    if (ids.Count == 0) {
      _dragging = false;
      _dragRow = null;
      _dragPress = null;
      return;
    }
    var transfer = new DataTransfer();
    transfer.Add(DataTransferItem.Create(
      MessageDragFormat,
      MailDragPayload.PackMessages(vm.SelectedMailbox.Id, vm.SelectedFolder.FullName, ids)));
    try {
      await DragDrop.DoDragDropAsync(_dragPress, transfer, DragDropEffects.Move | DragDropEffects.Copy);
    }
    finally {
      _dragging = false;
      _dragRow = null;
      _dragPress = null;
      SyncSelectionFromGrid();
      MessagesGrid.Focus();
    }
  }

  private void OnFolderTreePointerPressed(object? sender, PointerPressedEventArgs e) {
    _dragRow = null;
    if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
      return;
    if (e.Source is Button)
      return;
    if (HasSelectionModifier(e.KeyModifiers))
      return;
    var node = FolderNodeAt(e.Source);
    if (node?.Mailbox is null || node.Folder is null || node.IsAccount)
      return;
    if (!MailFolderRole.IsCustom(node.Folder.Name, node.Folder.FullName))
      return;
    _dragOrigin = e.GetPosition(this);
    _dragFolder = node;
    _dragPress = e;
  }

  private async void OnFolderTreePointerMoved(object? sender, PointerEventArgs e) {
    if (_dragFolder is null || _dragging || _dragPress is null)
      return;
    if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) {
      _dragFolder = null;
      _dragPress = null;
      return;
    }

    var point = e.GetPosition(this);
    if (Math.Abs(point.X - _dragOrigin.X) < 8 && Math.Abs(point.Y - _dragOrigin.Y) < 8)
      return;
    if (_dragFolder.Mailbox is null || _dragFolder.Folder is null) {
      _dragFolder = null;
      _dragPress = null;
      return;
    }

    _dragging = true;
    var folders = DataContext is MainViewModel vm
      ? vm.DragFolderNames(_dragFolder)
      : [_dragFolder.Folder.FullName];
    if (folders.Count == 0) {
      _dragging = false;
      _dragFolder = null;
      _dragPress = null;
      return;
    }

    var transfer = new DataTransfer();
    transfer.Add(DataTransferItem.Create(
      FolderDragFormat,
      MailDragPayload.PackFolders(_dragFolder.Mailbox.Id, folders)));
    try {
      await DragDrop.DoDragDropAsync(_dragPress, transfer, DragDropEffects.Move | DragDropEffects.Copy);
    }
    finally {
      _dragging = false;
      _dragFolder = null;
      _dragPress = null;
    }
  }

  private void OnFolderDragOver(object? sender, DragEventArgs e) {
    e.DragEffects = CanDrop(e, out _) ? DragDropEffects.Move : DragDropEffects.None;
    e.Handled = true;
  }

  private async void OnFolderDrop(object? sender, DragEventArgs e) {
    e.Handled = true;
    if (DataContext is not MainViewModel vm)
      return;
    var dest = DropNodeAt(e);
    if (dest?.Mailbox is null)
      return;
    var data = e.DataTransfer;
    if (data is null)
      return;
    if (data.Contains(MessageDragFormat)) {
      var packed = data.TryGetValue(MessageDragFormat);
      if (!MailDragPayload.TryUnpackMessages(packed, out var mailboxId, out var from, out var ids))
        return;
      if (!vm.CanDropMessagesOn(dest, mailboxId, from)) {
        if (dest.Mailbox.Id.Equals(mailboxId, StringComparison.OrdinalIgnoreCase)
            && dest.Folder is not null
            && dest.Folder.FullName.Equals(from, StringComparison.OrdinalIgnoreCase))
          vm.Status = vm.Copy.AlreadyInFolder;
        return;
      }

      await vm.DropMessagesAsync(dest, mailboxId, from, ids);
      return;
    }

    if (data.Contains(FolderDragFormat)) {
      var packed = data.TryGetValue(FolderDragFormat);
      if (!MailDragPayload.TryUnpackFolders(packed, out var mailboxId, out var folders))
        return;
      var sources = folders.Select(folder => (mailboxId, folder)).ToList();
      if (sources.All(source => !vm.CanDropFolderOn(dest, source.mailboxId, source.folder)))
        return;
      await vm.DropFoldersAsync(dest, sources);
    }
  }

  private bool CanDrop(DragEventArgs e, out FolderNodeViewModel? dest) {
    dest = DropNodeAt(e);
    if (DataContext is not MainViewModel vm || dest?.Mailbox is null || e.DataTransfer is null)
      return false;
    if (e.DataTransfer.Contains(MessageDragFormat)) {
      var packed = e.DataTransfer.TryGetValue(MessageDragFormat);
      return MailDragPayload.TryUnpackMessages(packed, out var mailboxId, out var from, out _)
        && vm.CanDropMessagesOn(dest, mailboxId, from);
    }

    if (e.DataTransfer.Contains(FolderDragFormat)) {
      var packed = e.DataTransfer.TryGetValue(FolderDragFormat);
      var node = dest;
      return MailDragPayload.TryUnpackFolders(packed, out var mailboxId, out var folders)
        && folders.Any(folder => vm.CanDropFolderOn(node, mailboxId, folder));
    }

    return false;
  }

  private void OnMessageMenuOpening(object? sender, EventArgs e) {
    if (sender is not MenuFlyout flyout || DataContext is not MainViewModel vm)
      return;
    SyncSelectionFromGrid();
    MenuItem? move = null;
    foreach (var item in flyout.Items) {
      if (item is MenuItem menu && string.Equals(menu.Header as string, vm.Copy.MoveTo, StringComparison.Ordinal))
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
    var result = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
      Title = "Save attachment",
      SuggestedFileName = file.Name
    });
    var path = result?.TryGetLocalPath();
    if (string.IsNullOrWhiteSpace(path))
      return;
    await File.WriteAllBytesAsync(path, file.Bytes);
  }

  private void OnWindowKeyDown(object? sender, KeyEventArgs e) {
    if (e.Handled || e.Key != Key.Delete || IsTextInputTarget())
      return;
    if (FolderTree.IsKeyboardFocusWithin || MessagesGrid.IsKeyboardFocusWithin)
      return;
    TryDeleteMessages(e);
  }

  private void OnMessagesKeyDown(object? sender, KeyEventArgs e) {
    if (e.Handled || e.Key != Key.Delete || IsTextInputTarget())
      return;
    TryDeleteMessages(e);
  }

  private void OnFolderTreeKeyDown(object? sender, KeyEventArgs e) {
    if (e.Handled || e.Key != Key.Delete || IsTextInputTarget())
      return;
    if (DataContext is not MainViewModel vm || !vm.DeleteCustomFolderCommand.CanExecute(null))
      return;
    vm.DeleteCustomFolderCommand.Execute(null);
    e.Handled = true;
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

  private static bool HasSelectionModifier(KeyModifiers modifiers) =>
    modifiers.HasFlag(KeyModifiers.Control)
    || modifiers.HasFlag(KeyModifiers.Meta)
    || modifiers.HasFlag(KeyModifiers.Shift);

  private static MessageRowViewModel? RowAt(object? source) {
    for (var current = source as Control; current is not null; current = current.Parent as Control) {
      if (current.DataContext is MessageRowViewModel row)
        return row;
    }

    return null;
  }

  private FolderNodeViewModel? DropNodeAt(DragEventArgs e) {
    var point = e.GetPosition(FolderTree);
    FolderNodeViewModel? folder = null;
    FolderNodeViewModel? account = null;
    var folderArea = double.PositiveInfinity;
    var accountArea = double.PositiveInfinity;
    foreach (var item in FolderTree.GetVisualDescendants().OfType<TreeViewItem>()) {
      if (!item.IsVisible || item.DataContext is not FolderNodeViewModel node)
        continue;
      if (item.TranslatePoint(new Point(0, 0), FolderTree) is not { } origin)
        continue;
      var rect = new Rect(origin, item.Bounds.Size);
      if (!rect.Contains(point))
        continue;
      var area = rect.Width * rect.Height;
      if (node.Folder is not null) {
        if (area >= folderArea)
          continue;
        folderArea = area;
        folder = node;
        continue;
      }

      if (area >= accountArea)
        continue;
      accountArea = area;
      account = node;
    }

    return folder
      ?? account
      ?? FolderNodeAt(e.Source)
      ?? FolderNodeAt(FolderTree.InputHitTest(point));
  }

  private static FolderNodeViewModel? FolderNodeAt(object? source) {
    for (var current = source as Control; current is not null; current = current.Parent as Control) {
      if (current.DataContext is FolderNodeViewModel node)
        return node;
    }

    return null;
  }
}
