namespace MaksIT.PostClient.Shared.Mail;


public static class MailProvider {
  public const string Imap = "imap";
  public const string Pst = "pst";
  public const string Store = "store";
  public const string Gmail = "gmail";
  public const string Outlook = "outlook";
  public const string Aruba = "aruba";
  public const string Legalmail = "legalmail";
  public const string Namirial = "namirial";
  public const string Postecert = "postecert";
  public const string Register = "register";
  public const string Libero = "libero";
  public const string Intesi = "intesi";

  public static string Normalize(string? value) {
    if (string.IsNullOrWhiteSpace(value))
      return Imap;
    return value.Trim().ToLowerInvariant() switch {
      "gmail" or "google" => Gmail,
      "store" or "local-store" or "folder-store" or "bucket" => Store,
      "pst" or "ost" or "outlook-store" or "outlook-data" or "outlookpst" => Pst,
      "outlook" or "microsoft" or "office365" or "hotmail" or "live" => Outlook,
      "aruba" or "arubapec" or "pecaruba" => Aruba,
      "legalmail" or "infocert" or "tinexta" => Legalmail,
      "namirial" or "sicurezzapostale" => Namirial,
      "postecert" or "poste" or "posteitaliane" => Postecert,
      "register" or "registerpec" or "pec-email" => Register,
      "libero" or "liberopec" or "postacert" or "italiaonline" => Libero,
      "intesi" or "intesigroup" or "ig-trustmail" or "trustmail" => Intesi,
      _ => Imap
    };
  }

  public static bool UsesOAuth(string? value) {
    var id = Normalize(value);
    return id is Gmail or Outlook;
  }

  public static bool HasHostPreset(string? value) {
    var id = Normalize(value);
    return id is not Imap and not Pst and not Store;
  }

  public static bool IsPst(string? value) =>
    Normalize(value) == Pst;

  public static bool IsStore(string? value) =>
    Normalize(value) == Store;

  public static bool IsLocalBucket(string? value) {
    var id = Normalize(value);
    return id is Store or Pst;
  }

  public static bool IsPec(string? value) {
    var id = Normalize(value);
    return IsItalianPec(id) || IsRemPreset(id);
  }

  public static bool IsItalianPec(string? value) {
    var id = Normalize(value);
    return id is Aruba or Legalmail or Namirial or Postecert or Register or Libero;
  }

  public static bool IsRemPreset(string? value) =>
    Normalize(value) == Intesi;

  public static void Apply(MailboxAccount box) {
    ArgumentNullException.ThrowIfNull(box);
    var provider = Normalize(box.Provider);
    box.Provider = provider;
    box.CertifiedKind = MailCertifiedKind.ForProvider(provider, box.CertifiedKind);
    if (IsStore(provider) || IsPst(provider)) {
      box.IncomingProtocol = IsPst(provider) ? MailProtocol.Pst : MailProtocol.Store;
      box.Provider = IsPst(provider) ? Pst : Store;
      return;
    }

    box.IncomingProtocol = MailProtocol.NormalizeIncoming(box.IncomingProtocol);
    if (!TryHosts(provider, MailProtocol.IsPop3(box.IncomingProtocol), out var incoming, out var smtp))
      return;
    box.ImapHost = incoming;
    box.SmtpHost = smtp;
    box.IncomingSecurity = MailSecurity.Ssl;
    box.ImapPort = MailSecurity.DefaultIncomingPort(box.IncomingProtocol, MailSecurity.Ssl);
    if (provider == Outlook) {
      box.SmtpSecurity = MailSecurity.StartTls;
      box.SmtpPort = MailSecurity.DefaultSmtpPort(MailSecurity.StartTls);
      return;
    }

    box.SmtpSecurity = MailSecurity.Ssl;
    box.SmtpPort = MailSecurity.DefaultSmtpPort(MailSecurity.Ssl);
  }

  private static bool TryHosts(string provider, bool pop3, out string incoming, out string smtp) {
    incoming = "";
    smtp = "";
    switch (provider) {
      case Gmail:
        incoming = pop3 ? "pop.gmail.com" : "imap.gmail.com";
        smtp = "smtp.gmail.com";
        return true;
      case Outlook:
        incoming = "outlook.office365.com";
        smtp = "smtp.office365.com";
        return true;
      case Aruba:
        incoming = pop3 ? "pop3s.pec.aruba.it" : "imaps.pec.aruba.it";
        smtp = "smtps.pec.aruba.it";
        return true;
      case Legalmail:
        incoming = "mbox.cert.legalmail.it";
        smtp = "sendm.cert.legalmail.it";
        return true;
      case Namirial:
        incoming = pop3 ? "pops.sicurezzapostale.it" : "imaps.sicurezzapostale.it";
        smtp = "smtps.sicurezzapostale.it";
        return true;
      case Postecert:
        incoming = "mail.postecert.it";
        smtp = "mail.postecert.it";
        return true;
      case Register:
        incoming = "imap.pec-email.com";
        smtp = "smtp.pec-email.com";
        return true;
      case Libero:
        incoming = "mail.postacert.it.net";
        smtp = "mail.postacert.it.net";
        return true;
      case Intesi:
        incoming = pop3 ? "pop.ig-trustmail.com" : "imap.ig-trustmail.com";
        smtp = "smtp.ig-trustmail.com";
        return true;
      default:
        return false;
    }
  }
}
