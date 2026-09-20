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

  public static string Left(int left) {
    var copy = UiLocale.Copy;
    return left <= 0 ? copy.IndexingBusy : string.Format(copy.IndexingLeft, left);
  }

  public static string MeaningLine(int done, int total) {
    var copy = UiLocale.Copy;
    return total <= 0 ? "" : string.Format(copy.IndexingMeaning, Math.Clamp(done, 0, total), total);
  }

  public static string MeaningLeft(int left) =>
    string.Format(UiLocale.Copy.IndexingMeaningLeft, Math.Max(0, left));

  public static string MeaningReady(string device, int done) {
    var copy = UiLocale.Copy;
    return done <= 0
      ? string.Format(copy.IndexingMeaningReady, device)
      : string.Format(copy.IndexingMeaningReadyCount, device, done);
  }
}
