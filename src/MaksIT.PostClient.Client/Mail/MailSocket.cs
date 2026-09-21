using MailKit.Security;


namespace MaksIT.PostClient.Client.Mail;


internal static class MailSocket {
  public static SecureSocketOptions Incoming(MailboxAccount account) =>
    Parse(account.IncomingSecurity, account.ImapSsl);

  public static SecureSocketOptions Smtp(MailboxAccount account) =>
    Parse(account.SmtpSecurity, account.SmtpSsl);

  private static SecureSocketOptions Parse(string? security, bool legacySsl) {
    if (string.IsNullOrWhiteSpace(security))
      return legacySsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
    return MailSecurity.Normalize(security, legacySsl) switch {
      MailSecurity.None => SecureSocketOptions.None,
      MailSecurity.StartTls => SecureSocketOptions.StartTls,
      MailSecurity.StartTlsWhenAvailable => SecureSocketOptions.StartTlsWhenAvailable,
      MailSecurity.Auto => SecureSocketOptions.Auto,
      _ => SecureSocketOptions.SslOnConnect
    };
  }
}
