using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI.Views.Mail;


public partial class FolderPaneView : UserControl {
  private MainViewModel? _model;
  private Point _dragOrigin;
  private FolderNodeViewModel? _dragFolder;
  private PointerPressedEventArgs? _dragPress;
  private bool _dragging;
  private bool _syncingSelection;

  public FolderPaneView() {
    InitializeComponent();
    FolderTree.AddHandler(
      InputElement.PointerPressedEvent,
      OnPointerPressed,
      RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
      true);
    FolderTree.AddHandler(
      InputElement.PointerMovedEvent,
      OnPointerMoved,
      RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
      true);
    FolderTree.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    DataContextChanged += (_, _) => Hook(DataContext as MainViewModel);
    Loaded += (_, _) => RestoreSelection();
    Hook(DataContext as MainViewModel);
  }

  private void Hook(MainViewModel? model) {
    if (ReferenceEquals(_model, model))
      return;

    if (_model is not null)
      _model.FolderTreeSelectionRestoreRequested -= RestoreSelection;

    _model = model;

    if (_model is null)
      return;

    _model.FolderTreeSelectionRestoreRequested += RestoreSelection;
  }

  private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) {
    if (_syncingSelection || DataContext is not MainViewModel vm)
      return;

    vm.SetSelectedFolders(
      FolderTree.SelectedItems.OfType<FolderNodeViewModel>(),
      FolderTree.SelectedItem as FolderNodeViewModel);
  }

  private void RestoreSelection() {
    if (DataContext is not MainViewModel vm)
      return;

    _syncingSelection = true;

    try {
      FolderTree.SelectedItems.Clear();

      foreach (var node in vm.SelectedFolderNodes)
        FolderTree.SelectedItems.Add(node);

      if (vm.SelectedFolderNode is not null
          && !FolderTree.SelectedItems.Contains(vm.SelectedFolderNode))
        FolderTree.SelectedItems.Add(vm.SelectedFolderNode);
    }
    finally {
      _syncingSelection = false;
    }
  }

  private void OnContextRequested(object? sender, ContextRequestedEventArgs e) {
    if (DataContext is not MainViewModel vm)
      return;

    var current = e.Source as Visual;

    while (current is not null) {
      if (current is Control { DataContext: FolderNodeViewModel node }) {
        if (!vm.SelectedFolderNodes.Contains(node))
          vm.SetSelectedFolders([node], node);
        else
          vm.RefreshFolderMenu();

        RestoreSelection();

        return;
      }

      current = current.GetVisualParent();
    }
  }

  private void OnPointerPressed(object? sender, PointerPressedEventArgs e) {
    if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
      return;

    if (e.Source is Button)
      return;

    if (MailDrag.HasSelectionModifier(e.KeyModifiers))
      return;

    var node = NodeAt(e.Source);

    if (node?.Mailbox is null || node.Folder is null || node.IsAccount)
      return;

    if (!MailFolderRole.IsCustom(node.Folder.Name, node.Folder.FullName))
      return;

    _dragOrigin = e.GetPosition(this);
    _dragFolder = node;
    _dragPress = e;
  }

  private async void OnPointerMoved(object? sender, PointerEventArgs e) {
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
      : new List<string> { _dragFolder.Folder.FullName };

    if (folders.Count == 0) {
      _dragging = false;
      _dragFolder = null;
      _dragPress = null;

      return;
    }

    var transfer = new DataTransfer();
    transfer.Add(DataTransferItem.Create(
      MailDrag.Folders,
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

  private void OnDragOver(object? sender, DragEventArgs e) {
    e.DragEffects = CanDrop(e, out _) ? DragDropEffects.Move : DragDropEffects.None;
    e.Handled = true;
  }

  private async void OnDrop(object? sender, DragEventArgs e) {
    e.Handled = true;

    if (DataContext is not MainViewModel vm)
      return;

    var dest = DropNodeAt(e);

    if (dest?.Mailbox is null)
      return;

    var data = e.DataTransfer;

    if (data is null)
      return;

    if (data.Contains(MailDrag.Messages)) {
      var packed = data.TryGetValue(MailDrag.Messages);

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

    if (data.Contains(MailDrag.Folders)) {
      var packed = data.TryGetValue(MailDrag.Folders);

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

    if (e.DataTransfer.Contains(MailDrag.Messages)) {
      var packed = e.DataTransfer.TryGetValue(MailDrag.Messages);

      return MailDragPayload.TryUnpackMessages(packed, out var mailboxId, out var from, out _)
        && vm.CanDropMessagesOn(dest, mailboxId, from);
    }

    if (e.DataTransfer.Contains(MailDrag.Folders)) {
      var packed = e.DataTransfer.TryGetValue(MailDrag.Folders);
      var node = dest;

      return MailDragPayload.TryUnpackFolders(packed, out var mailboxId, out var folders)
        && folders.Any(folder => vm.CanDropFolderOn(node, mailboxId, folder));
    }

    return false;
  }

  private void OnKeyDown(object? sender, KeyEventArgs e) {
    if (e.Handled || e.Key != Key.Delete || IsTextInput())
      return;

    if (DataContext is not MainViewModel vm || !vm.DeleteCustomFolderCommand.CanExecute(null))
      return;

    vm.DeleteCustomFolderCommand.Execute(null);
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
      ?? NodeAt(e.Source)
      ?? NodeAt(FolderTree.InputHitTest(point));
  }

  private static FolderNodeViewModel? NodeAt(object? source) {
    for (var current = source as Control; current is not null; current = current.Parent as Control) {
      if (current.DataContext is FolderNodeViewModel node)
        return node;
    }

    return null;
  }
}
