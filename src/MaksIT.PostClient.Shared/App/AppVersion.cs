using System.Reflection;


namespace MaksIT.PostClient.Shared.App;


public static class AppVersion {
  public static Version Current() {
    var assembly = Assembly.GetEntryAssembly() ?? typeof(AppPaths).Assembly;
    var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    if (TryParse(informational, out var parsed))
      return parsed;
    return assembly.GetName().Version ?? new Version(0, 0, 0);
  }

  public static string Display() =>
    Format(Current());

  public static string Format(Version version) {
    ArgumentNullException.ThrowIfNull(version);
    if (version.Build < 0)
      return $"{version.Major}.{version.Minor}";
    if (version.Revision <= 0)
      return $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
    return version.ToString();
  }

  public static bool TryParse(string? value, out Version version) {
    version = new Version(0, 0, 0);
    var text = (value ?? "").Trim();
    if (text.Length == 0)
      return false;
    if (text.StartsWith('v') || text.StartsWith('V'))
      text = text[1..];
    var plus = text.IndexOf('+');
    if (plus >= 0)
      text = text[..plus];
    var dash = text.IndexOf('-');
    if (dash >= 0)
      text = text[..dash];
    text = text.Trim();
    if (Version.TryParse(text, out var parsed)) {
      version = parsed;
      return true;
    }

    return Version.TryParse(text + ".0", out version!);
  }

  public static bool IsNewer(string? latestTag, Version? installed = null) {
    if (!TryParse(latestTag, out var latest))
      return false;
    var current = installed ?? Current();
    return latest > Normalize(current);
  }

  private static Version Normalize(Version version) {
    var build = version.Build < 0 ? 0 : version.Build;
    var revision = version.Revision < 0 ? 0 : version.Revision;
    return new Version(version.Major, version.Minor, build, revision);
  }
}
