using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using MaksIT.PostClient.Shared;
using MaksIT.Results;


namespace MaksIT.PostClient.Client;


public sealed class AppUpdateCheck {
  public required GitHubRelease Release { get; init; }

  public required Version Installed { get; init; }

  public bool IsNewer { get; init; }

  public GitHubReleaseAsset? Asset { get; init; }
}


public interface IAppUpdateService {
  Task<Result<AppUpdateCheck>> CheckAsync(CancellationToken cancellationToken = default);

  Task<Result<string>> DownloadAsync(GitHubReleaseAsset asset, CancellationToken cancellationToken = default);

  bool TryLaunch(string path);
}


public sealed class AppUpdateService : IAppUpdateService, IDisposable {
  private readonly HttpClient _http;
  private readonly bool _ownsHttp;

  public AppUpdateService(HttpClient? http = null) {
    if (http is null) {
      _http = CreateClient();
      _ownsHttp = true;
    }
    else {
      _http = http;
    }
  }

  public async Task<Result<AppUpdateCheck>> CheckAsync(CancellationToken cancellationToken = default) {
    HttpResponseMessage response;
    try {
      using var request = new HttpRequestMessage(HttpMethod.Get, AppInstall.LatestApiUrl);
      response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
    catch (Exception ex) {
      return Result<AppUpdateCheck>.UnprocessableEntity(null, ex.Message);
    }

    using (response) {
      var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
      if (!response.IsSuccessStatusCode)
        return Result<AppUpdateCheck>.UnprocessableEntity(null, $"GitHub {(int)response.StatusCode}");

      var release = GitHubReleaseParser.Parse(body);
      if (release is null)
        return Result<AppUpdateCheck>.UnprocessableEntity(null, "Could not read the GitHub release.");

      var installed = AppVersion.Current();
      return Result<AppUpdateCheck>.Ok(new AppUpdateCheck {
        Release = release,
        Installed = installed,
        IsNewer = AppVersion.IsNewer(release.Tag, installed),
        Asset = GitHubReleaseParser.PickAsset(release, AppInstall.Detect(), RuntimeInformation.ProcessArchitecture)
      });
    }
  }

  public async Task<Result<string>> DownloadAsync(GitHubReleaseAsset asset, CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(asset);
    if (string.IsNullOrWhiteSpace(asset.Url))
      return Result<string>.UnprocessableEntity(null, "No download URL.");

    var folder = Path.Combine(Path.GetTempPath(), "Postclient", "updates");
    Directory.CreateDirectory(folder);
    var name = string.IsNullOrWhiteSpace(asset.Name) ? "postclient-update.bin" : Path.GetFileName(asset.Name);
    var path = Path.Combine(folder, name);
    try {
      using var response = await _http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
        .ConfigureAwait(false);
      if (!response.IsSuccessStatusCode)
        return Result<string>.UnprocessableEntity(null, $"Download {(int)response.StatusCode}");

      await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
      await using var output = File.Create(path);
      await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
      return Result<string>.Ok(path);
    }
    catch (Exception ex) {
      return Result<string>.UnprocessableEntity(null, ex.Message);
    }
  }

  public bool TryLaunch(string path) {
    if (string.IsNullOrWhiteSpace(path))
      return false;
    try {
      Process.Start(new ProcessStartInfo {
        FileName = path,
        UseShellExecute = true
      });
      return true;
    }
    catch {
      return false;
    }
  }

  public void Dispose() {
    if (_ownsHttp)
      _http.Dispose();
  }

  private static HttpClient CreateClient() {
    var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Postclient", AppVersion.Display()));
    http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    http.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
    return http;
  }
}
