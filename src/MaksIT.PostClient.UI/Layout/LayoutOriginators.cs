using Avalonia;
using Avalonia.Controls;


namespace MaksIT.PostClient.UI.Layout;


internal static class LayoutRange {
  public static double Clamp(double value, double min, double max, double fallback) =>
    value < min || value > max || double.IsNaN(value) ? fallback : value;
}

internal sealed class WindowLayoutOriginator : ILayoutOriginator {
  private readonly Window _window;

  public WindowLayoutOriginator(Window window) => _window = window;

  public bool DeferSave => false;

  public void Attach(Action changed) {
    _window.Resized += (_, _) => changed();
    _window.PositionChanged += (_, _) => changed();
    _window.PropertyChanged += (_, e) => {
      if (e.Property.Name == nameof(Window.WindowState))
        changed();
    };
  }

  public void Apply(LayoutSettings layout) {
    _window.Width = LayoutRange.Clamp(
      layout.WindowWidth,
      _window.MinWidth,
      10000,
      LayoutSettings.DefaultWindowWidth);
    _window.Height = LayoutRange.Clamp(
      layout.WindowHeight,
      _window.MinHeight,
      10000,
      LayoutSettings.DefaultWindowHeight);

    if (layout.WindowX is int x && layout.WindowY is int y && IsOnScreen(x, y))
      _window.Position = new PixelPoint(x, y);

    if (Enum.TryParse<WindowState>(layout.WindowState, true, out var state) && state != WindowState.Minimized)
      _window.WindowState = state;
  }

  public void Capture(LayoutSettings layout) {
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

  private bool IsOnScreen(int x, int y) {
    var screens = _window.Screens?.All;

    if (screens is null || screens.Count == 0)
      return true;

    return screens.Any(screen => screen.WorkingArea.Contains(new PixelPoint(x, y)));
  }
}

internal sealed class GridBandOriginator : ILayoutOriginator {
  private readonly Grid? _grid;
  private readonly int _index;
  private readonly bool _column;
  private readonly double _min;
  private readonly double _max;
  private readonly double _fallback;
  private readonly Func<LayoutSettings, double> _read;
  private readonly Action<LayoutSettings, double> _write;
  private readonly Control? _prefer;
  private readonly double _minHostExtent;

  private GridBandOriginator(
    Grid? grid,
    int index,
    bool column,
    double min,
    double max,
    double fallback,
    Func<LayoutSettings, double> read,
    Action<LayoutSettings, double> write,
    Control? prefer,
    double minHostExtent) {
    _grid = grid;
    _index = index;
    _column = column;
    _min = min;
    _max = max;
    _fallback = fallback;
    _read = read;
    _write = write;
    _prefer = prefer;
    _minHostExtent = minHostExtent;
  }

  public bool DeferSave => false;

  public static GridBandOriginator Column(
    Grid? grid,
    int index,
    double min,
    double max,
    double fallback,
    Func<LayoutSettings, double> read,
    Action<LayoutSettings, double> write,
    Control? prefer = null) =>
    new(grid, index, column: true, min, max, fallback, read, write, prefer, minHostExtent: 0);

  public static GridBandOriginator Row(
    Grid? grid,
    int index,
    double min,
    double max,
    double fallback,
    Func<LayoutSettings, double> read,
    Action<LayoutSettings, double> write,
    double minHostExtent = 0) =>
    new(grid, index, column: false, min, max, fallback, read, write, prefer: null, minHostExtent);

  public void Attach(Action changed) {
    if (_grid is not null)
      _grid.LayoutUpdated += (_, _) => changed();
  }

  public void Apply(LayoutSettings layout) {
    if (_grid is null)
      return;

    var length = new GridLength(LayoutRange.Clamp(_read(layout), _min, _max, _fallback));

    if (_column) {
      if (_index >= 0 && _index < _grid.ColumnDefinitions.Count)
        _grid.ColumnDefinitions[_index].Width = length;

      return;
    }

    if (_index >= 0 && _index < _grid.RowDefinitions.Count)
      _grid.RowDefinitions[_index].Height = length;
  }

  public void Capture(LayoutSettings layout) {
    if (Measure() is double value && value >= _min)
      _write(layout, value);
  }

  private double? Measure() {
    if (_prefer is { IsVisible: true }) {
      var extent = _column ? _prefer.Bounds.Width : _prefer.Bounds.Height;

      if (extent > 0)
        return extent;
    }

    if (_grid is null)
      return null;

    if (_minHostExtent > 0) {
      var host = _column ? _grid.Bounds.Width : _grid.Bounds.Height;

      if (!_grid.IsVisible || host < _minHostExtent)
        return null;
    }

    if (_column) {
      if (_index < 0 || _index >= _grid.ColumnDefinitions.Count)
        return null;

      var definition = _grid.ColumnDefinitions[_index];

      return definition.Width.IsAbsolute ? definition.Width.Value : null;
    }

    if (_index < 0 || _index >= _grid.RowDefinitions.Count)
      return null;

    var row = _grid.RowDefinitions[_index];

    return row.Height.IsAbsolute ? row.Height.Value : null;
  }
}
