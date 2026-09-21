namespace MaksIT.PostClient.Shared.Mail;


public static class MailFolderStats {
  public static string Line(string? name, int total, int unread, int shown = 0) {
    var folder = string.IsNullOrWhiteSpace(name) ? UiLocale.Copy.Folders : name.Trim();
    return UiLocale.Copy.StatsLine(folder, total, unread, shown);
  }
}
