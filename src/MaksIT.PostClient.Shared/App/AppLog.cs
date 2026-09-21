namespace MaksIT.PostClient.Shared.App;


public static class AppLog {
  private static readonly Lock Gate = new();

  public static string FilePath() =>
    Path.Combine(AppPaths.LogsDirectory(), "app.log");

  public static void Write(string message) {
    try {
      AppPaths.EnsureDirectories();
      var line = DateTimeOffset.UtcNow.ToString("u") + " " + (message ?? "").TrimEnd();
      lock (Gate)
        File.AppendAllText(FilePath(), line + Environment.NewLine);
    }
    catch {
    }
  }

  public static void Write(Exception exception) {
    if (exception is null)
      return;
    Write(ErrorReport.Format(exception));
  }
}
