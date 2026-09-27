namespace MaksIT.PostClient.Shared.Upgrades;


/// <summary>
/// Copies meaning-search models from the user profile into the machine models folder.
/// <see cref="InstallsOver"/> is the last release that stored models beside user data.
/// Delete this type once installs of that version are no longer supported.
/// </summary>
public static class UserModelsUpgrade {
  public static Version InstallsOver => new(0, 3, 8);

  public static void Apply() {
    var central = AppPaths.ModelsDirectory();
    var legacy = Path.Combine(AppPaths.DataDirectory(), "models");
    if (legacy.Equals(central, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(legacy))
      return;
    Directory.CreateDirectory(central);
    foreach (var file in Directory.GetFiles(legacy)) {
      var dest = Path.Combine(central, Path.GetFileName(file));
      if (File.Exists(dest))
        continue;
      try {
        File.Copy(file, dest);
      }
      catch {
      }
    }
  }
}
