using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public sealed class SemanticSearchService : ISemanticSearchService {
  private readonly ConfigurationFileService _files;
  private readonly MailArchiveStore _archive;
  private readonly EmbeddingModelDownloader _downloader;
  private readonly Lock _gate = new();
  private readonly SemaphoreSlim _wake = new(0, 1);
  private CancellationTokenSource? _run;
  private CancellationTokenSource? _step;
  private ITextEmbedder? _embedder;
  private bool _gpu;
  private string _status = "";
  private bool _ready;

  public SemanticSearchService(ConfigurationFileService files, MailArchiveStore archive) {
    _files = files;
    _archive = archive;
    _downloader = new EmbeddingModelDownloader();
  }

  public bool IsReady {
    get {
      lock (_gate)
        return _ready;
    }
  }

  public bool UsesGpu {
    get {
      lock (_gate)
        return _gpu;
    }
  }

  public string StatusLine {
    get {
      lock (_gate)
        return _status;
    }
  }

  public event Action? Changed;

  public void NotifySettingsChanged() {
    _step?.Cancel();
    try {
      _wake.Release();
    }
    catch (SemaphoreFullException) {
    }
  }

  public float[]? EmbedQuery(string query) {
    ITextEmbedder? embedder;
    lock (_gate)
      embedder = _ready ? _embedder : null;
    if (embedder is null)
      return null;
    try {
      return embedder.Embed(EmbeddingPrompt.Query(query));
    }
    catch {
      return null;
    }
  }

  public ArchiveIndexStats IndexStats() =>
    _archive.IndexStats(EmbeddingModelSpec.Id);

  public int RebuildKeywordIndex() {
    var count = _archive.RebuildKeywordIndex();
    SetStatus($"Keyword index rebuilt ({count})", ready: _embedder is not null);
    return count;
  }

  public ArchiveIndexStats SanitizeIndices() {
    _step?.Cancel();
    var stats = _archive.SanitizeIndices(EmbeddingModelSpec.Id);
    SetStatus(
      stats.Orphans == 0
        ? $"Indices repaired ({stats.KeywordRows} keyword, {stats.MeaningRows} meaning)"
        : $"Indices repaired ({stats.Orphans} orphans remain)",
      ready: _embedder is not null);
    NotifySettingsChanged();
    return stats;
  }

  public int RebuildMeaningIndex() {
    _step?.Cancel();
    var dropped = _archive.ClearEmbeddings();
    SetStatus("Meaning index cleared", ready: false);
    NotifySettingsChanged();
    return dropped;
  }

  public void Start() {
    if (_run is not null)
      return;
    _run = new CancellationTokenSource();
    _ = Task.Run(() => LoopAsync(_run.Token), CancellationToken.None);
  }

  public void Dispose() {
    _run?.Cancel();
    _run?.Dispose();
    _wake.Dispose();
    _downloader.Dispose();
    lock (_gate) {
      _embedder?.Dispose();
      _embedder = null;
    }
  }

  private async Task LoopAsync(CancellationToken token) {
    while (!token.IsCancellationRequested) {
      using var step = CancellationTokenSource.CreateLinkedTokenSource(token);
      _step = step;
      try {
        await RunOnceAsync(step.Token).ConfigureAwait(false);
      }
      catch (OperationCanceledException) {
        if (token.IsCancellationRequested)
          break;
      }
      catch (Exception ex) {
        SetStatus(ex.Message, ready: false);
      }
    }
  }

  private async Task RunOnceAsync(CancellationToken token) {
    var settings = _files.Current.Semantic ?? new SemanticSearchSettings();
    settings.Normalize();
    if (!settings.Enabled) {
      CloseEmbedder();
      SetStatus("", ready: false);
      await WaitAsync(token).ConfigureAwait(false);
      return;
    }

    if (!EmbeddingModelSpec.FilesLookReady()) {
      SetStatus("Downloading meaning model…", ready: false);
      var lastPct = -1;
      var progress = new DirectProgress(p => {
        if (p.Total is not > 0)
          return;
        var pct = (int)(p.Received * 100 / p.Total.Value);
        if (pct == lastPct)
          return;
        lastPct = pct;
        SetStatus($"Downloading meaning model… {pct}% ({p.Label})", ready: false);
      });
      var downloaded = await _downloader.EnsureAsync(progress, token).ConfigureAwait(false);
      if (!downloaded.IsSuccess) {
        SetStatus(string.Join(" ", downloaded.Messages), ready: false);
        await DelayAsync(TimeSpan.FromMinutes(2), token).ConfigureAwait(false);
        return;
      }
    }

    var wantGpu = GpuProbe.UseGpu(settings.Device);
    if (!EnsureEmbedder(wantGpu, out var error)) {
      SetStatus(error, ready: false);
      await DelayAsync(TimeSpan.FromSeconds(30), token).ConfigureAwait(false);
      return;
    }

    _archive.DropForeignEmbeddings(EmbeddingModelSpec.Id);
    while (!token.IsCancellationRequested) {
      settings = _files.Current.Semantic ?? settings;
      if (!settings.Enabled)
        return;
      var pending = _archive.PendingEmbeddings(EmbeddingModelSpec.Id, 8);
      if (pending.Count == 0) {
        var done = _archive.EmbeddingCount(EmbeddingModelSpec.Id);
        var gpu = UsesGpu ? "GPU" : "CPU";
        SetStatus(MailIndexProgress.MeaningReady(gpu, done), ready: true);
        await WaitAsync(token).ConfigureAwait(false);
        return;
      }

      var left = _archive.EmbeddingPendingCount(EmbeddingModelSpec.Id);
      SetStatus(MailIndexProgress.MeaningLeft(left), ready: true);
      foreach (var item in pending) {
        token.ThrowIfCancellationRequested();
        ITextEmbedder embedder;
        lock (_gate)
          embedder = _embedder ?? throw new InvalidOperationException("Embedder missing.");
        var text = EmbeddingPrompt.Document(item.Subject, item.From, item.Body, item.Attachments);
        float[] vector;
        try {
          vector = embedder.Embed(text);
        }
        catch (Exception ex) {
          SetStatus(ex.Message, ready: false);
          await DelayAsync(TimeSpan.FromSeconds(15), token).ConfigureAwait(false);
          return;
        }

        _archive.UpsertEmbedding(item.MessageId, EmbeddingModelSpec.Id, vector);
      }
    }
  }

  private bool EnsureEmbedder(bool wantGpu, out string error) {
    error = "";
    lock (_gate) {
      if (_embedder is not null && _gpu == wantGpu) {
        _ready = true;
        return true;
      }

      _embedder?.Dispose();
      _embedder = null;
      _ready = false;
      _gpu = false;
    }

    try {
      var created = new OnnxGemmaEmbedder(
        EmbeddingModelSpec.OnnxPath(),
        EmbeddingModelSpec.TokenizerPath(),
        wantGpu);
      if (wantGpu && !created.UsesGpu && SemanticDevice.IsGpu(_files.Current.Semantic?.Device)) {
        created.Dispose();
        created = new OnnxGemmaEmbedder(
          EmbeddingModelSpec.OnnxPath(),
          EmbeddingModelSpec.TokenizerPath(),
          useGpu: false);
      }

      lock (_gate) {
        _embedder = created;
        _gpu = created.UsesGpu;
        _ready = true;
      }

      return true;
    }
    catch (Exception ex) {
      error = ex.Message;
      return false;
    }
  }

  private void CloseEmbedder() {
    lock (_gate) {
      _embedder?.Dispose();
      _embedder = null;
      _ready = false;
      _gpu = false;
    }
  }

  private void SetStatus(string line, bool ready) {
    var nextReady = ready && _embedder is not null;
    lock (_gate) {
      if (_status == line && _ready == nextReady)
        return;
      _status = line;
      _ready = nextReady;
    }

    Changed?.Invoke();
  }

  private async Task WaitAsync(CancellationToken token) {
    try {
      await _wake.WaitAsync(token).ConfigureAwait(false);
    }
    catch (OperationCanceledException) {
    }
  }

  private static async Task DelayAsync(TimeSpan delay, CancellationToken token) {
    try {
      await Task.Delay(delay, token).ConfigureAwait(false);
    }
    catch (OperationCanceledException) {
    }
  }

  private sealed class DirectProgress(Action<EmbeddingDownloadProgress> handler)
    : IProgress<EmbeddingDownloadProgress> {
    public void Report(EmbeddingDownloadProgress value) =>
      handler(value);
  }
}
