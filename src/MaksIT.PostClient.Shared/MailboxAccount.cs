using System.Text.Json.Serialization;


namespace MaksIT.PostClient.Shared;


public sealed class MailboxAccount {
  public string Id { get; set; } = Guid.NewGuid().ToString("N");

  public string DisplayName { get; set; } = "";

  public string Address { get; set; } = "";

  public string Username { get; set; } = "";

  public string IncomingProtocol { get; set; } = MailProtocol.Imap;

  public string StorePath { get; set; } = "";

  public string ArchiveStoreId { get; set; } = "";

  public string IncomingSecurity { get; set; } = MailSecurity.Ssl;

  public string ImapHost { get; set; } = "";

  public int ImapPort { get; set; } = 993;

  public bool ImapSsl { get; set; } = true;

  public string SmtpHost { get; set; } = "";

  public int SmtpPort { get; set; } = 465;

  public string SmtpSecurity { get; set; } = MailSecurity.Ssl;

  public bool SmtpSsl { get; set; } = true;

  public string Provider { get; set; } = MailProvider.Imap;

  public string CertifiedKind { get; set; } = MailCertifiedKind.Ordinary;

  public string AuthKind { get; set; } = MailAuthKind.Password;

  public bool InitialSyncCompleted { get; set; }

  [JsonIgnore]
  public bool TracksCertifiedReceipts =>
    MailCertifiedKind.TracksReceipts(CertifiedKind);

  [JsonIgnore]
  public string LoginName =>
    string.IsNullOrWhiteSpace(Username) ? Address.Trim() : Username.Trim();

  [JsonIgnore]
  public bool IsPstStore =>
    MailProtocol.IsPst(IncomingProtocol) || MailProvider.IsPst(Provider);

  [JsonIgnore]
  public bool IsLocalStore =>
    MailProtocol.IsStore(IncomingProtocol)
    || MailProvider.IsStore(Provider)
    || IsPstStore;

  [JsonIgnore]
  public string DataFile =>
    !string.IsNullOrWhiteSpace(StorePath) ? StorePath.Trim() : ImapHost.Trim();

  [JsonIgnore]
  public string Label =>
    !string.IsNullOrWhiteSpace(DisplayName)
      ? DisplayName.Trim()
      : !string.IsNullOrWhiteSpace(Address)
        ? Address.Trim()
        : !string.IsNullOrWhiteSpace(DataFile)
          ? Path.GetFileName(DataFile)
          : ImapHost;
}
