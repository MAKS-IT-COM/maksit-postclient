using System.ComponentModel;
using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI.Views.Mail;


public partial class MessageListView : UserControl {
  private MainViewModel? _model;
  private Point _dragOrigin;
  private MessageRowViewModel? _dragRow;
  private PointerPressedEventArgs? _dragPress;
  private bool _dragging;
  private bool _syncingSelection;

  public MessageListView() {
    InitializeComponent();
    MessagesGrid.AddHandler(
      InputElement.PointerPressedEvent,
      OnPointerPressed,
      RoutingStrategies.Tunnel,
      true);
    MessagesGrid.AddHandler(
      InputElement.PointerMovedEvent,
      OnPointerMoved,
      RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
      true);
    MessagesGrid.AddHandler(
      InputElement.PointerReleasedEvent,
      OnPointerReleased,
      RoutingStrategies.Bubble,
      true);
    MessagesGrid.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    DataContextChanged += (_, _) => Hook(DataContext as MainViewModel);
    Hook(DataContext as MainViewModel);
  }

  private void Hook(MainViewModel? model) {
    if (ReferenceEquals(_model, model))
      return;

    if (_model is not null) {
      _model.PropertyChanged -= OnModelPropertyChanged;
      _model.MessageListSelectionRestoreRequested -= RestoreSelection;
    }

    _model = model;

    if (_model is null)
      return;

    _model.PropertyChanged += OnModelPropertyChanged;
    _model.MessageListSelectionRestoreRequested += RestoreSelection;
    ApplyColumnHeaders();
  }

  private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e) {
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
        "Spam" => HeaderGlyph("⚠", copy.MarkSpam),
        "SpamHint" => HeaderGlyph("?", copy.SpamHint),
        "Flag" => HeaderGlyph("★", copy.Flag),
        "Priority" => HeaderGlyph("!", copy.Priority),
        "Attachments" => HeaderGlyph("📎", copy.Attachments),
        "Delivery" => HeaderGlyph("✓", copy.Delivery),
        "Type" => copy.Type,
        "From" => copy.From,
        "Label" => copy.Label,
        "Subject" => copy.Subject,
        "Date" => copy.Date,
        "Size" => copy.Size,
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

  private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) {
    if (_syncingSelection || DataContext is MainViewModel { IsSyncingMessageList: true })
      return;

    SyncSelectionFromGrid();
  }

  private void RestoreSelection() {
    if (DataContext is not MainViewModel vm)
      return;

    _syncingSelection = true;

    try {
      MessagesGrid.SelectedItems.Clear();

      foreach (var row in vm.VisibleSelectedMessages())
        MessagesGrid.SelectedItems.Add(row);
    }
    finally {
      _syncingSelection = false;
    }
  }

  private void SyncSelectionFromGrid() {
    if (DataContext is not MainViewModel vm)
      return;

    vm.SetSelectedMessages(LiveGridRows());
  }

  private List<MessageRowViewModel> LiveGridRows() {
    if (DataContext is not MainViewModel vm)
      return [];

    var keys = MessagesGrid.SelectedItems.OfType<MessageRowViewModel>().Select(row => row.Key);

    return MailMessageList.Resolve(keys, vm.VisibleMessages, row => row.Key).ToList();
  }

  private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) {
    if (_dragging || _syncingSelection || DataContext is MainViewModel { IsSyncingMessageList: true })
      return;

    SyncSelectionFromGrid();
  }

  private void OnToggleFlagClick(object? sender, RoutedEventArgs e) {
    if (DataContext is MainViewModel vm && sender is Button { DataContext: MessageRowViewModel row })
      vm.ToggleFlagCommand.Execute(row);
  }

  private void OnDoubleTapped(object? sender, TappedEventArgs e) {
    if (e.Source is Button)
      return;

    var row = RowAt(e.Source);

    if (DataContext is MainViewModel vm && row is not null)
      vm.OpenMessageWindowCommand.Execute(row);
  }

  private void OnPointerPressed(object? sender, PointerPressedEventArgs e) {
    _dragging = false;
    _dragRow = null;
    _dragPress = null;

    if (e.Source is Button)
      return;

    if (DataContext is not MainViewModel vm || vm.SelectedFolder is null)
      return;

    if (MailDrag.HasSelectionModifier(e.KeyModifiers))
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

  private async void OnPointerMoved(object? sender, PointerEventArgs e) {
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
      MailDrag.Messages,
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

  private void OnContextRequested(object? sender, ContextRequestedEventArgs e) {
    if (DataContext is not MainViewModel vm)
      return;

    var row = RowAt(e.Source);

    if (row is null)
      return;

    var live = LiveGridRows();

    if (live.Any(item => item.Key == row.Key))
      vm.SetSelectedMessages(live);
    else
      vm.SetSelectedMessages([row]);

    RestoreSelection();
  }

  private void OnMenuOpening(object? sender, EventArgs e) {
    if (sender is not MenuFlyout flyout || DataContext is not MainViewModel vm)
      return;

    var live = LiveGridRows();

    if (live.Count > 0)
      vm.SetSelectedMessages(live);
    else {
      vm.SetSelectedMessages(vm.VisibleSelectedMessages());
      RestoreSelection();
    }

    vm.RefreshMessageMenu();
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

  private void OnKeyDown(object? sender, KeyEventArgs e) {
    if (e.Handled || e.Key != Key.Delete || IsTextInput())
      return;

    if (DataContext is not MainViewModel vm || !vm.DeleteMessagesCommand.CanExecute(null))
      return;

    vm.DeleteMessagesCommand.Execute(null);
    e.Handled = true;
  }

  private bool IsTextInput() {
    var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();

    for (var current = focused as Visual; current is not null; current = current.GetVisualParent()) {
      if (current is TextBox or ComboBox)
        return true;
    }

    return false;
  }

  private static MessageRowViewModel? RowAt(object? source) {
    for (var current = source as Visual; current is not null; current = current.GetVisualParent()) {
      if (current is Control { DataContext: MessageRowViewModel row })
        return row;
    }

    return null;
  }
}
