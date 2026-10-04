using Avalonia;
using Avalonia.Controls;


namespace MaksIT.PostClient.UI.Layout;


public enum LayoutSlot {
  FolderWidth,
}

public enum LayoutAxis {
  Column,
  Row,
}

public enum MailPaneRole {
  None,
  Folder,
  FolderSplitter,
  List,
  StackedSplitter,
  WideSplitter,
  Reading,
}

public sealed class LayoutBand {
  public LayoutSlot Slot { get; set; }

  public LayoutAxis Axis { get; set; } = LayoutAxis.Column;

  public int Index { get; set; } = -1;

  public double Min { get; set; }

  public double Max { get; set; }

  public double Fallback { get; set; }

  public double MinHostExtent { get; set; }
}

/// <summary>
/// Each control registers its own originator. The caretaker does not name the control.
/// </summary>
public static class LayoutMemento {
  public static readonly AttachedProperty<bool> PersistWindowProperty =
    AvaloniaProperty.RegisterAttached<Window, bool>("PersistWindow", typeof(LayoutMemento));

  public static readonly AttachedProperty<bool> PersistMailProperty =
    AvaloniaProperty.RegisterAttached<Grid, bool>("PersistMail", typeof(LayoutMemento));

  public static readonly AttachedProperty<bool> ListPaneProperty =
    AvaloniaProperty.RegisterAttached<Control, bool>("ListPane", typeof(LayoutMemento));

  public static readonly AttachedProperty<string?> TableProperty =
    AvaloniaProperty.RegisterAttached<DataGrid, string?>("Table", typeof(LayoutMemento));

  public static readonly AttachedProperty<bool> PersistFoldersProperty =
    AvaloniaProperty.RegisterAttached<TreeView, bool>("PersistFolders", typeof(LayoutMemento));

  public static readonly AttachedProperty<bool> PersistFilterProperty =
    AvaloniaProperty.RegisterAttached<TextBox, bool>("PersistFilter", typeof(LayoutMemento));

  public static readonly AttachedProperty<LayoutBand?> BandProperty =
    AvaloniaProperty.RegisterAttached<Control, LayoutBand?>("Band", typeof(LayoutMemento));

  public static readonly AttachedProperty<MailPaneRole> PaneRoleProperty =
    AvaloniaProperty.RegisterAttached<Control, MailPaneRole>("PaneRole", typeof(LayoutMemento));

  private static readonly AttachedProperty<LayoutPersistence?> CaretakerProperty =
    AvaloniaProperty.RegisterAttached<Window, LayoutPersistence?>("Caretaker", typeof(LayoutMemento));

  private static readonly AttachedProperty<bool> WatchingProperty =
    AvaloniaProperty.RegisterAttached<Control, bool>("Watching", typeof(LayoutMemento));

  private static readonly AttachedProperty<bool> RegisteredProperty =
    AvaloniaProperty.RegisterAttached<Control, bool>("Registered", typeof(LayoutMemento));

  private static readonly List<WeakReference<Control>> Pending = [];

  static LayoutMemento() {
    PersistWindowProperty.Changed.AddClassHandler<Window>((control, _) => Watch(control));
    PersistMailProperty.Changed.AddClassHandler<Grid>((control, _) => Watch(control));
    ListPaneProperty.Changed.AddClassHandler<Control>((control, _) => Watch(control));
    TableProperty.Changed.AddClassHandler<DataGrid>((control, _) => Watch(control));
    PersistFoldersProperty.Changed.AddClassHandler<TreeView>((control, _) => Watch(control));
    PersistFilterProperty.Changed.AddClassHandler<TextBox>((control, _) => Watch(control));
    BandProperty.Changed.AddClassHandler<Control>((control, _) => Watch(control));
    CaretakerProperty.Changed.AddClassHandler<Window>((control, _) => TryRegister(control));
  }

  public static bool GetPersistWindow(Window window) =>
    window.GetValue(PersistWindowProperty);

  public static void SetPersistWindow(Window window, bool value) =>
    window.SetValue(PersistWindowProperty, value);

  public static bool GetPersistMail(Grid grid) =>
    grid.GetValue(PersistMailProperty);

  public static void SetPersistMail(Grid grid, bool value) =>
    grid.SetValue(PersistMailProperty, value);

  public static bool GetListPane(Control control) =>
    control.GetValue(ListPaneProperty);

  public static void SetListPane(Control control, bool value) =>
    control.SetValue(ListPaneProperty, value);

  public static string? GetTable(DataGrid grid) =>
    grid.GetValue(TableProperty);

  public static void SetTable(DataGrid grid, string? value) =>
    grid.SetValue(TableProperty, value);

  public static bool GetPersistFolders(TreeView tree) =>
    tree.GetValue(PersistFoldersProperty);

  public static void SetPersistFolders(TreeView tree, bool value) =>
    tree.SetValue(PersistFoldersProperty, value);

  public static bool GetPersistFilter(TextBox box) =>
    box.GetValue(PersistFilterProperty);

  public static void SetPersistFilter(TextBox box, bool value) =>
    box.SetValue(PersistFilterProperty, value);

  public static LayoutBand? GetBand(Control control) =>
    control.GetValue(BandProperty);

  public static void SetBand(Control control, LayoutBand? value) =>
    control.SetValue(BandProperty, value);

  public static MailPaneRole GetPaneRole(Control control) =>
    control.GetValue(PaneRoleProperty);

  public static void SetPaneRole(Control control, MailPaneRole value) =>
    control.SetValue(PaneRoleProperty, value);

  internal static void Publish(Window window, LayoutPersistence caretaker) {
    window.SetValue(CaretakerProperty, caretaker);

    foreach (var reference in Pending.ToArray()) {
      if (reference.TryGetTarget(out var control))
        TryRegister(control);
    }

    Pending.RemoveAll(reference => !reference.TryGetTarget(out var control) || control.GetValue(RegisteredProperty));
  }

  internal static IDisposable SuspendSave(Control control) =>
    CaretakerFor(control)?.SuspendSave() ?? Noop.Instance;

  private static void Watch(Control control) {
    if (control.GetValue(WatchingProperty))
      return;

    control.SetValue(WatchingProperty, true);
    control.AttachedToLogicalTree += (_, _) => TryRegister(control);
    TryRegister(control);

    if (!control.GetValue(RegisteredProperty))
      Pending.Add(new WeakReference<Control>(control));
  }

  private static void TryRegister(Control control) {
    if (control.GetValue(RegisteredProperty))
      return;

    var caretaker = CaretakerFor(control);

    if (caretaker is null)
      return;

    var originator = Create(control, caretaker);

    if (originator is null)
      return;

    control.SetValue(RegisteredProperty, true);
    caretaker.Register(originator);
  }

  private static LayoutPersistence? CaretakerFor(StyledElement control) {
    for (StyledElement? node = control; node is not null; node = node.Parent) {
      if (node is Window window && window.GetValue(CaretakerProperty) is LayoutPersistence caretaker)
        return caretaker;
    }

    return null;
  }

  private static ILayoutOriginator? Create(Control control, LayoutPersistence caretaker) {
    if (control is Window window && window.GetValue(PersistWindowProperty))
      return new WindowLayoutOriginator(window);

    if (control is Grid mailGrid && mailGrid.GetValue(PersistMailProperty))
      return new MailGridOriginator(mailGrid, caretaker.Layout);

    if (control.GetValue(ListPaneProperty))
      return new ListPaneOriginator(control);

    if (control.GetValue(BandProperty) is LayoutBand band)
      return CreateBand(control, band);

    if (control is DataGrid grid) {
      var key = grid.GetValue(TableProperty);

      if (!string.IsNullOrWhiteSpace(key))
        return new MessageGridOriginator(grid, caretaker.Layout);
    }

    if (control is TreeView tree && tree.GetValue(PersistFoldersProperty))
      return new FolderTreeOriginator(tree, caretaker.Configuration);

    if (control is TextBox box && box.GetValue(PersistFilterProperty))
      return new FilterOriginator(box);

    return null;
  }

  private static ILayoutOriginator? CreateBand(Control control, LayoutBand band) {
    var targetSelf = control is Grid && band.Index >= 0;
    var grid = targetSelf ? (Grid)control : control.Parent as Grid;

    if (grid is null)
      return null;

    var index = band.Index >= 0
      ? band.Index
      : band.Axis == LayoutAxis.Row ? Grid.GetRow(control) : Grid.GetColumn(control);
    var prefer = targetSelf ? null : control;
    var (read, write) = Bind(band.Slot);

    if (band.Axis == LayoutAxis.Row) {
      return GridBandOriginator.Row(
        grid, index, band.Min, band.Max, band.Fallback, read, write, band.MinHostExtent);
    }

    return GridBandOriginator.Column(
      grid, index, band.Min, band.Max, band.Fallback, read, write, prefer);
  }

  private static (Func<LayoutSettings, double> Read, Action<LayoutSettings, double> Write) Bind(LayoutSlot slot) =>
    slot switch {
      LayoutSlot.FolderWidth => (static layout => layout.FolderWidth, static (layout, value) => layout.FolderWidth = value),
      _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
    };

  private sealed class Noop : IDisposable {
    public static readonly Noop Instance = new();

    public void Dispose() {
    }
  }
}
