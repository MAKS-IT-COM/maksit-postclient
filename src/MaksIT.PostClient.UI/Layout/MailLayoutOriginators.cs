using System.Globalization;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI.Layout;


internal sealed class MailGridOriginator : ILayoutOriginator {
  private readonly Grid _grid;
  private readonly Func<LayoutSettings> _layout;
  private MainViewModel? _hooked;
  private bool _appliedOnce;

  public MailGridOriginator(Grid grid, Func<LayoutSettings> layout) {
    _grid = grid;
    _layout = layout;
  }

  public bool DeferSave => false;

  public bool ApplyFirst => true;

  public void Attach(Action changed) {
    _ = changed;
    Hook(_grid.DataContext as MainViewModel);
    _grid.DataContextChanged += (_, _) => Hook(_grid.DataContext as MainViewModel);
  }

  public void Apply(LayoutSettings layout) {
    _appliedOnce = true;
    var folderPane = Child(MailPaneRole.Folder);
    var folderSplitter = Child(MailPaneRole.FolderSplitter);
    var list = Child(MailPaneRole.List);
    var reading = Child(MailPaneRole.Reading);
    var stackedSplitter = Child(MailPaneRole.StackedSplitter);
    var wideSplitter = Child(MailPaneRole.WideSplitter);

    if (folderPane is null || folderSplitter is null || list is null || reading is null)
      return;

    layout.Normalize();
    var folder = layout.ResolvedFolderWidth();
    var wide = _grid.DataContext is MainViewModel { IsWideLayout: true };

    if (wide) {
      var listWidth = layout.ResolvedListWidth();
      _grid.ColumnDefinitions = ColumnDefinitions.Parse(
        string.Create(CultureInfo.InvariantCulture, $"{folder:0.##},4,{listWidth:0.##},4,*"));
      _grid.ColumnDefinitions[0].MinWidth = 160;
      _grid.ColumnDefinitions[2].MinWidth = 220;
      _grid.ColumnDefinitions[4].MinWidth = 280;
      _grid.RowDefinitions = RowDefinitions.Parse("*");
      Grid.SetRowSpan(folderPane, 1);
      Grid.SetRowSpan(folderSplitter, 1);
      Grid.SetColumn(list, 2);
      Grid.SetRow(list, 0);

      if (stackedSplitter is not null)
        stackedSplitter.IsVisible = false;

      if (wideSplitter is not null)
        wideSplitter.IsVisible = true;

      Grid.SetColumn(reading, 4);
      Grid.SetRow(reading, 0);

      return;
    }

    _grid.ColumnDefinitions = ColumnDefinitions.Parse(
      string.Create(CultureInfo.InvariantCulture, $"{folder:0.##},4,*"));
    _grid.ColumnDefinitions[0].MinWidth = 160;
    _grid.ColumnDefinitions[2].MinWidth = 280;

    if (layout.ListHeight >= 120) {
      var listHeight = layout.ResolvedListHeight();
      _grid.RowDefinitions = RowDefinitions.Parse(
        string.Create(CultureInfo.InvariantCulture, $"{listHeight:0.##},4,*"));
    }
    else {
      _grid.RowDefinitions = RowDefinitions.Parse("*,4,1.15*");
    }

    _grid.RowDefinitions[0].MinHeight = 120;
    _grid.RowDefinitions[2].MinHeight = 160;
    Grid.SetRowSpan(folderPane, 3);
    Grid.SetRowSpan(folderSplitter, 3);
    Grid.SetColumn(list, 2);
    Grid.SetRow(list, 0);

    if (stackedSplitter is not null)
      stackedSplitter.IsVisible = true;

    if (wideSplitter is not null)
      wideSplitter.IsVisible = false;

    Grid.SetColumn(reading, 2);
    Grid.SetRow(reading, 2);
  }

  public void Capture(LayoutSettings layout) {
  }

  private void Hook(MainViewModel? vm) {
    if (ReferenceEquals(_hooked, vm))
      return;

    if (_hooked is not null)
      _hooked.PropertyChanged -= OnViewModelPropertyChanged;

    _hooked = vm;

    if (_hooked is null)
      return;

    _hooked.PropertyChanged += OnViewModelPropertyChanged;

    if (!_appliedOnce)
      return;

    using (LayoutMemento.SuspendSave(_grid))
      Apply(_layout());
  }

  private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) {
    if (e.PropertyName is not (nameof(MainViewModel.ReadingLayout) or nameof(MainViewModel.IsWideLayout)))
      return;

    using (LayoutMemento.SuspendSave(_grid))
      Apply(_layout());
  }

  private Control? Child(MailPaneRole role) {
    foreach (var child in _grid.Children) {
      if (child is Control control && LayoutMemento.GetPaneRole(control) == role)
        return control;
    }

    return null;
  }
}

internal sealed class ListPaneOriginator : ILayoutOriginator {
  private readonly Control _view;

  public ListPaneOriginator(Control view) => _view = view;

  public bool DeferSave => false;

  public void Attach(Action changed) {
    if (_view.Parent is Grid grid) {
      grid.LayoutUpdated += (_, _) => changed();

      return;
    }

    _view.AttachedToLogicalTree += (_, _) => {
      if (_view.Parent is Grid parent)
        parent.LayoutUpdated += (_, _) => changed();
    };
  }

  public void Apply(LayoutSettings layout) {
    if (_view.Parent is not Grid grid)
      return;

    if (IsWide()) {
      var column = Grid.GetColumn(_view);

      if (column >= 0 && column < grid.ColumnDefinitions.Count)
        grid.ColumnDefinitions[column].Width = new GridLength(layout.ResolvedListWidth());

      return;
    }

    var row = Grid.GetRow(_view);

    if (row >= 0 && row < grid.RowDefinitions.Count)
      grid.RowDefinitions[row].Height = new GridLength(layout.ResolvedListHeight());
  }

  public void Capture(LayoutSettings layout) {
    if (IsWide()) {
      var width = Measure(horizontal: true);

      if (width >= 220)
        layout.ListWidth = width;

      return;
    }

    var height = Measure(horizontal: false);

    if (height >= 120)
      layout.ListHeight = height;
  }

  private bool IsWide() =>
    _view.DataContext is MainViewModel { IsWideLayout: true };

  private double Measure(bool horizontal) {
    if (_view.IsVisible) {
      var bound = horizontal ? _view.Bounds.Width : _view.Bounds.Height;

      if (bound > 0)
        return bound;
    }

    if (_view.Parent is not Grid grid)
      return 0;

    if (horizontal) {
      var column = Grid.GetColumn(_view);

      if (column < 0 || column >= grid.ColumnDefinitions.Count)
        return 0;

      var definition = grid.ColumnDefinitions[column];

      return definition.Width.IsAbsolute ? definition.Width.Value : 0;
    }

    var row = Grid.GetRow(_view);

    if (row < 0 || row >= grid.RowDefinitions.Count)
      return 0;

    var height = grid.RowDefinitions[row];

    return height.Height.IsAbsolute ? height.Height.Value : 0;
  }
}

internal sealed class MessageGridOriginator : ILayoutOriginator {
  private readonly DataGrid _grid;
  private readonly Func<LayoutSettings> _layout;
  private PendingColumnSort? _pending;
  private int _restoreSortPending;

  public MessageGridOriginator(DataGrid grid, Func<LayoutSettings> layout) {
    _grid = grid;
    _layout = layout;
  }

  public bool DeferSave => _restoreSortPending > 0;

  public void Attach(Action changed) {
    _grid.LayoutUpdated += (_, _) => {
      TryApplyPendingSort();
      changed();
    };
    _grid.Sorting += (_, e) => OnSorting(e, changed);
  }

  public void Apply(LayoutSettings layout) {
    ApplyColumnOrder(_grid, layout);
    ApplyColumnWidths(_grid, layout);
    ApplyColumnSort(layout);
  }

  public void Capture(LayoutSettings layout) {
    var widths = ReadColumnWidths(_grid);

    if (widths.Count > 0)
      layout.ColumnWidths = widths;

    var order = ReadColumnOrder(_grid);

    if (order.Count > 0)
      layout.ColumnOrder = order;
  }

  private void OnSorting(DataGridColumnEventArgs e, Action changed) {
    PersistSort(e.Column, changed);

    if (_grid.DataContext is not MainViewModel vm)
      return;

    var saved = _layout().ColumnSort;
    var header = saved?.Header ?? ColumnKey(e.Column) ?? "Date";
    var descending = saved is not null
      && string.Equals(saved.Direction, nameof(ListSortDirection.Descending), StringComparison.OrdinalIgnoreCase);
    vm.ApplyListSort(header, descending);

    if (vm.GroupConversations)
      e.Handled = true;
  }

  private void ApplyColumnSort(LayoutSettings layout) {
    var saved = layout.ColumnSort;

    if (saved is null || string.IsNullOrWhiteSpace(saved.Header)) {
      _pending = null;

      return;
    }

    if (!Enum.TryParse<ListSortDirection>(saved.Direction, true, out var direction))
      direction = ListSortDirection.Ascending;

    _pending = new PendingColumnSort(saved.Header, direction);
    Dispatcher.UIThread.Post(TryApplyPendingSort, DispatcherPriority.Loaded);
  }

  private void TryApplyPendingSort() {
    if (_pending is not { } pending)
      return;

    var column = FindColumn(_grid, pending.Header);

    if (column is null) {
      _pending = null;

      return;
    }

    if (!_grid.IsAttachedToVisualTree() || !_grid.IsEffectivelyVisible || column.ActualWidth <= 0)
      return;

    _pending = null;
    _restoreSortPending++;

    if (_grid.DataContext is MainViewModel vm) {
      vm.ApplyListSort(pending.Header, pending.Direction == ListSortDirection.Descending);

      if (!vm.GroupConversations)
        column.Sort(pending.Direction);
    }

    Dispatcher.UIThread.Post(() => {
      if (_restoreSortPending > 0)
        _restoreSortPending--;
    }, DispatcherPriority.Background);
  }

  private void PersistSort(DataGridColumn column, Action changed) {
    if (DeferSave)
      return;

    var header = ColumnKey(column);

    if (header is null)
      return;

    var layout = _layout();
    var previous = layout.ColumnSort;
    var direction = ListSortDirection.Ascending;

    if (previous is not null
        && string.Equals(previous.Header, header, StringComparison.Ordinal)
        && string.Equals(previous.Direction, nameof(ListSortDirection.Ascending), StringComparison.OrdinalIgnoreCase))
      direction = ListSortDirection.Descending;

    layout.ColumnSort = new SavedColumnSort {
      Header = header,
      Direction = direction.ToString()
    };
    changed();
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

  private static DataGridColumn? FindColumn(DataGrid grid, string header) {
    foreach (var candidate in grid.Columns) {
      if (string.Equals(ColumnKey(candidate), header, StringComparison.Ordinal))
        return candidate;
    }

    return null;
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

    foreach (var column in grid.Columns.OrderBy(item => item.DisplayIndex)) {
      var key = ColumnKey(column);

      if (key is null)
        continue;

      order.Add(key);
    }

    return order;
  }

  private static string? ColumnKey(DataGridColumn column) =>
    column.Tag as string ?? column.Header as string ?? column.Header?.ToString();

  private sealed record PendingColumnSort(string Header, ListSortDirection Direction);
}

internal sealed class FolderTreeOriginator : ILayoutOriginator {
  private readonly TreeView _tree;
  private readonly ConfigurationFileService _configuration;

  public FolderTreeOriginator(TreeView tree, ConfigurationFileService configuration) {
    _tree = tree;
    _configuration = configuration;
  }

  public bool DeferSave => false;

  public void Attach(Action changed) {
    _tree.AddHandler(TreeViewItem.ExpandedEvent, (_, _) => changed(), RoutingStrategies.Bubble);
    _tree.AddHandler(TreeViewItem.CollapsedEvent, (_, _) => changed(), RoutingStrategies.Bubble);
  }

  public void Apply(LayoutSettings layout) {
    if (_tree.DataContext is not MainViewModel vm)
      return;

    ApplyExpanded(vm.FolderTree, layout);
  }

  public void Capture(LayoutSettings layout) {
    if (_tree.DataContext is not MainViewModel vm)
      return;

    layout.CollapsedFolders = CaptureCollapsed(vm, layout);
  }

  private List<string> CaptureCollapsed(MainViewModel vm, LayoutSettings layout) {
    var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var collapsed = new List<string>();
    Walk(vm.FolderTree);

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

  private static void ApplyExpanded(IEnumerable<FolderNodeViewModel> nodes, LayoutSettings layout) {
    foreach (var node in nodes) {
      var expanded = layout.IsFolderExpanded(node.Mailbox?.Id, node.Folder?.FullName);

      if (node.IsExpanded != expanded)
        node.IsExpanded = expanded;

      ApplyExpanded(node.Children, layout);
    }
  }

  private static string MailboxIdOf(string key) {
    var at = key.IndexOf('\t');

    return at < 0 ? key : key[..at];
  }
}

internal sealed class FilterOriginator : ILayoutOriginator {
  private readonly TextBox _box;

  public FilterOriginator(TextBox box) => _box = box;

  public bool DeferSave => false;

  public void Attach(Action changed) {
    _box.PropertyChanged += (_, e) => {
      if (e.Property.Name == nameof(TextBox.Text))
        changed();
    };
  }

  public void Apply(LayoutSettings layout) {
    if (_box.DataContext is not MainViewModel vm)
      return;

    var filter = layout.MessageFilter ?? "";

    if (!string.Equals(vm.MessageFilter, filter, StringComparison.Ordinal))
      vm.MessageFilter = filter;
  }

  public void Capture(LayoutSettings layout) {
    if (_box.DataContext is not MainViewModel vm)
      return;

    layout.MessageFilter = vm.MessageFilter ?? "";
  }
}
