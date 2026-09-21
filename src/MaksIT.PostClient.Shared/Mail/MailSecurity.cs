namespace MaksIT.PostClient.Shared.Mail;


public static class MailSecurity {
  public const string Auto = "auto";
  public const string Ssl = "ssl";
  public const string StartTls = "starttls";
  public const string StartTlsWhenAvailable = "starttls-available";
  public const string None = "none";

  public static string Normalize(string? value, bool legacySsl = true) {
    if (string.IsNullOrWhiteSpace(value))
      return legacySsl ? Ssl : StartTlsWhenAvailable;
    return value.Trim().ToLowerInvariant() switch {
      "none" or "off" or "plain" => None,
      "starttls" or "tls" => StartTls,
      "starttls-available" or "starttlswhenavailable" => StartTlsWhenAvailable,
      "auto" => Auto,
      _ => Ssl
    };
  }

  public static bool IsImplicitTls(string? value) =>
    Normalize(value) == Ssl;

  public static int DefaultIncomingPort(string? protocol, string? security) {
    var tls = Normalize(security);
    if (MailProtocol.IsPop3(protocol))
      return tls == Ssl || tls == Auto ? 995 : 110;
    return tls == Ssl || tls == Auto ? 993 : 143;
  }

  public static int DefaultSmtpPort(string? security) {
    var tls = Normalize(security);
    if (tls == Ssl)
      return 465;
    if (tls == None)
      return 25;
    return 587;
  }

  public static bool IsDefaultIncomingPort(int port) =>
    port is 110 or 143 or 993 or 995;

  public static bool IsDefaultSmtpPort(int port) =>
    port is 25 or 465 or 587;
}
