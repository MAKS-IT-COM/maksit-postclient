namespace MaksIT.PostClient.Shared.Auth;


public static class IdentityHubAddress {
  public const string Env = "POSTCLIENT_IDENTITY_HUB";
  public const string Production = "https://identity.maks-it.com";

  public static string Origin() {
    var fromEnv = Environment.GetEnvironmentVariable(Env);
    if (!string.IsNullOrWhiteSpace(fromEnv))
      return fromEnv.Trim().TrimEnd('/');
    return Production;
  }

  public static string DesktopLoginUrl(string authKind) {
    var provider = MailAuthKind.Normalize(authKind) == MailAuthKind.Microsoft
      ? "Microsoft"
      : "Google";
    return Origin() + "/desktop-login?provider=" + provider;
  }

  public static bool IsHubUrl(Uri? uri) {
    if (uri is null || uri.Scheme is not ("http" or "https"))
      return false;
    if (!Uri.TryCreate(Origin() + "/", UriKind.Absolute, out var origin))
      return false;
    return string.Equals(uri.Host, origin.Host, StringComparison.OrdinalIgnoreCase);
  }
}
