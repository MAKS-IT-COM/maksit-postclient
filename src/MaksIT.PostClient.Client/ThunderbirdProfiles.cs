namespace MaksIT.PostClient.Client;


public static class ThunderbirdProfiles {
  public static IReadOnlyList<string> MailStores() {
    var found = new List<string>();
    foreach (var root in Roots()) {
      if (!Directory.Exists(root))
        continue;
      foreach (var profile in Directory.EnumerateDirectories(root)) {
        foreach (var mail in new[] { "Mail", "ImapMail" }) {
          var dir = Path.Combine(profile, mail);
          if (Directory.Exists(dir))
            found.Add(dir);
        }
      }
    }

    return found;
  }

  public static IReadOnlyList<string> MboxFiles(string directory) {
    if (!Directory.Exists(directory))
      return [];
    var files = new List<string>();
    foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)) {
      var name = Path.GetFileName(path);
      var ext = Path.GetExtension(path);
      if (name.EndsWith(".msf", StringComparison.OrdinalIgnoreCase))
        continue;
      if (name is "msgFilterRules.dat" or "filterlog.html" or "popstate.dat")
        continue;
      if (ext.Length > 0 && !ext.Equals(".mbox", StringComparison.OrdinalIgnoreCase))
        continue;
      files.Add(path);
    }

    return files;
  }

  private static IEnumerable<string> Roots() {
    if (OperatingSystem.IsWindows()) {
      yield return Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Thunderbird",
        "Profiles");
      yield break;
    }

    if (OperatingSystem.IsMacOS()) {
      yield return Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library",
        "Thunderbird",
        "Profiles");
      yield break;
    }

    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    yield return Path.Combine(home, ".thunderbird");
    yield return Path.Combine(home, ".icedove");
  }
}
