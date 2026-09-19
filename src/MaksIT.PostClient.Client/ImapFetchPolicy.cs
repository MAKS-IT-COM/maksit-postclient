using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


internal static class ImapFetchPolicy {
  public static readonly string[] CertifiedHeaders = [
    "X-Trasporto", "X-Ricevuta", "X-TipoRicevuta", "X-Riferimento-Message-ID",
    "In-Reply-To", "References", "X-Priority", "Importance", "Priority"
  ];

  public static string[]? ExtraHeaders(string? provider) {
    var id = MailProvider.Normalize(provider);
    if (id is MailProvider.Gmail or MailProvider.Outlook)
      return null;
    return CertifiedHeaders;
  }

  public static bool PreferComplete(
    bool candidateHasEnvelope,
    bool currentHasEnvelope,
    bool candidateHasDate,
    bool currentHasDate) {
    if (candidateHasEnvelope != currentHasEnvelope)
      return candidateHasEnvelope;
    if (candidateHasDate != currentHasDate)
      return candidateHasDate;
    return true;
  }

  public static bool IsInboxPath(string? folder) =>
    MailFolderRole.Kind(null, folder) == "inbox";
}
