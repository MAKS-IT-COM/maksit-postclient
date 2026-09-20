namespace MaksIT.PostClient.Client;


internal sealed class MailSessionGate : IDisposable {
  private readonly SemaphoreSlim _io = new(1, 1);
  private int _interactiveWaiting;

  public Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken) =>
    RunCoreAsync(work, cancellationToken, interactive: false);

  public Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken, bool interactive) =>
    RunCoreAsync(work, cancellationToken, interactive);

  public Task RunAsync(Func<Task> work, CancellationToken cancellationToken) =>
    RunCoreAsync(Wrap(work), cancellationToken, interactive: false);

  public Task RunAsync(Func<Task> work, CancellationToken cancellationToken, bool interactive) =>
    RunCoreAsync(Wrap(work), cancellationToken, interactive);

  private static Func<Task<int>> Wrap(Func<Task> work) =>
    async () => {
      await work().ConfigureAwait(false);
      return 0;
    };

  private async Task<T> RunCoreAsync<T>(
    Func<Task<T>> work,
    CancellationToken cancellationToken,
    bool interactive) {
    await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
    if (interactive)
      Interlocked.Increment(ref _interactiveWaiting);
    try {
      while (true) {
        await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (!interactive && Volatile.Read(ref _interactiveWaiting) > 0) {
          _io.Release();
          await Task.Delay(1, cancellationToken).ConfigureAwait(false);
          continue;
        }

        try {
          return await work().ConfigureAwait(false);
        }
        finally {
          _io.Release();
        }
      }
    }
    finally {
      if (interactive)
        Interlocked.Decrement(ref _interactiveWaiting);
    }
  }

  public void Dispose() =>
    _io.Dispose();
}
