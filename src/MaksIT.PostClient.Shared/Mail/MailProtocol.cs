namespace MaksIT.PostClient.Shared.Mail;


public static class MailProtocol {
  public const string Imap = "imap";
  public const string Pop3 = "pop3";
  public const string Pst = "pst";
  public const string Store = "store";
  public const string Smtp = "smtp";

  public static bool IsPop3(string? value) =>
    NormalizeIncoming(value) == Pop3;

  public static bool IsPst(string? value) =>
    NormalizeIncoming(value) == Pst;

  public static bool IsStore(string? value) =>
    NormalizeIncoming(value) == Store;

  public static bool IsLocalBucket(string? value) {
    var id = NormalizeIncoming(value);
    return id is Store or Pst;
  }

  public static string NormalizeIncoming(string? value) {
    if (string.IsNullOrWhiteSpace(value))
      return Imap;
    var trimmed = value.Trim().ToLowerInvariant();
    if (trimmed is "store" or "local-store" or "folder-store" or "bucket")
      return Store;
    if (trimmed is "pst" or "ost" or "outlook-store" or "outlook-data")
      return Pst;
    if (trimmed.StartsWith("pop", StringComparison.Ordinal))
      return Pop3;
    return Imap;
  }
}
