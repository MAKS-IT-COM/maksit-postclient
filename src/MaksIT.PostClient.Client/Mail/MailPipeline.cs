namespace MaksIT.PostClient.Client.Mail;


/// <summary>
/// One background thread for incoming mail, in the shape Thunderbird uses for filters and gloda.
/// Each turn gets one folder page, applies rules, then indexes a few bodies.
/// Meaning search stays on its own thread and is only nudged.
/// </summary>
public sealed class MailPipeline : IDisposable {
  public const int HeaderPage = 200;
  public const int RulesBatch = 24;
  public const int BodyBatch = 2;
  public const int MeaningBatch = 4;
  public static readonly TimeSpan Yield = TimeSpan.FromMilliseconds(50);
  public static readonly TimeSpan Idle = TimeSpan.FromSeconds(15);
  public static readonly TimeSpan MeaningYield = TimeSpan.FromMilliseconds(400);
  public static readonly TimeSpan FolderPoll = TimeSpan.FromSeconds(45);

  private readonly IMailPipelineWork _work;
  private readonly TimeSpan _yield;
  private readonly TimeSpan _idle;
  private readonly int _bodyBatch;
  private readonly object _gate = new();
  private Thread? _thread;
  private CancellationTokenSource? _cts;
  private CancellationTokenSource? _step;
  private CancellationTokenSource? _wait;

  public MailPipeline(
    IMailPipelineWork work,
    TimeSpan? turnYield = null,
    TimeSpan? idle = null,
    int? bodyBatch = null) {
    _work = work;
    _yield = turnYield ?? Yield;
    _idle = idle ?? Idle;
    _bodyBatch = bodyBatch ?? BodyBatch;
  }

  public void Start() {
    if (_thread is not null)
      return;
    var cts = new CancellationTokenSource();
    _cts = cts;
    var thread = new Thread(() => Run(cts.Token)) {
      IsBackground = true,
      Name = "Mail pipeline"
    };
    _thread = thread;
    thread.Start();
  }

  public void Stop() {
    var thread = _thread;
    var cts = _cts;
    _thread = null;
    _cts = null;
    TryCancel(cts);
    if (thread is not null && thread.IsAlive && !thread.Join(TimeSpan.FromSeconds(5)))
      return;
    cts?.Dispose();
  }

  public void Interrupt() {
    CancellationTokenSource? step;
    CancellationTokenSource? wait;
    lock (_gate) {
      step = _step;
      wait = _wait;
    }

    TryCancel(step);
    TryCancel(wait);
  }

  public void Dispose() => Stop();

  private void Run(CancellationToken token) {
    try {
      LoopAsync(token).GetAwaiter().GetResult();
    }
    catch (OperationCanceledException) {
    }
  }

  internal async Task LoopAsync(CancellationToken token) {
    while (!token.IsCancellationRequested) {
      var step = CancellationTokenSource.CreateLinkedTokenSource(token);
      lock (_gate)
        _step = step;
      var busy = false;
      try {
        busy = await TurnAsync(step.Token).ConfigureAwait(false);
      }
      catch (OperationCanceledException) when (!token.IsCancellationRequested) {
        continue;
      }
      catch (Exception ex) when (ex is not OperationCanceledException) {
        AppLog.Write(ex);
        busy = false;
      }
      finally {
        lock (_gate) {
          if (ReferenceEquals(_step, step))
            _step = null;
        }

        step.Dispose();
      }

      if (!await PauseAsync(busy ? _yield : _idle, token).ConfigureAwait(false))
        break;
    }
  }

  private async Task<bool> TurnAsync(CancellationToken token) {
    await _work.WaitWhilePausedAsync(token).ConfigureAwait(false);
    var fetched = await _work.FetchNextPageAsync(token).ConfigureAwait(false);
    await _work.WaitWhilePausedAsync(token).ConfigureAwait(false);
    var ruled = await _work.ApplyRulesAsync(token).ConfigureAwait(false);
    await _work.WaitWhilePausedAsync(token).ConfigureAwait(false);
    var bodies = await _work.IndexBodiesAsync(_bodyBatch, token).ConfigureAwait(false);
    if (bodies > 0)
      _work.WakeMeaningIndex();
    return fetched || ruled || bodies > 0;
  }

  private async Task<bool> PauseAsync(TimeSpan delay, CancellationToken token) {
    var wait = CancellationTokenSource.CreateLinkedTokenSource(token);
    lock (_gate)
      _wait = wait;
    try {
      await Task.Delay(delay, wait.Token).ConfigureAwait(false);
      return true;
    }
    catch (OperationCanceledException) when (!token.IsCancellationRequested) {
      return true;
    }
    catch (OperationCanceledException) {
      return false;
    }
    finally {
      lock (_gate) {
        if (ReferenceEquals(_wait, wait))
          _wait = null;
      }

      wait.Dispose();
    }
  }

  private static void TryCancel(CancellationTokenSource? cts) {
    if (cts is null)
      return;
    try {
      cts.Cancel();
    }
    catch (ObjectDisposedException) {
    }
  }
}


public interface IMailPipelineWork {
  Task WaitWhilePausedAsync(CancellationToken token);

  Task<bool> FetchNextPageAsync(CancellationToken token);

  Task<bool> ApplyRulesAsync(CancellationToken token);

  Task<int> IndexBodiesAsync(int limit, CancellationToken token);

  void WakeMeaningIndex();
}
