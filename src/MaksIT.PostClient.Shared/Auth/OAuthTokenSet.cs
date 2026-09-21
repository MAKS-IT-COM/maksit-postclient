namespace MaksIT.PostClient.Shared.Auth;


public sealed class OAuthTokenSet {
  public string Provider { get; set; } = "";

  public string Email { get; set; } = "";

  public string HubToken { get; set; } = "";

  public string HubRefreshToken { get; set; } = "";

  public DateTimeOffset HubExpires { get; set; }

  public string AccessToken { get; set; } = "";

  public string RefreshToken { get; set; } = "";

  public DateTimeOffset AccessExpires { get; set; }

  public string Scope { get; set; } = "";
}
