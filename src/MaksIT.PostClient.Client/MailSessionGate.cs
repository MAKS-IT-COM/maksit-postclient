namespace MaksIT.PostClient.Client;


internal sealed class MailSessionGate : IDisposable {
  private readonly SemaphoreSlim _io = new(1, 1);

  public async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken) {
    await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
    await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
    try {
      return await work().ConfigureAwait(false);
    }
    finally {
      _io.Release();
    }
  }

  public async Task RunAsync(Func<Task> work, CancellationToken cancellationToken) {
    await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
    await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
    try {
      await work().ConfigureAwait(false);
    }
    finally {
      _io.Release();
    }
  }

  public void Dispose() =>
    _io.Dispose();
}
