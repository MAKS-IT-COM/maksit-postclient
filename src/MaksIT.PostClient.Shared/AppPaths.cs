namespace MaksIT.PostClient.Shared;


/// <summary>
/// Placeholder product folder until the public sign is chosen.
/// Windows: <c>%AppData%\Postclient</c> / <c>%LocalAppData%\Postclient</c>.
/// Linux: XDG. macOS: Application Support. Not a MAKS-IT subfolder.
/// </summary>
public static class AppPaths {
  public const string ProductName = "Postclient";
  public const string ProductId = "postclient";
  public const string ConfigEnv = "POSTCLIENT_CONFIG";
  public const string DataEnv = "POSTCLIENT_DATA_DIR";

  public static string ConfigDirectory() {
    var fromEnv = Environment.GetEnvironmentVariable(ConfigEnv);
    if (!string.IsNullOrWhiteSpace(fromEnv))
      return Path.GetFullPath(fromEnv.Trim());

    var portable = PortableRoot();
    if (portable is not null)
      return portable;

    if (OperatingSystem.IsWindows())
      return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ProductName);

    if (OperatingSystem.IsMacOS()) {
      return Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library",
        "Application Support",
        ProductName);
    }

    var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
    if (string.IsNullOrWhiteSpace(xdg))
      xdg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
    return Path.Combine(xdg, ProductId);
  }

  public static string DataDirectory() {
    var fromEnv = Environment.GetEnvironmentVariable(DataEnv);
    if (!string.IsNullOrWhiteSpace(fromEnv))
      return Path.GetFullPath(fromEnv.Trim());

    var portable = PortableRoot();
    if (portable is not null)
      return Path.Combine(portable, "data");

    if (OperatingSystem.IsWindows())
      return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductName);

    if (OperatingSystem.IsMacOS())
      return ConfigDirectory();

    var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
    if (string.IsNullOrWhiteSpace(xdg))
      xdg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
    return Path.Combine(xdg, ProductId);
  }

  public static string SettingsFile() =>
    Path.Combine(ConfigDirectory(), "settings.json");

  public static string SecretsFile() =>
    Path.Combine(ConfigDirectory(), "secrets.bin");

  public static string ReceiptsFile() =>
    Path.Combine(ConfigDirectory(), "receipts.json");

  public static string ObjectsDirectory() =>
    Path.Combine(DataDirectory(), "objects");

  public static string ArchiveDatabase() =>
    Path.Combine(DataDirectory(), "mail.db");

  public static string ModelsDirectory() =>
    Path.Combine(DataDirectory(), "models");

  public static string WebViewDirectory() =>
    Path.Combine(DataDirectory(), "webview");

  public static string LogsDirectory() {
    if (OperatingSystem.IsMacOS()) {
      return Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library",
        "Logs",
        ProductName);
    }

    return Path.Combine(DataDirectory(), "logs");
  }

  public static void EnsureDirectories() {
    Directory.CreateDirectory(ConfigDirectory());
    Directory.CreateDirectory(DataDirectory());
    Directory.CreateDirectory(ObjectsDirectory());
    Directory.CreateDirectory(ModelsDirectory());
    Directory.CreateDirectory(WebViewDirectory());
    Directory.CreateDirectory(LogsDirectory());
    Directory.CreateDirectory(Path.GetDirectoryName(ArchiveDatabase())!);
  }

  internal static string? PortableRoot() {
    var marker = Path.Combine(AppContext.BaseDirectory, "settings.json");
    if (!File.Exists(marker))
      return null;

    try {
      using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(marker));
      if (!document.RootElement.TryGetProperty("Configuration", out var configuration))
        return null;
      if (!configuration.TryGetProperty("installType", out var type))
        return null;
      if (!string.Equals(type.GetString(), "portable", StringComparison.OrdinalIgnoreCase))
        return null;
      return AppContext.BaseDirectory;
    }
    catch {
      return null;
    }
  }
}
