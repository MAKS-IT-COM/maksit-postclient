namespace MaksIT.PostClient.Shared.App;


/// <summary>
/// Machine folder shared by every account and the sync service.
/// ProgramData on Windows, /var/lib on Linux, and /Users/Shared on macOS.
/// </summary>
public static class SharedMailPaths {
  public const string Env = "POSTCLIENT_SHARED_DIR";

  private static string? _windowsRoot;

  public static string Root() {
    var fromEnv = Environment.GetEnvironmentVariable(Env);
    if (!string.IsNullOrWhiteSpace(fromEnv))
      return Path.GetFullPath(fromEnv.Trim());
    if (OperatingSystem.IsWindows())
      return WindowsRoot();
    if (OperatingSystem.IsMacOS())
      return Path.Combine("/Users/Shared", AppPaths.BrandFolder, AppPaths.ProductName);
    return Path.Combine("/var/lib", "maksit", AppPaths.ProductId);
  }

  public static string AccountsDirectory() =>
    Path.Combine(Root(), "accounts");

  public static string AccountDirectory(string mailboxId) =>
    Path.Combine(AccountsDirectory(), mailboxId);

  public static string AccountFile(string mailboxId) =>
    Path.Combine(AccountDirectory(mailboxId), "account.json");

  private static string WindowsRoot() {
    if (_windowsRoot is not null)
      return _windowsRoot;
    var programData = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
      AppPaths.BrandFolder,
      AppPaths.ProductName);
    if (CanCreate(programData)) {
      _windowsRoot = programData;
      return programData;
    }

    var shared = Path.GetFullPath(Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
      "..",
      AppPaths.BrandFolder,
      AppPaths.ProductName));
    _windowsRoot = shared;
    return shared;
  }

  private static bool CanCreate(string path) {
    try {
      Directory.CreateDirectory(path);
      var probe = Path.Combine(path, ".write-probe");
      File.WriteAllText(probe, "");
      File.Delete(probe);
      return true;
    }
    catch {
      return false;
    }
  }
}
