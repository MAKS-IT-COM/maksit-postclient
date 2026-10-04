using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;


namespace MaksIT.PostClient.UI.Layout;


internal sealed class LayoutPersistence {
  private readonly Window _window;
  private readonly ConfigurationFileService _configuration;
  private readonly DispatcherTimer _saveTimer;
  private readonly List<ILayoutOriginator> _originators = [];
  private int _applyDepth;
  private bool _attached;
  private bool _applied;
  private string? _lastSaved;

  internal ConfigurationFileService Configuration => _configuration;

  internal Func<LayoutSettings> Layout => () => _configuration.Current.Layout;

  public LayoutPersistence(Window window, ConfigurationFileService configuration) {
    _window = window;
    _configuration = configuration;
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
    LayoutMemento.Publish(_window, this);
    Apply();
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
    if (_applyDepth > 0 || _originators.Any(originator => originator.DeferSave))
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

    foreach (var originator in _originators)
      originator.Capture(layout);

    var snapshot = JsonSerializer.Serialize(layout);

    if (snapshot == _lastSaved)
      return;

    _configuration.Save(cfg);
    _lastSaved = snapshot;
  }

  internal void Register(ILayoutOriginator originator) {
    _originators.Add(originator);
    originator.Attach(ScheduleSave);

    if (!_applied)
      return;

    using (SuspendSave())
      originator.Apply(_configuration.Current.Layout);
  }

  private void Apply() {
    using (SuspendSave()) {
      var layout = _configuration.Current.Layout;

      foreach (var originator in _originators) {
        if (originator.ApplyFirst)
          originator.Apply(layout);
      }

      foreach (var originator in _originators) {
        if (!originator.ApplyFirst)
          originator.Apply(layout);
      }
    }

    _applied = true;
  }

  private void ReleaseApply() {
    if (_applyDepth > 0)
      _applyDepth--;
  }

  private sealed class ApplyScope(LayoutPersistence owner) : IDisposable {
    public void Dispose() => owner.ReleaseApply();
  }
}
