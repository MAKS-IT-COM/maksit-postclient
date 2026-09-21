using System.Globalization;
using System.Text.Json;
using MaksIT.IdentityHub.Client;
using MaksIT.IdentityHub.Contracts.Identity;
using MaksIT.Results;


namespace MaksIT.PostClient.Client.Auth;


public sealed class MailAuthService : IMailAuthService {
  private static readonly JsonSerializerOptions Json = new() {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true
  };
  private readonly ISecretStore _secrets;
  private readonly IIdentityHubClient _hub;

  public MailAuthService(ISecretStore secrets, IIdentityHubClient hub) {
    _secrets = secrets;
    _hub = hub;
  }

  public string DesktopLoginUrl(string authKind) =>
    IdentityHubAddress.DesktopLoginUrl(authKind);

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
      return Result<MailAuthMaterial>.UnprocessableEntity(
        null,
        "Sign in with Google or Microsoft in Account Settings (Identity Hub).");

    var tokens = loaded.Value;
    if (HasHubSession(tokens)) {
      try {
        return await RefreshHubMailboxAsync(account.Id, tokens, cancellationToken).ConfigureAwait(false);
      }
      catch (IdentityHubApiException ex) {
        var cached = CachedMailbox(tokens);
        if (cached is not null)
          return cached;
        return Result<MailAuthMaterial>.UnprocessableEntity(null, ex.Message);
      }
    }

    var leftover = CachedMailbox(tokens);
    if (leftover is not null)
      return leftover;
    return Result<MailAuthMaterial>.UnprocessableEntity(
      null,
      "Sign in again through Identity Hub.");
  }

  private async Task<Result<MailAuthMaterial>> RefreshHubMailboxAsync(
    string mailboxId,
    OAuthTokenSet tokens,
    CancellationToken cancellationToken) {
    if (!string.IsNullOrWhiteSpace(tokens.HubRefreshToken)) {
      var refreshed = await _hub.RefreshTokenAsync(
        new RefreshTokenRequest { RefreshToken = tokens.HubRefreshToken },
        cancellationToken).ConfigureAwait(false);
      tokens.HubToken = refreshed.Token;
      if (!string.IsNullOrWhiteSpace(refreshed.RefreshToken))
        tokens.HubRefreshToken = refreshed.RefreshToken;
      tokens.HubExpires = AsUtc(refreshed.ExpiresAt);
      if (!string.IsNullOrWhiteSpace(refreshed.Username))
        tokens.Email = refreshed.Username;
      SaveTokens(mailboxId, tokens);
    }
    else if (tokens.HubExpires <= DateTimeOffset.UtcNow.AddMinutes(2)) {
      var cached = CachedMailbox(tokens);
      if (cached is not null)
        return cached;
      return Result<MailAuthMaterial>.UnprocessableEntity(null, "Sign in again. The Hub session expired.");
    }

    _hub.AccessToken = tokens.HubToken;
    var mailbox = await _hub.GetMailboxAccessTokenAsync(cancellationToken).ConfigureAwait(false);
    if (string.IsNullOrWhiteSpace(mailbox.AccessToken))
      return Result<MailAuthMaterial>.UnprocessableEntity(null, "Identity Hub did not return a mailbox token.");
    tokens.AccessToken = mailbox.AccessToken;
    tokens.AccessExpires = DateTimeOffset.UtcNow.AddMinutes(50);
    SaveTokens(mailboxId, tokens);
    return Result<MailAuthMaterial>.Ok(new MailAuthMaterial {
      UseOAuth = true,
      AccessToken = mailbox.AccessToken
    });
  }

  private static Result<MailAuthMaterial>? CachedMailbox(OAuthTokenSet tokens) {
    if (string.IsNullOrWhiteSpace(tokens.AccessToken))
      return null;
    if (tokens.AccessExpires <= DateTimeOffset.UtcNow.AddMinutes(2))
      return null;
    return Result<MailAuthMaterial>.Ok(new MailAuthMaterial {
      UseOAuth = true,
      AccessToken = tokens.AccessToken
    });
  }

  public Task<Result<OAuthTokenSet>> SignInAsync(
    string authKind,
    string? loginHint = null,
    CancellationToken cancellationToken = default) {
    _ = loginHint;
    _ = cancellationToken;
    var kind = MailAuthKind.Normalize(authKind);
    if (!MailAuthKind.IsOAuth(kind))
      return Task.FromResult(Result<OAuthTokenSet>.BadRequest(null, "Choose Gmail or Outlook."));
    return Task.FromResult(Result<OAuthTokenSet>.UnprocessableEntity(
      null,
      "Open Identity Hub: " + DesktopLoginUrl(kind)));
  }

  public Result<OAuthTokenSet> CompleteHubSignIn(string json, string authKind) {
    var payloadJson = HubDesktopSession.Normalize(json);
    if (payloadJson is null)
      return Result<OAuthTokenSet>.BadRequest(null, "Identity Hub session is empty.");
    try {
      var payload = JsonSerializer.Deserialize<HubSession>(payloadJson, Json);
      if (payload is null || string.IsNullOrWhiteSpace(payload.Token))
        return Result<OAuthTokenSet>.UnprocessableEntity(null, "Identity Hub did not return a token.");
      if (string.IsNullOrWhiteSpace(payload.RefreshToken))
        return Result<OAuthTokenSet>.UnprocessableEntity(
          null,
          "Identity Hub did not return a refresh token. Sign in again.");
      return Result<OAuthTokenSet>.Ok(new OAuthTokenSet {
        Provider = MailAuthKind.Normalize(authKind),
        Email = payload.Username ?? "",
        HubToken = payload.Token,
        HubRefreshToken = payload.RefreshToken,
        HubExpires = ParseHubExpires(payload.ExpiresAt)
      });
    }
    catch (Exception ex) {
      return Result<OAuthTokenSet>.UnprocessableEntity(null, ex.Message);
    }
  }

  public async Task<Result<OAuthTokenSet>> CompleteHubSignInAsync(
    string json,
    string authKind,
    CancellationToken cancellationToken = default) {
    var parsed = CompleteHubSignIn(json, authKind);
    if (!parsed.IsSuccess || parsed.Value is null)
      return parsed;

    var tokens = parsed.Value;
    try {
      _hub.AccessToken = tokens.HubToken;
      var mailbox = await _hub.GetMailboxAccessTokenAsync(cancellationToken).ConfigureAwait(false);
      if (string.IsNullOrWhiteSpace(mailbox.AccessToken))
        return Result<OAuthTokenSet>.UnprocessableEntity(null, "Identity Hub did not return a mailbox token.");
      tokens.AccessToken = mailbox.AccessToken;
      tokens.AccessExpires = DateTimeOffset.UtcNow.AddMinutes(50);
      return Result<OAuthTokenSet>.Ok(tokens);
    }
    catch (IdentityHubApiException ex) {
      return Result<OAuthTokenSet>.UnprocessableEntity(null, ex.Message);
    }
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
    tokens is not null && (HasHubSession(tokens) || !string.IsNullOrWhiteSpace(tokens.AccessToken));

  private static bool HasHubSession(OAuthTokenSet tokens) =>
    !string.IsNullOrWhiteSpace(tokens.HubToken) || !string.IsNullOrWhiteSpace(tokens.HubRefreshToken);

  private static DateTimeOffset ParseHubExpires(string? value) {
    if (string.IsNullOrWhiteSpace(value))
      return DateTimeOffset.UtcNow.AddHours(1);
    if (DateTimeOffset.TryParse(
          value,
          CultureInfo.InvariantCulture,
          DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
          out var parsed))
      return parsed;
    return DateTimeOffset.UtcNow.AddHours(1);
  }

  private static DateTimeOffset AsUtc(DateTime value) {
    if (value.Kind == DateTimeKind.Utc)
      return new DateTimeOffset(value);
    if (value.Kind == DateTimeKind.Local)
      return new DateTimeOffset(value).ToUniversalTime();
    return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
  }

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

  private sealed class HubSession {
    public string? Token { get; set; }

    public string? RefreshToken { get; set; }

    public string? ExpiresAt { get; set; }

    public string? Username { get; set; }
  }
}
