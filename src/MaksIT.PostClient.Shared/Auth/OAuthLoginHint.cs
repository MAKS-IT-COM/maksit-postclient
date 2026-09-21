namespace MaksIT.PostClient.Shared.Auth;


public static class OAuthLoginHint {
  public static string? For(string? authKind, string? address) {
    var hint = (address ?? "").Trim();
    if (hint.Length == 0)
      return null;
    var kind = MailAuthKind.Normalize(authKind);
    if (kind == MailAuthKind.Microsoft)
      return hint;
    if (kind != MailAuthKind.Google)
      return null;
    if (hint.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase)
        || hint.EndsWith("@googlemail.com", StringComparison.OrdinalIgnoreCase))
      return hint;
    return null;
  }
}
