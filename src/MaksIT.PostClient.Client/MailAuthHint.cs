using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


internal static class MailAuthHint {
  public static string Incoming(MailboxAccount account, string message) {
    var prefix = MailProtocol.IsPop3(account.IncomingProtocol) ? "POP3: " : "IMAP: ";
    return prefix + message + Suffix(account);
  }

  public static string Smtp(MailboxAccount account, string message) =>
    "SMTP: " + message + Suffix(account);

  private static string Suffix(MailboxAccount account) {
    var provider = MailProvider.Normalize(account.Provider);
    if (provider == MailProvider.Gmail)
      return " Enable IMAP in Gmail (Settings → See all settings → Forwarding and POP/IMAP). Sign in must allow Gmail mail access, not only email.";
    if (provider == MailProvider.Outlook)
      return " Sign in must allow IMAP and SMTP on the Azure app.";
    if (account.TracksCertifiedReceipts || MailProvider.IsPec(provider))
      return " Username is usually the full certified address; Legalmail may use the InfoCert User ID.";
    return "";
  }
}
