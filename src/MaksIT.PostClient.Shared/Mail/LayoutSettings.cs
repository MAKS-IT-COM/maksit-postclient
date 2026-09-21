namespace MaksIT.PostClient.Shared.Mail;


public sealed class SavedColumnSort {
  public string Header { get; set; } = "";

  public string Direction { get; set; } = "Ascending";
}


public sealed class LayoutSettings {
  public const double DefaultWindowWidth = 1280;
  public const double DefaultWindowHeight = 860;
  public const double DefaultFolderWidth = 248;
  public const double DefaultListWidth = 360;
  public const double DefaultListHeight = 320;

  public double WindowWidth { get; set; } = DefaultWindowWidth;

  public double WindowHeight { get; set; } = DefaultWindowHeight;

  public int? WindowX { get; set; }

  public int? WindowY { get; set; }

  public string WindowState { get; set; } = "Normal";

  public double FolderWidth { get; set; } = DefaultFolderWidth;

  public double ListWidth { get; set; } = DefaultListWidth;

  public double ListHeight { get; set; } = DefaultListHeight;

  public string MessageFilter { get; set; } = "";

  public Dictionary<string, double> ColumnWidths { get; set; } = new(StringComparer.Ordinal);

  public List<string> ColumnOrder { get; set; } = [];

  public SavedColumnSort? ColumnSort { get; set; }

  public List<string> CollapsedFolders { get; set; } = [];

  public static string FolderTreeKey(string? mailboxId, string? folder) {
    var id = (mailboxId ?? "").Trim();
    if (id.Length == 0)
      return "";
    var name = (folder ?? "").Trim();
    return name.Length == 0 ? id : id + "\t" + name;
  }

  public bool IsFolderExpanded(string? mailboxId, string? folder) {
    var key = FolderTreeKey(mailboxId, folder);
    if (key.Length == 0)
      return true;
    foreach (var row in CollapsedFolders) {
      if (string.Equals(row, key, StringComparison.OrdinalIgnoreCase))
        return false;
    }

    return true;
  }

  public void Normalize() {
    ColumnWidths ??= new Dictionary<string, double>(StringComparer.Ordinal);
    ColumnOrder ??= [];
    CollapsedFolders ??= [];
    MessageFilter ??= "";
    if (string.IsNullOrWhiteSpace(WindowState))
      WindowState = "Normal";
    if (WindowWidth <= 0)
      WindowWidth = DefaultWindowWidth;
    if (WindowHeight <= 0)
      WindowHeight = DefaultWindowHeight;
    if (FolderWidth <= 0)
      FolderWidth = DefaultFolderWidth;
    if (ListWidth <= 0)
      ListWidth = DefaultListWidth;
  }

  public double ResolvedFolderWidth() =>
    Clamp(FolderWidth, 160, 900, DefaultFolderWidth);

  public double ResolvedListWidth() =>
    Clamp(ListWidth, 220, 1600, DefaultListWidth);

  public double ResolvedListHeight() =>
    Clamp(ListHeight, 120, 5000, DefaultListHeight);

  public static double Clamp(double value, double min, double max, double fallback) =>
    value < min || value > max || double.IsNaN(value) ? fallback : value;
}
