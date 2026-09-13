namespace MaksIT.PostClient.Shared;


public static class MailIndexProgress {
  public static string Line(int done, int total, string? folder = null) {
    var copy = UiLocale.Copy;
    if (total <= 0)
      return string.IsNullOrWhiteSpace(folder) ? "" : copy.IndexScan(folder);
    return string.IsNullOrWhiteSpace(folder)
      ? copy.IndexLine(done, total)
      : copy.IndexLine(done, total, folder);
  }
}
