using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI.Layout;


internal sealed class LayoutPersistence {
  private readonly Window _window;
  private readonly ConfigurationFileService _configuration;
  private readonly MainViewModel _viewModel;
  private readonly Action _applyMailLayout;
  private readonly DispatcherTimer _saveTimer;
  private readonly Dictionary<DataGrid, PendingColumnSort> _pendingSorts = [];
  private DataGrid? _messages;
  private int _applyDepth;
  private int _restoreSortPending;
  private bool _attached;
  private string? _lastSaved;

  public LayoutPersistence(
    Window window,
    ConfigurationFileService configuration,
    MainViewModel viewModel,
    Action applyMailLayout) {
    _window = window;
    _configuration = configuration;
    _viewModel = viewModel;
    _applyMailLayout = applyMailLayout;
    _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
    _saveTimer.Tick += (_, _) => {
      _saveTimer.Stop();
      SaveNow();
    };
  }

  public void Attach() {
    if (_attached)
      return;

    _attached = true;
    _messages = _window.FindControl<DataGrid>("MessagesGrid");
    Track(_messages);
    Apply();
    TrackPane("MailGrid");
    TrackFolderTree();
    _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    _window.Resized += (_, _) => ScheduleSave();
    _window.PositionChanged += (_, _) => ScheduleSave();
    _window.PropertyChanged += (_, e) => {
      if (e.Property.Name == nameof(Window.WindowState))
        ScheduleSave();
    };
    _window.Closing += (_, _) => {
      _saveTimer.Stop();
      SaveNow();
    };
  }

  public IDisposable SuspendSave() {
    _applyDepth++;
    return new ApplyScope(this);
  }

  public void ScheduleSave() {
    if (_applyDepth > 0 || _restoreSortPending > 0)
      return;
    _saveTimer.Stop();
    _saveTimer.Start();
  }

  public void SaveNow() {
    if (_applyDepth > 0)
      return;

    var cfg = _configuration.Current;
    cfg.EnsureDefaults();
    var layout = cfg.Layout;
    CaptureWindow(layout);
    CapturePanes(layout);
    layout.MessageFilter = _viewModel.MessageFilter ?? "";
    if (_messages is not null) {
      var widths = ReadColumnWidths(_messages);
      if (widths.Count > 0)
        layout.ColumnWidths = widths;
      var order = ReadColumnOrder(_messages);
      if (order.Count > 0)
        layout.ColumnOrder = order;
    }

    layout.CollapsedFolders = CaptureCollapsed(layout);

    var snapshot = JsonSnapshot(layout);
    if (snapshot == _lastSaved)
      return;

    _configuration.Save(cfg);
    _lastSaved = snapshot;
  }

  private void Apply() {
    using (SuspendSave()) {
      var layout = _configuration.Current.Layout;
      ApplyWindow(layout);
      _applyMailLayout();
      ApplyFilter(layout);
      if (_messages is not null)
        ApplyColumnState(_messages, layout);
    }
  }

  private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) {
    if (e.PropertyName == nameof(MainViewModel.MessageFilter))
      ScheduleSave();
  }

  private void TrackPane(string gridName) {
    var grid = _window.FindControl<Grid>(gridName);
    if (grid is not null)
      grid.LayoutUpdated += (_, _) => ScheduleSave();
  }

  private void TrackFolderTree() {
    var tree = _window.FindControl<TreeView>("FolderTree");
    if (tree is null)
      return;
    tree.AddHandler(TreeViewItem.ExpandedEvent, OnFolderExpandChanged, RoutingStrategies.Bubble);
    tree.AddHandler(TreeViewItem.CollapsedEvent, OnFolderExpandChanged, RoutingStrategies.Bubble);
  }

  private void OnFolderExpandChanged(object? sender, RoutedEventArgs e) =>
    ScheduleSave();

  private void Track(DataGrid? grid) {
    if (grid is null)
      return;
    grid.LayoutUpdated += (_, _) => {
      TryApplyPendingSort(grid);
      ScheduleSave();
    };
    grid.Sorting += (_, e) => OnSorting(grid, e);
  }

  private void ApplyWindow(LayoutSettings layout) {
    var width = LayoutSettings.Clamp(layout.WindowWidth, _window.MinWidth, 10000, LayoutSettings.DefaultWindowWidth);
    var height = LayoutSettings.Clamp(layout.WindowHeight, _window.MinHeight, 10000, LayoutSettings.DefaultWindowHeight);
    _window.Width = width;
    _window.Height = height;

    if (layout.WindowX is int x && layout.WindowY is int y && IsOnScreen(x, y))
      _window.Position = new PixelPoint(x, y);

    if (Enum.TryParse<WindowState>(layout.WindowState, true, out var state) && state != WindowState.Minimized)
      _window.WindowState = state;
  }

  private void CaptureWindow(LayoutSettings layout) {
    if (_window.WindowState == WindowState.Normal) {
      layout.WindowWidth = _window.Width;
      layout.WindowHeight = _window.Height;
      layout.WindowX = _window.Position.X;
      layout.WindowY = _window.Position.Y;
    }

    layout.WindowState = _window.WindowState == WindowState.Minimized
      ? WindowState.Normal.ToString()
      : _window.WindowState.ToString();
  }

  private void CapturePanes(LayoutSettings layout) {
    var folder = PaneSize("FolderPane", true, 0);
    if (folder >= 160)
      layout.FolderWidth = folder;

    if (_viewModel.IsWideLayout) {
      var list = PaneSize("ListPane", true, 2);
      if (list >= 220)
        layout.ListWidth = list;
      return;
    }

    var listHeight = PaneSize("ListPane", false, 0);
    if (listHeight >= 120)
      layout.ListHeight = listHeight;
  }

  private double PaneSize(string paneName, bool horizontal, int index) {
    var pane = _window.FindControl<Control>(paneName);
    if (pane is { IsVisible: true }) {
      var bound = horizontal ? pane.Bounds.Width : pane.Bounds.Height;
      if (bound > 0)
        return bound;
    }

    var grid = _window.FindControl<Grid>("MailGrid");
    if (grid is null)
      return 0;
    if (horizontal) {
      if (index < 0 || index >= grid.ColumnDefinitions.Count)
        return 0;
      var definition = grid.ColumnDefinitions[index];
      return definition.Width.IsAbsolute ? definition.Width.Value : 0;
    }

    if (index < 0 || index >= grid.RowDefinitions.Count)
      return 0;
    var row = grid.RowDefinitions[index];
    return row.Height.IsAbsolute ? row.Height.Value : 0;
  }

  private void ApplyFilter(LayoutSettings layout) {
    var filter = layout.MessageFilter ?? "";
    if (!string.Equals(_viewModel.MessageFilter, filter, StringComparison.Ordinal))
      _viewModel.MessageFilter = filter;
  }

  private void ApplyColumnState(DataGrid grid, LayoutSettings layout) {
    ApplyColumnOrder(grid, layout);
    ApplyColumnWidths(grid, layout);
    ApplyColumnSort(grid, layout);
  }

  private static void ApplyColumnOrder(DataGrid grid, LayoutSettings layout) {
    var saved = layout.ColumnOrder;
    if (saved is null || saved.Count == 0)
      return;

    var index = 0;
    foreach (var key in saved) {
      var column = FindColumn(grid, key);
      if (column is null)
        continue;
      if (column.DisplayIndex != index)
        column.DisplayIndex = index;
      index++;
    }
  }

  private static void ApplyColumnWidths(DataGrid grid, LayoutSettings layout) {
    var saved = layout.ColumnWidths;
    if (saved is null || saved.Count == 0)
      return;

    foreach (var column in grid.Columns) {
      var header = ColumnKey(column);
      if (header is null || !saved.TryGetValue(header, out var width) || width < 8)
        continue;
      column.Width = new DataGridLength(width, DataGridLengthUnitType.Pixel);
    }
  }

  private void ApplyColumnSort(DataGrid grid, LayoutSettings layout) {
    var saved = layout.ColumnSort;
    if (saved is null || string.IsNullOrWhiteSpace(saved.Header)) {
      _pendingSorts.Remove(grid);
      return;
    }
    if (!Enum.TryParse<ListSortDirection>(saved.Direction, true, out var direction))
      direction = ListSortDirection.Ascending;

    _pendingSorts[grid] = new PendingColumnSort(saved.Header, direction);
    Dispatcher.UIThread.Post(() => TryApplyPendingSort(grid), DispatcherPriority.Loaded);
  }

  private void TryApplyPendingSort(DataGrid grid) {
    if (!_pendingSorts.TryGetValue(grid, out var pending))
      return;

    var column = FindColumn(grid, pending.Header);
    if (column is null) {
      _pendingSorts.Remove(grid);
      return;
    }

    if (!grid.IsAttachedToVisualTree() || !grid.IsEffectivelyVisible || column.ActualWidth <= 0)
      return;

    _pendingSorts.Remove(grid);
    _restoreSortPending++;
    _viewModel.ApplyListSort(pending.Header, pending.Direction == ListSortDirection.Descending);
    if (!_viewModel.GroupConversations)
      column.Sort(pending.Direction);
    Dispatcher.UIThread.Post(() => {
      if (_restoreSortPending > 0)
        _restoreSortPending--;
    }, DispatcherPriority.Background);
  }

  private void OnSorting(DataGrid grid, DataGridColumnEventArgs e) {
    PersistSort(grid, e.Column);
    var saved = _configuration.Current.Layout.ColumnSort;
    var header = saved?.Header ?? ColumnKey(e.Column) ?? "Date";
    var descending = saved is not null
      && string.Equals(saved.Direction, nameof(ListSortDirection.Descending), StringComparison.OrdinalIgnoreCase);
    _viewModel.ApplyListSort(header, descending);
    if (_viewModel.GroupConversations)
      e.Handled = true;
  }

  private static DataGridColumn? FindColumn(DataGrid grid, string header) {
    foreach (var candidate in grid.Columns) {
      if (string.Equals(ColumnKey(candidate), header, StringComparison.Ordinal))
        return candidate;
    }

    return null;
  }

  private void PersistSort(DataGrid grid, DataGridColumn column) {
    if (_applyDepth > 0 || _restoreSortPending > 0)
      return;
    var header = ColumnKey(column);
    if (header is null)
      return;

    var previous = _configuration.Current.Layout.ColumnSort;
    var direction = ListSortDirection.Ascending;
    if (previous is not null
        && string.Equals(previous.Header, header, StringComparison.Ordinal)
        && string.Equals(previous.Direction, nameof(ListSortDirection.Ascending), StringComparison.OrdinalIgnoreCase))
      direction = ListSortDirection.Descending;

    var cfg = _configuration.Current;
    cfg.EnsureDefaults();
    cfg.Layout.ColumnSort = new SavedColumnSort {
      Header = header,
      Direction = direction.ToString()
    };
    ScheduleSave();
  }

  private static Dictionary<string, double> ReadColumnWidths(DataGrid grid) {
    var widths = new Dictionary<string, double>(StringComparer.Ordinal);
    foreach (var column in grid.Columns) {
      var header = ColumnKey(column);
      var width = column.ActualWidth > 0 ? column.ActualWidth : column.Width.Value;
      if (header is null || width < 8)
        continue;
      widths[header] = width;
    }

    return widths;
  }

  private static List<string> ReadColumnOrder(DataGrid grid) {
    var order = new List<string>();
    foreach (var column in grid.Columns.OrderBy(c => c.DisplayIndex)) {
      var key = ColumnKey(column);
      if (key is null)
        continue;
      order.Add(key);
    }

    return order;
  }

  private List<string> CaptureCollapsed(LayoutSettings layout) {
    var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var collapsed = new List<string>();
    Walk(_viewModel.FolderTree);

    var mailboxes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var box in _configuration.Current.Mailboxes)
      mailboxes.Add(box.Id);

    foreach (var key in layout.CollapsedFolders) {
      if (string.IsNullOrWhiteSpace(key) || present.Contains(key))
        continue;
      var mailboxId = MailboxIdOf(key);
      if (!mailboxes.Contains(mailboxId) || loaded.Contains(mailboxId))
        continue;
      collapsed.Add(key);
    }

    return collapsed;

    void Walk(IEnumerable<FolderNodeViewModel> nodes) {
      foreach (var node in nodes) {
        var key = LayoutSettings.FolderTreeKey(node.Mailbox?.Id, node.Folder?.FullName);
        if (key.Length > 0) {
          present.Add(key);
          if (!node.IsExpanded)
            collapsed.Add(key);
        }

        if (node.IsAccount && node.Children.Count > 0 && node.Mailbox is { } box)
          loaded.Add(box.Id);
        Walk(node.Children);
      }
    }
  }

  private static string MailboxIdOf(string key) {
    var at = key.IndexOf('\t');
    return at < 0 ? key : key[..at];
  }

  private static string? ColumnKey(DataGridColumn column) =>
    column.Tag as string ?? column.Header as string ?? column.Header?.ToString();

  private bool IsOnScreen(int x, int y) {
    var screens = _window.Screens?.All;
    if (screens is null || screens.Count == 0)
      return true;
    return screens.Any(screen => screen.WorkingArea.Contains(new PixelPoint(x, y)));
  }

  private static string JsonSnapshot(LayoutSettings layout) =>
    System.Text.Json.JsonSerializer.Serialize(layout);

  private void ReleaseApply() {
    if (_applyDepth > 0)
      _applyDepth--;
  }

  private sealed record PendingColumnSort(string Header, ListSortDirection Direction);

  private sealed class ApplyScope(LayoutPersistence owner) : IDisposable {
    public void Dispose() => owner.ReleaseApply();
  }
}
