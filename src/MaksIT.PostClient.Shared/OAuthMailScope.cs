namespace MaksIT.PostClient.Shared;


public static class OAuthMailScope {
  public const string GoogleMail = "https://mail.google.com/";
  public const string MicrosoftImap = "IMAP.AccessAsUser.All";
  public const string MicrosoftSmtp = "SMTP.Send";

  public static bool GrantsMail(string? authKind, string? granted) {
    if (string.IsNullOrWhiteSpace(granted))
      return false;
    var kind = MailAuthKind.Normalize(authKind);
    if (kind == MailAuthKind.Microsoft)
      return Contains(granted, MicrosoftImap) || Contains(granted, MicrosoftSmtp);
    return Contains(granted, "mail.google.com");
  }

  public static string MissingMailMessage(string? authKind) {
    if (MailAuthKind.Normalize(authKind) == MailAuthKind.Microsoft)
      return "Microsoft signed you in but did not grant IMAP/SMTP. Add IMAP.AccessAsUser.All and SMTP.Send on the Azure app, then Sign in again.";
    return "Google signed you in but did not grant Gmail. If the browser named a different app and only asked for email, the client ID in Account Settings is from the wrong Cloud project. Paste a Desktop client ID from the project that has https://mail.google.com/, then Sign in again and allow Gmail (read/compose/delete), not only email.";
  }

  private static bool Contains(string granted, string fragment) =>
    granted.Contains(fragment, StringComparison.OrdinalIgnoreCase);
}
