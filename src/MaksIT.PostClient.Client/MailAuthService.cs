using System.Diagnostics;
using System.Text.Json;
using MaksIT.Results;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public sealed class MailAuthService : IMailAuthService {
  private static readonly JsonSerializerOptions Json = new() {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
  };
  private readonly ISecretStore _secrets;
  private readonly ConfigurationFileService _files;

  public MailAuthService(ISecretStore secrets, ConfigurationFileService files) {
    _secrets = secrets;
    _files = files;
  }

  public async Task<Result<MailAuthMaterial>> ResolveAsync(
    MailboxAccount account,
    string? password,
    CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(account);
    var loaded = LoadTokens(account.Id);
    if (!loaded.IsSuccess)
      return FailMaterial(loaded);
    var useOauth = MailAuthKind.IsOAuth(account.AuthKind) || HasMailTokens(loaded.Value);
    if (!useOauth) {
      if (string.IsNullOrWhiteSpace(password))
        return Result<MailAuthMaterial>.BadRequest(null, "Password is required.");
      return Result<MailAuthMaterial>.Ok(new MailAuthMaterial { Password = password });
    }

    if (loaded.Value is null)
      return Result<MailAuthMaterial>.UnprocessableEntity(null, "Sign in with Google or Microsoft in Account Settings, and allow mail access.");

    var tokens = loaded.Value;
    var kind = MailAuthKind.IsOAuth(account.AuthKind)
      ? MailAuthKind.Normalize(account.AuthKind)
      : MailAuthKind.Normalize(tokens.Provider);
    if (tokens.AccessExpires <= DateTimeOffset.UtcNow.AddMinutes(2)) {
      if (string.IsNullOrWhiteSpace(tokens.RefreshToken))
        return Result<MailAuthMaterial>.UnprocessableEntity(null, "Sign in again. The access token expired.");
      var clientId = ClientId(kind);
      if (string.IsNullOrWhiteSpace(clientId))
        return Result<MailAuthMaterial>.UnprocessableEntity(null, MissingClientId(kind));
      var refreshed = await OAuthTokenClient.RefreshAsync(
        OAuthProviderProfile.For(kind),
        clientId,
        tokens,
        ClientSecret(kind),
        cancellationToken).ConfigureAwait(false);
      if (!refreshed.IsSuccess || refreshed.Value is null)
        return FailMaterial(refreshed);
      tokens = refreshed.Value;
      SaveTokens(account.Id, tokens);
    }

    if (string.IsNullOrWhiteSpace(tokens.AccessToken))
      return Result<MailAuthMaterial>.UnprocessableEntity(null, "Sign in again.");
    if (!string.IsNullOrWhiteSpace(tokens.Scope) && !OAuthMailScope.GrantsMail(kind, tokens.Scope))
      return Result<MailAuthMaterial>.UnprocessableEntity(null, OAuthMailScope.MissingMailMessage(kind));
    return Result<MailAuthMaterial>.Ok(new MailAuthMaterial {
      UseOAuth = true,
      AccessToken = tokens.AccessToken
    });
  }

  public async Task<Result<OAuthTokenSet>> SignInAsync(
    string authKind,
    string? loginHint = null,
    CancellationToken cancellationToken = default) {
    var kind = MailAuthKind.Normalize(authKind);
    if (!MailAuthKind.IsOAuth(kind))
      return Result<OAuthTokenSet>.BadRequest(null, "Choose Gmail or Outlook.");
    var clientId = OAuthClientId.Normalize(ClientId(kind));
    if (string.IsNullOrWhiteSpace(clientId))
      return Result<OAuthTokenSet>.UnprocessableEntity(null, MissingClientId(kind));
    if (kind == MailAuthKind.Google && !OAuthClientId.IsGoogle(clientId))
      return Result<OAuthTokenSet>.UnprocessableEntity(null, InvalidGoogleClientId());
    if (kind == MailAuthKind.Microsoft && !OAuthClientId.IsMicrosoft(clientId))
      return Result<OAuthTokenSet>.UnprocessableEntity(null, InvalidMicrosoftClientId());

    var profile = OAuthProviderProfile.For(kind);
    using var loopback = OAuthLoopback.Start(profile.LoopbackHost);
    var verifier = OAuthPkce.CreateVerifier();
    var state = OAuthPkce.CreateVerifier();
    var authorize = profile.AuthorizeUrl
      + "?response_type=code"
      + "&client_id=" + Uri.EscapeDataString(clientId)
      + "&redirect_uri=" + Uri.EscapeDataString(loopback.RedirectUri)
      + "&scope=" + Uri.EscapeDataString(profile.Scope)
      + "&state=" + Uri.EscapeDataString(state)
      + "&code_challenge=" + Uri.EscapeDataString(OAuthPkce.ChallengeS256(verifier))
      + "&code_challenge_method=S256";
    if (kind == MailAuthKind.Google)
      authorize += "&access_type=offline&prompt=select_account%20consent";
    var hint = OAuthLoginHint.For(kind, loginHint);
    if (!string.IsNullOrWhiteSpace(hint))
      authorize += "&login_hint=" + Uri.EscapeDataString(hint);

    try {
      Process.Start(new ProcessStartInfo { FileName = authorize, UseShellExecute = true });
    }
    catch (Exception ex) {
      return Result<OAuthTokenSet>.UnprocessableEntity(null, "Open the browser to sign in: " + ex.Message);
    }

    OAuthTrace.Write(kind + " wait " + OAuthClientId.Preview(clientId) + " " + loopback.RedirectUri);
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
    var code = await loopback.WaitForCodeAsync(state, linked.Token).ConfigureAwait(false);
    if (!code.IsSuccess || string.IsNullOrWhiteSpace(code.Value)) {
      OAuthTrace.Write("code failed " + string.Join(" ", code.Messages));
      if (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        return Result<OAuthTokenSet>.UnprocessableEntity(null, LoopbackTimeoutMessage(kind));
      return FailTokens(code);
    }

    OAuthTrace.Write("code received, exchanging token secret=" + (string.IsNullOrWhiteSpace(ClientSecret(kind)) ? "no" : "yes"));
    var tokens = await OAuthTokenClient.ExchangeCodeAsync(
      profile,
      clientId,
      loopback.RedirectUri,
      code.Value,
      verifier,
      ClientSecret(kind),
      CancellationToken.None).ConfigureAwait(false);
    if (!tokens.IsSuccess || tokens.Value is null) {
      OAuthTrace.Write("token failed " + string.Join(" ", tokens.Messages));
      return tokens;
    }
    tokens.Value.Provider = kind;
    var granted = tokens.Value.Scope;
    OAuthTrace.Write(
      "token ok email=" + tokens.Value.Email
      + " scope=" + (string.IsNullOrWhiteSpace(granted) ? "(empty)" : granted));
    if (!string.IsNullOrWhiteSpace(granted) && !OAuthMailScope.GrantsMail(kind, granted))
      return Result<OAuthTokenSet>.UnprocessableEntity(null, OAuthMailScope.MissingMailMessage(kind));
    return tokens;
  }

  public Result SaveTokens(string mailboxId, OAuthTokenSet tokens) {
    ArgumentNullException.ThrowIfNull(tokens);
    if (string.IsNullOrWhiteSpace(mailboxId))
      return Result.BadRequest("Mailbox is required.");
    return _secrets.Put(FileSecretStore.OAuthKey(mailboxId), JsonSerializer.Serialize(tokens, Json));
  }

  public Result DeleteTokens(string mailboxId) =>
    _secrets.Delete(FileSecretStore.OAuthKey(mailboxId));

  public bool HasTokens(string mailboxId) {
    var loaded = LoadTokens(mailboxId);
    return loaded.IsSuccess && HasMailTokens(loaded.Value);
  }

  private static bool HasMailTokens(OAuthTokenSet? tokens) =>
    tokens is not null
    && (!string.IsNullOrWhiteSpace(tokens.AccessToken) || !string.IsNullOrWhiteSpace(tokens.RefreshToken));

  private Result<OAuthTokenSet?> LoadTokens(string mailboxId) {
    var stored = _secrets.Get(FileSecretStore.OAuthKey(mailboxId));
    if (!stored.IsSuccess)
      return Result<OAuthTokenSet?>.UnprocessableEntity(null, string.Join(" ", stored.Messages));
    if (string.IsNullOrWhiteSpace(stored.Value))
      return Result<OAuthTokenSet?>.Ok(null);
    try {
      return Result<OAuthTokenSet?>.Ok(JsonSerializer.Deserialize<OAuthTokenSet>(stored.Value, Json));
    }
    catch (Exception ex) {
      return Result<OAuthTokenSet?>.UnprocessableEntity(null, ex.Message);
    }
  }

  private static Result<MailAuthMaterial> FailMaterial<T>(Result<T> step) =>
    Result<MailAuthMaterial>.UnprocessableEntity(null, string.Join(" ", step.Messages));

  private static Result<OAuthTokenSet> FailTokens<T>(Result<T> step) =>
    Result<OAuthTokenSet>.UnprocessableEntity(null, string.Join(" ", step.Messages));

  private string ClientId(string authKind) {
    var kind = MailAuthKind.Normalize(authKind);
    if (kind == MailAuthKind.Microsoft)
      return First(
        _files.Current.MicrosoftClientId,
        Environment.GetEnvironmentVariable("POSTCLIENT_MICROSOFT_CLIENT_ID"));
    return First(
      _files.Current.GoogleClientId,
      Environment.GetEnvironmentVariable("POSTCLIENT_GOOGLE_CLIENT_ID"));
  }

  private string ClientSecret(string authKind) {
    var kind = MailAuthKind.Normalize(authKind);
    var stored = _secrets.Get(FileSecretStore.OAuthClientSecretKey(kind));
    var fromStore = stored.IsSuccess ? stored.Value : null;
    if (kind == MailAuthKind.Microsoft)
      return First(fromStore, Environment.GetEnvironmentVariable("POSTCLIENT_MICROSOFT_CLIENT_SECRET"));
    return First(fromStore, Environment.GetEnvironmentVariable("POSTCLIENT_GOOGLE_CLIENT_SECRET"));
  }

  private static string LoopbackTimeoutMessage(string kind) =>
    kind == MailAuthKind.Microsoft
      ? "The browser never returned to Postclient. Check the Microsoft page for a redirect error. The Azure app must allow public-client loopback http://localhost."
      : "The browser never returned to Postclient. Check the Google page for redirect_uri_mismatch. The client must be type Desktop app in the same Cloud project where you added https://mail.google.com/, then paste that client ID into Account Settings.";

  private static string MissingClientId(string kind) =>
    kind == MailAuthKind.Microsoft
      ? "Paste a Microsoft public-client (desktop) Application ID in Account Settings, or set POSTCLIENT_MICROSOFT_CLIENT_ID."
      : "Paste a Google Desktop OAuth client ID in Account Settings, or set POSTCLIENT_GOOGLE_CLIENT_ID.";

  private static string InvalidGoogleClientId() =>
    "That is not a Google OAuth client ID. Paste the value that ends with .apps.googleusercontent.com from Google Cloud → Credentials (not your Gmail address).";

  private static string InvalidMicrosoftClientId() =>
    "That is not a Microsoft Application (client) ID. Paste the GUID from the Azure app registration (not the mailbox address).";

  private static string First(params string?[] values) {
    foreach (var value in values) {
      if (!string.IsNullOrWhiteSpace(value))
        return value.Trim();
    }

    return "";
  }
}
