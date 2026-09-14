namespace MaksIT.PostClient.Shared;


public static class MailCertifiedKind {
  public const string Ordinary = "ordinary";
  public const string Pec = "pec";
  public const string Rem = "rem";

  public static string Normalize(string? value) {
    if (string.IsNullOrWhiteSpace(value))
      return Ordinary;
    return value.Trim().ToLowerInvariant() switch {
      Ordinary or "none" or "mail" => Ordinary,
      Pec or "pec-it" or "italian-pec" or "posta-certificata" => Pec,
      Rem or "eidas" or "eidas-rem" or "registered" => Rem,
      _ => Ordinary
    };
  }

  public static bool TracksReceipts(string? value) {
    var kind = Normalize(value);
    return kind is Pec or Rem;
  }

  public static string ForProvider(string? provider, string? current = null) {
    var id = MailProvider.Normalize(provider);
    if (id is MailProvider.Gmail or MailProvider.Outlook or MailProvider.Pst)
      return Ordinary;
    if (MailProvider.IsItalianPec(id))
      return Pec;
    if (MailProvider.IsRemPreset(id))
      return Rem;
    return Normalize(current);
  }
}
