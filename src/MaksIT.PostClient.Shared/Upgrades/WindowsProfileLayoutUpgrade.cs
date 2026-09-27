using System.Text.Json;


namespace MaksIT.PostClient.Shared.Upgrades;


/// <summary>
/// Moves <c>%LocalAppData%\Postclient</c> and <c>%AppData%\Postclient</c> under <c>MaksIT\Postclient</c>.
/// <see cref="InstallsOver"/> is the last release that still used those folders.
/// Delete this type once installs of that version are no longer supported.
/// </summary>
public static class WindowsProfileLayoutUpgrade {
  public static Version InstallsOver => new(0, 3, 2);

  public static void Apply() {
    if (!OperatingSystem.IsWindows() || !UsesDefaultUserLayout())
      return;

    try {
      ApplyWindows();
    }
    catch {
    }
  }

  internal static void AdoptSettingsIfEmpty(string source, string dest) {
    if (!File.Exists(source))
      return;
    if (!File.Exists(dest)) {
      CopyIfMissing(source, dest);
      return;
    }

    if (MailboxCount(dest) > 0 || MailboxCount(source) == 0)
      return;
    File.Copy(source, dest, overwrite: true);
  }

  internal static void MergeMissingDirectory(string source, string dest) {
    if (!Directory.Exists(source))
      return;
    Directory.CreateDirectory(dest);
    foreach (var file in Directory.GetFiles(source)) {
      var name = Path.GetFileName(file);
      if (name.Equals("settings.json", StringComparison.OrdinalIgnoreCase)
          || name.Equals("secrets.bin", StringComparison.OrdinalIgnoreCase)
          || name.EndsWith(".migrated", StringComparison.OrdinalIgnoreCase))
        continue;
      CopyIfMissing(file, Path.Combine(dest, name));
    }

    foreach (var dir in Directory.GetDirectories(source))
      MergeMissingDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
  }

  internal static int MailboxCount(string settingsPath) {
    try {
      using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
      if (!document.RootElement.TryGetProperty("Configuration", out var configuration))
        return 0;
      if (!configuration.TryGetProperty("Mailboxes", out var boxes)
          && !configuration.TryGetProperty("mailboxes", out boxes))
        return 0;
      return boxes.ValueKind == JsonValueKind.Array ? boxes.GetArrayLength() : 0;
    }
    catch {
      return 0;
    }
  }

  private static void ApplyWindows() {
    var dest = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      AppPaths.BrandFolder,
      AppPaths.ProductName);
    var oldLocal = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      AppPaths.ProductName);
    var oldRoam = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      AppPaths.ProductName);
    if (!Directory.Exists(dest) && Directory.Exists(oldLocal)) {
      try {
        Directory.Move(oldLocal, dest);
      }
      catch {
      }
    }

    Directory.CreateDirectory(dest);
    AdoptSettingsIfEmpty(Path.Combine(oldRoam, "settings.json"), Path.Combine(dest, "settings.json"));
    AdoptSettingsIfEmpty(Path.Combine(oldLocal, "settings.json"), Path.Combine(dest, "settings.json"));
    FileSecretStore.MergeMissingKeys(Path.Combine(oldRoam, "secrets.bin"), Path.Combine(dest, "secrets.bin"));
    FileSecretStore.MergeMissingKeys(Path.Combine(oldLocal, "secrets.bin"), Path.Combine(dest, "secrets.bin"));
    CopyIfMissing(Path.Combine(oldRoam, "receipts.json"), Path.Combine(dest, "receipts.json"));
    MergeMissingDirectory(oldLocal, dest);
    MergeMissingDirectory(Path.Combine(oldLocal, "webview"), Path.Combine(dest, "webview"));
  }

  private static bool UsesDefaultUserLayout() {
    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AppPaths.ConfigEnv)))
      return false;
    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AppPaths.DataEnv)))
      return false;
    return AppPaths.PortableRoot() is null;
  }

  private static void CopyIfMissing(string source, string dest) {
    if (!File.Exists(source) || File.Exists(dest))
      return;
    var dir = Path.GetDirectoryName(dest);
    if (!string.IsNullOrWhiteSpace(dir))
      Directory.CreateDirectory(dir);
    File.Copy(source, dest);
  }
}
