using System.Runtime.InteropServices;
using System.Text.Json;


namespace MaksIT.PostClient.Shared;


public sealed class GitHubRelease {
  public string Tag { get; init; } = "";

  public string HtmlUrl { get; init; } = "";

  public string Notes { get; init; } = "";

  public IReadOnlyList<GitHubReleaseAsset> Assets { get; init; } = [];

  public string VersionText =>
    AppVersion.TryParse(Tag, out var version) ? AppVersion.Format(version) : Tag.TrimStart('v', 'V');
}


public sealed class GitHubReleaseAsset {
  public string Name { get; init; } = "";

  public string Url { get; init; } = "";

  public long Size { get; init; }
}


public static class GitHubReleaseParser {
  public static GitHubRelease? Parse(string json) {
    if (string.IsNullOrWhiteSpace(json))
      return null;

    try {
      using var document = JsonDocument.Parse(json);
      return Parse(document.RootElement);
    }
    catch (JsonException) {
      return null;
    }
  }

  public static GitHubRelease? Parse(JsonElement root) {
    if (root.ValueKind != JsonValueKind.Object)
      return null;
    if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
      return null;

    var tag = Text(root, "tag_name");
    if (string.IsNullOrWhiteSpace(tag))
      return null;

    var assets = new List<GitHubReleaseAsset>();
    if (root.TryGetProperty("assets", out var list) && list.ValueKind == JsonValueKind.Array) {
      foreach (var item in list.EnumerateArray()) {
        var name = Text(item, "name");
        var url = Text(item, "browser_download_url");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
          continue;
        assets.Add(new GitHubReleaseAsset {
          Name = name,
          Url = url,
          Size = item.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes) ? bytes : 0
        });
      }
    }

    return new GitHubRelease {
      Tag = tag,
      HtmlUrl = FirstNonEmpty(Text(root, "html_url"), AppInstall.ReleasesUrl),
      Notes = Text(root, "body"),
      Assets = assets
    };
  }

  public static GitHubReleaseAsset? FindNamed(GitHubRelease release, string fileName) {
    ArgumentNullException.ThrowIfNull(release);
    if (string.IsNullOrWhiteSpace(fileName))
      return null;
    foreach (var asset in release.Assets) {
      if (asset.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
        return asset;
    }

    return null;
  }

  public static GitHubReleaseAsset? PickAsset(
    GitHubRelease release,
    AppInstallKind kind,
    Architecture architecture) {
    ArgumentNullException.ThrowIfNull(release);
    var assets = release.Assets;
    return kind switch {
      AppInstallKind.WindowsSetup => Find(assets, ".exe"),
      AppInstallKind.Portable => Find(assets, ".zip"),
      AppInstallKind.Flatpak => Find(assets, ".flatpak"),
      AppInstallKind.MacApp => FindMac(assets, architecture),
      AppInstallKind.Unpackaged when OperatingSystem.IsWindows() => Find(assets, ".exe") ?? Find(assets, ".zip"),
      AppInstallKind.Unpackaged when OperatingSystem.IsLinux() => Find(assets, ".flatpak"),
      AppInstallKind.Unpackaged when OperatingSystem.IsMacOS() => FindMac(assets, architecture),
      _ => null
    };
  }

  public static bool RequiresExit(string path) {
    var name = Path.GetFileName(path) ?? "";
    return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
      || name.EndsWith(".flatpak", StringComparison.OrdinalIgnoreCase);
  }

  private static GitHubReleaseAsset? Find(IReadOnlyList<GitHubReleaseAsset> assets, string suffix) {
    foreach (var asset in assets) {
      if (asset.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
          && asset.Name.Contains("postclient", StringComparison.OrdinalIgnoreCase))
        return asset;
    }

    foreach (var asset in assets) {
      if (asset.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        return asset;
    }

    return null;
  }

  private static GitHubReleaseAsset? FindMac(IReadOnlyList<GitHubReleaseAsset> assets, Architecture architecture) {
    var rid = architecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
    foreach (var asset in assets) {
      if (asset.Name.EndsWith(".dmg", StringComparison.OrdinalIgnoreCase)
          && asset.Name.Contains(rid, StringComparison.OrdinalIgnoreCase))
        return asset;
    }

    return Find(assets, ".dmg");
  }

  private static string Text(JsonElement element, string name) {
    if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
      return "";
    return value.GetString()?.Trim() ?? "";
  }

  private static string FirstNonEmpty(string first, string second) =>
    string.IsNullOrWhiteSpace(first) ? second : first;
}
