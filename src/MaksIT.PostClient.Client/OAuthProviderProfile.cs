using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


internal sealed class OAuthProviderProfile {
  public required string Id { get; init; }

  public required string AuthorizeUrl { get; init; }

  public required string TokenUrl { get; init; }

  public required string Scope { get; init; }

  public required string LoopbackHost { get; init; }

  public static OAuthProviderProfile Google { get; } = new() {
    Id = "google",
    AuthorizeUrl = "https://accounts.google.com/o/oauth2/v2/auth",
    TokenUrl = "https://oauth2.googleapis.com/token",
    Scope = "openid email https://mail.google.com/",
    LoopbackHost = "127.0.0.1"
  };

  public static OAuthProviderProfile Microsoft { get; } = new() {
    Id = "microsoft",
    AuthorizeUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
    TokenUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/token",
    Scope = "offline_access openid email https://outlook.office.com/IMAP.AccessAsUser.All https://outlook.office.com/POP.AccessAsUser.All https://outlook.office.com/SMTP.Send",
    LoopbackHost = "localhost"
  };

  public static OAuthProviderProfile For(string? kind) {
    var id = MailAuthKind.Normalize(kind);
    return id == MailAuthKind.Microsoft ? Microsoft : Google;
  }
}
