using CommunityToolkit.Mvvm.ComponentModel;


namespace MaksIT.PostClient.UI.ViewModels;


public sealed partial class LogViewModel : ObservableObject {
  public LogViewModel() {
    Refresh();
  }

  public UiCopy Copy =>
    UiLocale.Copy;

  public IReadOnlyList<string> Files { get; private set; } = [];

  [ObservableProperty]
  private string selectedFile = "";

  [ObservableProperty]
  private string report = "";

  [ObservableProperty]
  private string copyStatus = "";

  public void Refresh() {
    try {
      AppPaths.EnsureDirectories();
      var dir = AppPaths.LogsDirectory();
      Files = Directory.Exists(dir)
        ? Directory.GetFiles(dir)
          .OrderByDescending(File.GetLastWriteTimeUtc)
          .Select(Path.GetFileName)
          .Where(name => !string.IsNullOrWhiteSpace(name))
          .Cast<string>()
          .ToList()
        : [];
    }
    catch {
      Files = [];
    }

    OnPropertyChanged(nameof(Files));
    if (Files.Count == 0) {
      SelectedFile = "";
      Report = "";
      return;
    }

    if (string.IsNullOrWhiteSpace(SelectedFile) || !Files.Contains(SelectedFile, StringComparer.OrdinalIgnoreCase))
      SelectedFile = Files[0];
    else
      LoadSelected();
  }

  partial void OnSelectedFileChanged(string value) =>
    LoadSelected();

  public void MarkCopied() =>
    CopyStatus = Copy.Copied;

  private void LoadSelected() {
    CopyStatus = "";
    if (string.IsNullOrWhiteSpace(SelectedFile)) {
      Report = "";
      return;
    }

    try {
      var path = Path.Combine(AppPaths.LogsDirectory(), SelectedFile);
      Report = File.Exists(path) ? File.ReadAllText(path) : "";
    }
    catch (Exception ex) {
      Report = ex.Message;
    }
  }
}
