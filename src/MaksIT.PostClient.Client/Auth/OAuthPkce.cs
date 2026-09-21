using System.Security.Cryptography;
using System.Text;
using System.Text.Json;


namespace MaksIT.PostClient.Client.Auth;


public static class OAuthPkce {
  public static string CreateVerifier() {
    Span<byte> bytes = stackalloc byte[32];
    RandomNumberGenerator.Fill(bytes);
    return Base64Url(bytes);
  }

  public static string ChallengeS256(string verifier) {
    ArgumentException.ThrowIfNullOrWhiteSpace(verifier);
    var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
    return Base64Url(hash);
  }

  public static string? EmailFromIdToken(string? idToken) {
    if (string.IsNullOrWhiteSpace(idToken))
      return null;
    var parts = idToken.Split('.');
    if (parts.Length < 2)
      return null;
    try {
      var json = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
      using var document = JsonDocument.Parse(json);
      if (document.RootElement.TryGetProperty("email", out var email) && email.GetString() is { Length: > 0 } value)
        return value;
      if (document.RootElement.TryGetProperty("preferred_username", out var name) && name.GetString() is { Length: > 0 } user)
        return user;
      if (document.RootElement.TryGetProperty("upn", out var upn) && upn.GetString() is { Length: > 0 } principal)
        return principal;
    }
    catch {
      return null;
    }

    return null;
  }

  public static string Base64Url(ReadOnlySpan<byte> bytes) =>
    Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

  public static byte[] Base64UrlDecode(string value) {
    var padded = value.Replace('-', '+').Replace('_', '/');
    switch (padded.Length % 4) {
      case 2:
        padded += "==";
        break;
      case 3:
        padded += "=";
        break;
    }

    return Convert.FromBase64String(padded);
  }
}
