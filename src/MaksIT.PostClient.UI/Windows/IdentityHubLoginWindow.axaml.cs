using Avalonia.Controls;
using Avalonia.Threading;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.UI.Windows;


public partial class IdentityHubLoginWindow : Window {
  private const string ReadSessionScript = """
    (function () {
      var keySession = 'identityHubDesktopSession';
      function payload() {
        return sessionStorage.getItem(keySession) || '';
      }
      function notify() {
        var json = payload();
        if (!json)
          return json;
        try {
          if (typeof invokeCSharpAction === 'function')
            invokeCSharpAction(json);
        } catch (e) { }
        return json;
      }
      if (!window.__maksitHubDesktopHook) {
        window.__maksitHubDesktopHook = true;
        var orig = sessionStorage.setItem.bind(sessionStorage);
        sessionStorage.setItem = function (key, value) {
          orig(key, value);
          if (key === keySession)
            notify();
        };
      }
      return notify();
    })()
    """;

  private readonly string _url;
  private readonly DispatcherTimer _poll;
  private bool _done;
  private bool _reading;
  private bool _started;

  public IdentityHubLoginWindow() : this(IdentityHubAddress.Production) {
  }

  public IdentityHubLoginWindow(string url) {
    _url = url;
    InitializeComponent();
    _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
    _poll.Tick += (_, _) => _ = TryCompleteFromWebViewAsync();
    HubWeb.EnvironmentRequested += (_, args) => WebViewSetup.Apply(args, identityHub: true);
    HubWeb.AdapterCreated += (_, _) => LoadHub(force: false);
    HubWeb.NavigationCompleted += (_, _) => _ = TryCompleteFromWebViewAsync();
    HubWeb.WebMessageReceived += OnWebMessage;
    HubWeb.NewWindowRequested += OnNewWindow;
    Opened += OnOpened;
    Closed += (_, _) => _poll.Stop();
    LoadHub(force: true);
  }

  private void OnOpened(object? sender, EventArgs e) {
    LoadHub(force: false);
    StatusText.Text = "Waiting for Identity Hub… keep this window open until it closes itself.";
    _poll.Start();
  }

  private void LoadHub(bool force) {
    if (_done)
      return;
    if (!Uri.TryCreate(_url, UriKind.Absolute, out var target)) {
      StatusText.Text = "Unable to open Identity Hub.";
      return;
    }

    if (!force && HubWeb.Source is { } current && !IsEmptyDocument(current))
      return;
    try {
      HubWeb.Source = target;
      _started = true;
    }
    catch {
      if (!_started)
        StatusText.Text = "Unable to open Identity Hub.";
    }
  }

  private void OnNewWindow(object? sender, WebViewNewWindowRequestedEventArgs e) {
    if (e.Request is null || IsEmptyDocument(e.Request))
      return;
    e.Handled = true;
    HubWeb.Source = e.Request;
  }

  private static bool IsEmptyDocument(Uri uri) =>
    uri.Scheme is "about" or "blob" or "data"
    || string.Equals(uri.AbsoluteUri, "about:blank", StringComparison.OrdinalIgnoreCase);

  private void OnWebMessage(object? sender, WebMessageReceivedEventArgs e) =>
    TryFinish(e.Body);

  private async Task TryCompleteFromWebViewAsync() {
    if (_done || _reading || !IdentityHubAddress.IsHubUrl(HubWeb.Source))
      return;
    _reading = true;
    try {
      TryFinish(await HubWeb.InvokeScript(ReadSessionScript));
    }
    catch {
    }
    finally {
      _reading = false;
    }
  }

  private void TryFinish(string? raw) {
    if (_done)
      return;
    var json = HubDesktopSession.Normalize(raw);
    if (json is null || !HubDesktopSession.IsComplete(json))
      return;
    _done = true;
    _poll.Stop();
    Close(json);
  }
}
