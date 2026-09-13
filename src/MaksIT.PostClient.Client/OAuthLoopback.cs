using System.Net;
using System.Net.Sockets;
using System.Text;
using MaksIT.Results;


namespace MaksIT.PostClient.Client;


internal sealed class OAuthLoopback : IDisposable {
  private readonly List<TcpListener> _listeners = [];

  public string RedirectUri { get; }

  private OAuthLoopback(string redirectUri) {
    RedirectUri = redirectUri;
  }

  public static OAuthLoopback Start(string host) {
    var v4 = new TcpListener(IPAddress.Loopback, 0);
    v4.Start();
    var port = ((IPEndPoint)v4.LocalEndpoint).Port;
    var loopback = new OAuthLoopback($"http://{host}:{port}/");
    loopback._listeners.Add(v4);
    try {
      var v6 = new TcpListener(IPAddress.IPv6Loopback, port);
      v6.Start();
      loopback._listeners.Add(v6);
    }
    catch (SocketException) {
    }

    return loopback;
  }

  public async Task<Result<string>> WaitForCodeAsync(
    string expectedState,
    CancellationToken cancellationToken) {
    using var cancel = cancellationToken.Register(Dispose);
    var accepts = _listeners.ToDictionary(
      listener => listener,
      listener => listener.AcceptTcpClientAsync(cancellationToken).AsTask());
    try {
      while (!cancellationToken.IsCancellationRequested) {
        var done = await Task.WhenAny(accepts.Values).ConfigureAwait(false);
        var owner = accepts.First(pair => pair.Value == done).Key;
        accepts[owner] = owner.AcceptTcpClientAsync(cancellationToken).AsTask();
        using var client = await done.ConfigureAwait(false);
        var stream = client.GetStream();
        var path = await ReadPathAsync(stream, cancellationToken).ConfigureAwait(false);
        var query = ParseQuery(path);
        var error = Value(query, "error_description") ?? Value(query, "error");
        var state = Value(query, "state") ?? "";
        var code = Value(query, "code") ?? "";
        if (string.IsNullOrWhiteSpace(error)
            && string.IsNullOrWhiteSpace(code)
            && string.IsNullOrWhiteSpace(state))
          continue;

        if (!string.IsNullOrWhiteSpace(error)
            || !state.Equals(expectedState, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(code)) {
          await WriteHtmlAsync(
            stream,
            "<html><body><p>Sign-in did not finish. You can close this window and check Postclient.</p></body></html>",
            CancellationToken.None).ConfigureAwait(false);
          Drop(client);
          if (!string.IsNullOrWhiteSpace(error))
            return Result<string>.UnprocessableEntity(null, error);
          if (!state.Equals(expectedState, StringComparison.Ordinal))
            return Result<string>.UnprocessableEntity(null, "OAuth state mismatch.");
          return Result<string>.UnprocessableEntity(null, "OAuth code is missing.");
        }

        await WriteHtmlAsync(
          stream,
          "<html><body><p>Returned to Postclient. Close this tab and look at Account Settings — the app confirms whether Gmail access was granted.</p></body></html>",
          CancellationToken.None).ConfigureAwait(false);
        Drop(client);
        return Result<string>.Ok(code);
      }

      return Result<string>.UnprocessableEntity(null, "Sign-in cancelled.");
    }
    catch (OperationCanceledException) {
      return Result<string>.UnprocessableEntity(null, "Sign-in cancelled.");
    }
    catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) {
      return Result<string>.UnprocessableEntity(null, "Sign-in cancelled.");
    }
    catch (SocketException) when (cancellationToken.IsCancellationRequested) {
      return Result<string>.UnprocessableEntity(null, "Sign-in cancelled.");
    }
    catch (Exception ex) {
      return Result<string>.UnprocessableEntity(null, ex.Message);
    }
  }

  private static void Drop(TcpClient client) {
    try {
      client.LingerState = new LingerOption(true, 0);
      client.Client.Close(0);
    }
    catch {
    }
  }

  public void Dispose() {
    foreach (var listener in _listeners) {
      try {
        listener.Stop();
      }
      catch {
      }
    }
  }

  private static async Task<string> ReadPathAsync(
    NetworkStream stream,
    CancellationToken cancellationToken) {
    var buffer = new byte[8192];
    var total = 0;
    while (total < buffer.Length) {
      var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false);
      if (read == 0)
        break;
      total += read;
      var text = Encoding.ASCII.GetString(buffer, 0, total);
      var headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
      var lf = headerEnd < 0;
      if (lf)
        headerEnd = text.IndexOf("\n\n", StringComparison.Ordinal);
      if (headerEnd < 0)
        continue;
      var lineEnd = lf ? text.IndexOf('\n') : text.IndexOf("\r\n", StringComparison.Ordinal);
      if (lineEnd < 0)
        return "/";
      var line = text[..lineEnd];
      var parts = line.Split(' ');
      return parts.Length >= 2 ? parts[1] : "/";
    }

    return "/";
  }

  private static async Task WriteHtmlAsync(
    NetworkStream stream,
    string html,
    CancellationToken cancellationToken) {
    var body = Encoding.UTF8.GetBytes(html);
    var header = Encoding.ASCII.GetBytes(
      "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: "
      + body.Length
      + "\r\nConnection: close\r\n\r\n");
    await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
    await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
  }

  private static Dictionary<string, string> ParseQuery(string path) {
    var map = new Dictionary<string, string>(StringComparer.Ordinal);
    var start = path.IndexOf('?');
    if (start < 0 || start == path.Length - 1)
      return map;
    var query = path[(start + 1)..];
    var hash = query.IndexOf('#');
    if (hash >= 0)
      query = query[..hash];
    foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries)) {
      var eq = part.IndexOf('=');
      var key = Decode(eq < 0 ? part : part[..eq]);
      if (string.IsNullOrEmpty(key))
        continue;
      map[key] = Decode(eq < 0 ? "" : part[(eq + 1)..]);
    }

    return map;
  }

  private static string? Value(Dictionary<string, string> query, string key) =>
    query.TryGetValue(key, out var value) ? value : null;

  private static string Decode(string value) =>
    Uri.UnescapeDataString(value.Replace('+', ' '));
}
