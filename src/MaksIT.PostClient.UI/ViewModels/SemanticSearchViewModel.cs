using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;


namespace MaksIT.PostClient.UI.ViewModels;


public sealed partial class SemanticSearchViewModel : ObservableObject {
  private readonly ConfigurationFileService _files;
  private readonly ISemanticSearchService _semantic;
  private int _statusPosted;

  public SemanticSearchViewModel(ConfigurationFileService files, ISemanticSearchService semantic) {
    _files = files;
    _semantic = semantic;
    var settings = files.Current.Semantic ?? new SemanticSearchSettings();
    settings.Normalize();
    enabled = settings.Enabled;
    device = settings.Device;
    status = semantic.StatusLine;
    modelFilesReady = EmbeddingModelSpec.FilesLookReady();
    RefreshStats();
    semantic.Changed += OnChanged;
  }

  public UiCopy Copy =>
    UiLocale.Copy;

  public bool GpuAvailable =>
    GpuProbe.SupportsBoost;

  public bool GpuChoiceEnabled =>
    GpuAvailable && !IsWorking;

  public string ModelsPath =>
    AppPaths.ModelsDirectory();

  [ObservableProperty]
  private bool enabled;

  [ObservableProperty]
  private string device = SemanticDevice.Auto;

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
  private string status = "";

  [ObservableProperty]
  private string keywordStatus = "";

  [ObservableProperty]
  private string meaningStatus = "";

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
  [NotifyCanExecuteChangedFor(nameof(RebuildKeywordCommand))]
  [NotifyCanExecuteChangedFor(nameof(RebuildMeaningCommand))]
  [NotifyCanExecuteChangedFor(nameof(SanitizeIndicesCommand))]
  private bool isWorking;

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
  private bool modelFilesReady;

  public bool IsAuto {
    get => SemanticDevice.IsAuto(Device);
    set {
      if (value)
        Device = SemanticDevice.Auto;
    }
  }

  public bool IsCpu {
    get => SemanticDevice.IsCpu(Device);
    set {
      if (value)
        Device = SemanticDevice.Cpu;
    }
  }

  public bool IsGpu {
    get => SemanticDevice.IsGpu(Device);
    set {
      if (value)
        Device = SemanticDevice.Gpu;
    }
  }

  public void Persist() {
    var configuration = _files.Current;
    configuration.Semantic ??= new SemanticSearchSettings();
    configuration.Semantic.Enabled = Enabled;
    configuration.Semantic.Device = SemanticDevice.Normalize(Device);
    configuration.Semantic.Normalize();
    _files.Save(configuration);
    _semantic.NotifySettingsChanged();
  }

  [RelayCommand(CanExecute = nameof(CanDownload))]
  private void Retry() {
    if (EmbeddingModelSpec.FilesLookReady()) {
      ModelFilesReady = true;
      return;
    }

    _semantic.NotifySettingsChanged();
  }

  [RelayCommand(CanExecute = nameof(CanRepair))]
  private async Task RebuildKeywordAsync() =>
    await RepairAsync(() => _semantic.RebuildKeywordIndex());

  [RelayCommand(CanExecute = nameof(CanRepair))]
  private async Task RebuildMeaningAsync() =>
    await RepairAsync(() => _semantic.RebuildMeaningIndex());

  [RelayCommand(CanExecute = nameof(CanRepair))]
  private async Task SanitizeIndicesAsync() =>
    await RepairAsync(() => _semantic.SanitizeIndices());

  public void Detach() =>
    _semantic.Changed -= OnChanged;

  private bool CanDownload() =>
    !IsWorking
    && !ModelFilesReady
    && !Status.StartsWith("Downloading", StringComparison.OrdinalIgnoreCase);

  private bool CanRepair() =>
    !IsWorking;

  private async Task RepairAsync(Action work) {
    if (IsWorking)
      return;
    IsWorking = true;
    try {
      await Task.Run(work);
      RefreshStats();
      Status = _semantic.StatusLine;
      ModelFilesReady = EmbeddingModelSpec.FilesLookReady();
    }
    finally {
      IsWorking = false;
    }
  }

  private void OnChanged() {
    if (Interlocked.Exchange(ref _statusPosted, 1) == 1)
      return;
    Dispatcher.UIThread.Post(ApplyStatus);
  }

  private void ApplyStatus() {
    Interlocked.Exchange(ref _statusPosted, 0);
    var line = _semantic.StatusLine;
    if (Status != line)
      Status = line;
    ModelFilesReady = EmbeddingModelSpec.FilesLookReady();
    if (line.StartsWith("Downloading", StringComparison.OrdinalIgnoreCase))
      return;
    RefreshStats();
  }

  private void RefreshStats() {
    var stats = _semantic.IndexStats();
    KeywordStatus = string.Format(Copy.IndicesKeywordStats, stats.Messages, stats.KeywordRows);
    MeaningStatus = string.Format(
      Copy.IndicesMeaningStats,
      stats.MeaningRows,
      stats.MeaningPending,
      stats.Orphans);
  }

  partial void OnIsWorkingChanged(bool value) =>
    OnPropertyChanged(nameof(GpuChoiceEnabled));

  partial void OnEnabledChanged(bool value) =>
    Persist();

  partial void OnDeviceChanged(string value) {
    OnPropertyChanged(nameof(IsAuto));
    OnPropertyChanged(nameof(IsCpu));
    OnPropertyChanged(nameof(IsGpu));
    Persist();
  }
}
