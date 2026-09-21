using MaksIT.Results;


namespace MaksIT.PostClient.Client.Auth;


public sealed class MailAuthMaterial {
  public bool UseOAuth { get; init; }

  public string Password { get; init; } = "";

  public string AccessToken { get; init; } = "";
}


public interface IMailAuthService {
  Task<Result<MailAuthMaterial>> ResolveAsync(
    MailboxAccount account,
    string? password,
    CancellationToken cancellationToken = default);

  Task<Result<OAuthTokenSet>> SignInAsync(
    string authKind,
    string? loginHint = null,
    CancellationToken cancellationToken = default);

  Result SaveTokens(string mailboxId, OAuthTokenSet tokens);

  Result DeleteTokens(string mailboxId);

  bool HasTokens(string mailboxId);

  string DesktopLoginUrl(string authKind);

  Result<OAuthTokenSet> CompleteHubSignIn(string json, string authKind);

  Task<Result<OAuthTokenSet>> CompleteHubSignInAsync(
    string json,
    string authKind,
    CancellationToken cancellationToken = default);
}
