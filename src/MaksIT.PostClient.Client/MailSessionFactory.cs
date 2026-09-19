using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public sealed class MailSessionFactory : IMailSessionFactory {
  private readonly IMailAuthService _auth;

  public MailSessionFactory(IMailAuthService auth) {
    _auth = auth;
  }

  public IMailSession Create(MailboxAccount account) {
    ArgumentNullException.ThrowIfNull(account);
    if (MailProtocol.IsStore(account.IncomingProtocol) || MailProvider.IsStore(account.Provider))
      return new LocalStoreSession();
    if (MailProtocol.IsPst(account.IncomingProtocol) || MailProvider.IsPst(account.Provider))
      return new PstMailSession();
    if (MailProtocol.IsPop3(account.IncomingProtocol))
      return new Pop3MailSession(_auth);
    return new ImapMailSession(_auth);
  }
}
