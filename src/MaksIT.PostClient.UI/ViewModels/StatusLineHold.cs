using Avalonia.Threading;


namespace MaksIT.PostClient.UI.ViewModels;


internal sealed class StatusLineHold {
  private readonly Action<string> _apply;
  private readonly int _minShowMs;
  private readonly int _hideMs;
  private string _wanted = "";
  private string _shown = "";
  private long _shownAt;
  private int _gen;
  private CancellationTokenSource? _delay;

  public StatusLineHold(Action<string> apply, int minShowMs = 250, int hideMs = 800) {
    _apply = apply;
    _minShowMs = minShowMs;
    _hideMs = hideMs;
  }

  public void Set(string line) {
    _wanted = line ?? "";
    CancelDelay();
    if (!string.IsNullOrWhiteSpace(_wanted)) {
      _gen++;
      var now = Environment.TickCount64;
      if (_shown == _wanted)
        return;
      if (_shown.Length > 0 && now - _shownAt < _minShowMs) {
        Start(FlushLater);
        return;
      }

      Show(_wanted);
      return;
    }

    var gen = ++_gen;
    Start(token => HideLater(gen, token));
  }

  public void ClearNow() {
    CancelDelay();
    _gen++;
    _wanted = "";
    Show("");
  }

  public void Dispose() =>
    CancelDelay();

  private void Start(Func<CancellationToken, Task> work) {
    var delay = new CancellationTokenSource();
    _delay = delay;
    _ = work(delay.Token);
  }

  private async Task FlushLater(CancellationToken token) {
    var wait = _minShowMs - (int)(Environment.TickCount64 - _shownAt);
    if (wait < 0)
      wait = 0;
    try {
      await Task.Delay(wait, token).ConfigureAwait(false);
    }
    catch (OperationCanceledException) {
      return;
    }

    await Dispatcher.UIThread.InvokeAsync(() => {
      if (!string.IsNullOrWhiteSpace(_wanted) && _wanted != _shown)
        Show(_wanted);
    });
  }

  private async Task HideLater(int gen, CancellationToken token) {
    try {
      await Task.Delay(_hideMs, token).ConfigureAwait(false);
    }
    catch (OperationCanceledException) {
      return;
    }

    await Dispatcher.UIThread.InvokeAsync(() => {
      if (gen != _gen)
        return;
      if (string.IsNullOrWhiteSpace(_wanted))
        Show("");
    });
  }

  private void Show(string line) {
    _shown = line;
    _shownAt = Environment.TickCount64;
    _apply(line);
  }

  private void CancelDelay() {
    _delay?.Cancel();
    _delay?.Dispose();
    _delay = null;
  }
}
