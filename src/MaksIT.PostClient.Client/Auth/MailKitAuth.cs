using MailKit;
using MailKit.Security;


namespace MaksIT.PostClient.Client.Auth;


internal static class MailKitAuth {
  public static Task AuthenticateAsync(
    MailService client,
    MailboxAccount account,
    MailAuthMaterial material,
    CancellationToken cancellationToken) {
    if (material.UseOAuth)
      return client.AuthenticateAsync(
        new SaslMechanismOAuth2(account.LoginName, material.AccessToken),
        cancellationToken);
    client.AuthenticationMechanisms.Remove("XOAUTH2");
    client.AuthenticationMechanisms.Remove("XOAUTH");
    return client.AuthenticateAsync(account.LoginName, material.Password, cancellationToken);
  }
}
