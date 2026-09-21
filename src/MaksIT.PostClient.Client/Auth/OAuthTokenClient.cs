using System.Net.Http;
using System.Text.Json;
using MaksIT.Results;


namespace MaksIT.PostClient.Client.Auth;


internal static class OAuthTokenClient {
  public static async Task<Result<OAuthTokenSet>> ExchangeCodeAsync(
    OAuthProviderProfile profile,
    string clientId,
    string redirectUri,
    string code,
    string verifier,
    string? clientSecret = null,
    CancellationToken cancellationToken = default) {
    var form = new Dictionary<string, string> {
      ["client_id"] = clientId,
      ["code"] = code,
      ["code_verifier"] = verifier,
      ["grant_type"] = "authorization_code",
      ["redirect_uri"] = redirectUri
    };
    AddSecret(form, clientSecret);
    return await RequestAsync(profile, form, cancellationToken).ConfigureAwait(false);
  }

  public static async Task<Result<OAuthTokenSet>> RefreshAsync(
    OAuthProviderProfile profile,
    string clientId,
    OAuthTokenSet current,
    string? clientSecret = null,
    CancellationToken cancellationToken = default) {
    var form = new Dictionary<string, string> {
      ["client_id"] = clientId,
      ["grant_type"] = "refresh_token",
      ["refresh_token"] = current.RefreshToken
    };
    AddSecret(form, clientSecret);
    var refreshed = await RequestAsync(profile, form, cancellationToken).ConfigureAwait(false);
    if (!refreshed.IsSuccess || refreshed.Value is null)
      return refreshed;
    if (string.IsNullOrWhiteSpace(refreshed.Value.RefreshToken))
      refreshed.Value.RefreshToken = current.RefreshToken;
    if (string.IsNullOrWhiteSpace(refreshed.Value.Email))
      refreshed.Value.Email = current.Email;
    if (string.IsNullOrWhiteSpace(refreshed.Value.Scope))
      refreshed.Value.Scope = current.Scope;
    refreshed.Value.Provider = current.Provider;
    return refreshed;
  }

  private static void AddSecret(Dictionary<string, string> form, string? clientSecret) {
    var secret = OAuthClientSecret.Normalize(clientSecret);
    if (!string.IsNullOrWhiteSpace(secret))
      form["client_secret"] = secret;
  }

  private static async Task<Result<OAuthTokenSet>> RequestAsync(
    OAuthProviderProfile profile,
    Dictionary<string, string> form,
    CancellationToken cancellationToken) {
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    using var content = new FormUrlEncodedContent(form);
    HttpResponseMessage response;
    try {
      response = await http.PostAsync(profile.TokenUrl, content, cancellationToken).ConfigureAwait(false);
    }
    catch (Exception ex) {
      return Result<OAuthTokenSet>.UnprocessableEntity(null, ex.Message);
    }

    using (response) {
      var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
      JsonDocument document;
      try {
        document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
      }
      catch (JsonException) {
        return Result<OAuthTokenSet>.UnprocessableEntity(null, "Token request failed.");
      }

      using (document) {
        var root = document.RootElement;
        if (!response.IsSuccessStatusCode) {
          var error = Text(root, "error_description") ?? Text(root, "error") ?? response.ReasonPhrase ?? "Token request failed.";
          return Result<OAuthTokenSet>.UnprocessableEntity(null, error);
        }

        var access = Text(root, "access_token");
        if (string.IsNullOrWhiteSpace(access))
          return Result<OAuthTokenSet>.UnprocessableEntity(null, "Access token is missing.");
        var expires = root.TryGetProperty("expires_in", out var seconds) && seconds.TryGetInt32(out var value)
          ? DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, value - 60))
          : DateTimeOffset.UtcNow.AddMinutes(50);
        var idToken = Text(root, "id_token");
        return Result<OAuthTokenSet>.Ok(new OAuthTokenSet {
          Provider = profile.Id,
          AccessToken = access,
          RefreshToken = Text(root, "refresh_token") ?? "",
          AccessExpires = expires,
          Email = OAuthPkce.EmailFromIdToken(idToken) ?? "",
          Scope = Text(root, "scope") ?? ""
        });
      }
    }
  }

  private static string? Text(JsonElement root, string name) {
    if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
      return null;
    return value.GetString();
  }
}
