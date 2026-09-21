using System.Diagnostics;
using System.Net.Http.Headers;
using MaksIT.Results;


namespace MaksIT.PostClient.Client.Search;


public sealed class EmbeddingModelDownloader : IDisposable {
  private readonly HttpClient _http;
  private readonly bool _ownsHttp;
  private readonly string _directory;

  public EmbeddingModelDownloader(HttpClient? http = null, string? directory = null) {
    _directory = directory ?? AppPaths.ModelsDirectory();
    if (http is null) {
      _http = CreateClient();
      _ownsHttp = true;
    }
    else {
      _http = http;
    }
  }

  public async Task<Result<string>> EnsureAsync(
    IProgress<EmbeddingDownloadProgress>? progress = null,
    CancellationToken cancellationToken = default) {
    Directory.CreateDirectory(_directory);
    if (EmbeddingModelSpec.FilesLookReady(_directory))
      return Result<string>.Ok(_directory);

    foreach (var file in EmbeddingModelSpec.Files) {
      var downloaded = await DownloadFileAsync(
        Path.Combine(_directory, file.FileName),
        file.Url,
        file.MinBytes,
        progress,
        file.Label,
        cancellationToken).ConfigureAwait(false);
      if (!downloaded.IsSuccess)
        return Result<string>.UnprocessableEntity(null, string.Join(" ", downloaded.Messages));
    }

    if (!EmbeddingModelSpec.FilesLookReady(_directory))
      return Result<string>.UnprocessableEntity(null, "The embedding model files are incomplete.");
    return Result<string>.Ok(_directory);
  }

  public void Dispose() {
    if (_ownsHttp)
      _http.Dispose();
  }

  private async Task<Result> DownloadFileAsync(
    string path,
    string url,
    long minBytes,
    IProgress<EmbeddingDownloadProgress>? progress,
    string label,
    CancellationToken cancellationToken) {
    if (File.Exists(path) && new FileInfo(path).Length >= minBytes)
      return Result.Ok();
    if (string.IsNullOrWhiteSpace(url))
      return Result.UnprocessableEntity("No download URL for " + label + ".");

    var part = path + ".part";
    var existing = File.Exists(part) ? new FileInfo(part).Length : 0L;
    using var request = new HttpRequestMessage(HttpMethod.Get, url);
    if (existing > 0)
      request.Headers.Range = new RangeHeaderValue(existing, null);

    HttpResponseMessage response;
    try {
      response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
        .ConfigureAwait(false);
    }
    catch (Exception ex) {
      return Result.UnprocessableEntity(ex.Message);
    }

    using (response) {
      if (response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable) {
        if (File.Exists(part))
          File.Delete(part);
        existing = 0;
      }
      else if (!response.IsSuccessStatusCode)
        return Result.UnprocessableEntity($"Download {label} {(int)response.StatusCode}");

      var total = response.Content.Headers.ContentLength;
      if (total is not null && existing > 0 && response.StatusCode == System.Net.HttpStatusCode.PartialContent)
        total += existing;
      await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
      await using var output = new FileStream(
        part,
        existing > 0 && response.StatusCode == System.Net.HttpStatusCode.PartialContent
          ? FileMode.Append
          : FileMode.Create,
        FileAccess.Write,
        FileShare.None);
      var buffer = new byte[64 * 1024];
      var received = existing;
      var lastReport = 0L;
      while (true) {
        var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (read <= 0)
          break;
        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        received += read;
        if (progress is null)
          continue;
        if (lastReport != 0 && Stopwatch.GetElapsedTime(lastReport).TotalMilliseconds < 250)
          continue;
        lastReport = Stopwatch.GetTimestamp();
        progress.Report(new EmbeddingDownloadProgress(label, received, total));
      }

      progress?.Report(new EmbeddingDownloadProgress(label, received, total));
    }

    if (!File.Exists(part) || new FileInfo(part).Length < minBytes) {
      if (File.Exists(part))
        File.Delete(part);
      return Result.UnprocessableEntity("Download " + label + " was truncated.");
    }

    if (File.Exists(path))
      File.Delete(path);
    File.Move(part, path);
    return Result.Ok();
  }

  private static HttpClient CreateClient() {
    var http = new HttpClient { Timeout = TimeSpan.FromMinutes(45) };
    http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Postclient", AppVersion.Display()));
    return http;
  }
}


public readonly record struct EmbeddingDownloadProgress(string Label, long Received, long? Total);
