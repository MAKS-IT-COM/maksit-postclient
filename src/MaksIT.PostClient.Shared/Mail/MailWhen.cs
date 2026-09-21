using System.Globalization;


namespace MaksIT.PostClient.Shared.Mail;


public static class MailWhen {
  public const string Pattern = "yyyy-MM-dd HH:mm";

  public static string Line(DateTimeOffset date) {
    if (date == DateTimeOffset.MinValue)
      return "";
    return date.ToLocalTime().ToString(Pattern, CultureInfo.InvariantCulture);
  }
}
