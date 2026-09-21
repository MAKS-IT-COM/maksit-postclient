namespace MaksIT.PostClient.Shared.App;


public enum AppInstallKind {
  Unpackaged,
  Portable,
  WindowsSetup,
  Flatpak,
  MacApp
}


public static class AppInstall {
  public const string GitHubOwner = "MAKS-IT-COM";
  public const string GitHubRepo = "maksit-postclient";
  public const string ReleasesUrl = "https://github.com/MAKS-IT-COM/maksit-postclient/releases";
  public const string LatestApiUrl = "https://api.github.com/repos/MAKS-IT-COM/maksit-postclient/releases/latest";

  public static AppInstallKind Detect() =>
    Classify(
      AppContext.BaseDirectory,
      AppPaths.PortableRoot() is not null,
      Environment.GetEnvironmentVariable("FLATPAK_ID"),
      OperatingSystem.IsWindows(),
      OperatingSystem.IsMacOS(),
      Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
      Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));

  public static AppInstallKind Classify(
    string baseDirectory,
    bool portable,
    string? flatpakId,
    bool windows,
    bool macos,
    string? programFiles,
    string? programFilesX86) {
    if (portable)
      return AppInstallKind.Portable;
    if (!string.IsNullOrWhiteSpace(flatpakId))
      return AppInstallKind.Flatpak;
    var dir = baseDirectory ?? "";
    if (windows && IsProgramFilesProduct(dir, programFiles, programFilesX86))
      return AppInstallKind.WindowsSetup;
    if (macos && dir.Contains(".app", StringComparison.OrdinalIgnoreCase)
        && dir.Contains("Contents", StringComparison.OrdinalIgnoreCase))
      return AppInstallKind.MacApp;
    return AppInstallKind.Unpackaged;
  }

  private static bool IsProgramFilesProduct(string directory, string? programFiles, string? programFilesX86) {
    if (!directory.Contains("Postclient", StringComparison.OrdinalIgnoreCase))
      return false;
    return Under(directory, programFiles) || Under(directory, programFilesX86);
  }

  private static bool Under(string directory, string? root) =>
    !string.IsNullOrWhiteSpace(root)
    && directory.StartsWith(root, StringComparison.OrdinalIgnoreCase);
}
