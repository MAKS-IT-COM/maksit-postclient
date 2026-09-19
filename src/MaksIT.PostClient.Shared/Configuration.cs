namespace MaksIT.PostClient.Shared;


public sealed class Configuration {
  public string? installType { get; set; }

  public string? SelectedMailboxId { get; set; }

  public List<MailboxAccount> Mailboxes { get; set; } = [];

  public bool UnwrapEnvelope { get; set; }

  public string ReadingBodyKind { get; set; } = MailBodyKind.Html;

  public string ReadingLayout { get; set; } = MailLayout.Stacked;

  public LayoutSettings Layout { get; set; } = new();

  public bool GroupConversations { get; set; } = true;

  public string Language { get; set; } = "";

  public string GoogleClientId { get; set; } = "";

  public string MicrosoftClientId { get; set; } = "";

  public FeatureSettings Features { get; set; } = new();

  public SemanticSearchSettings Semantic { get; set; } = new();

  public List<MailRule> Rules { get; set; } = [];

  public List<FolderRetention> Retention { get; set; } = [];

  public void EnsureDefaults() {
    Mailboxes ??= [];
    foreach (var mailbox in Mailboxes) {
      if (string.IsNullOrWhiteSpace(mailbox.Id))
        mailbox.Id = Guid.NewGuid().ToString("N");
      mailbox.IncomingProtocol = MailProtocol.NormalizeIncoming(mailbox.IncomingProtocol);
      mailbox.IncomingSecurity = MailSecurity.Normalize(mailbox.IncomingSecurity, mailbox.ImapSsl);
      mailbox.SmtpSecurity = MailSecurity.Normalize(mailbox.SmtpSecurity, mailbox.SmtpSsl);
      if (mailbox.IsLocalStore && !mailbox.IsPstStore)
        mailbox.Provider = MailProvider.Store;
      if (mailbox.IsPstStore)
        mailbox.Provider = MailProvider.Pst;
      if (!mailbox.IsLocalStore && mailbox.ImapPort <= 0)
        mailbox.ImapPort = MailSecurity.DefaultIncomingPort(mailbox.IncomingProtocol, mailbox.IncomingSecurity);
      if (mailbox.SmtpPort <= 0)
        mailbox.SmtpPort = MailSecurity.DefaultSmtpPort(mailbox.SmtpSecurity);
      mailbox.ImapSsl = MailSecurity.IsImplicitTls(mailbox.IncomingSecurity);
      mailbox.SmtpSsl = MailSecurity.IsImplicitTls(mailbox.SmtpSecurity);
      mailbox.Provider = MailProvider.Normalize(mailbox.Provider);
      mailbox.CertifiedKind = MailCertifiedKind.ForProvider(mailbox.Provider, mailbox.CertifiedKind);
      mailbox.AuthKind = MailAuthKind.Normalize(mailbox.AuthKind);
    }

    ReadingBodyKind = MailBodyKind.Normalize(ReadingBodyKind);
    ReadingLayout = MailLayout.Normalize(ReadingLayout);
    Language = string.IsNullOrWhiteSpace(Language)
      ? UiLanguage.Detect()
      : UiLanguage.Normalize(Language);
    Layout ??= new LayoutSettings();
    Layout.Normalize();
    Features ??= new FeatureSettings();
    Features.Normalize();
    FeatureGate.Use(Features);
    Semantic ??= new SemanticSearchSettings();
    Semantic.Normalize();
    Rules ??= [];
    foreach (var rule in Rules) {
      rule.MailboxId ??= "";
      rule.FolderMailboxId ??= "";
      rule.Folder ??= "";
    }

    Retention ??= [];
    foreach (var row in Retention) {
      row.MailboxId ??= "";
      row.Folder ??= "";
      if (row.Days < 0)
        row.Days = 0;
    }
  }

  public MailboxAccount? FindMailbox(string? id) {
    if (string.IsNullOrWhiteSpace(id))
      return null;
    return Mailboxes.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
  }

  public MailboxAccount? FindMailboxByAddress(string? address) {
    if (string.IsNullOrWhiteSpace(address))
      return null;
    return Mailboxes.FirstOrDefault(m => m.Address.Equals(address.Trim(), StringComparison.OrdinalIgnoreCase));
  }
}
