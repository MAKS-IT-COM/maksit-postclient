using MaksIT.Core.Desktop;


namespace MaksIT.PostClient.Shared.App;

/// <summary>Formats unhandled exceptions for a copyable dialog and a crash log file.</summary>
public static class ErrorReport {
  public static string Capture(Exception exception) =>
    CrashReport.Capture(
      exception,
      AppInfo.ProductName + " " + AppVersion.Display(),
      AppInfo.Brand,
      AppPaths.LogsDirectory);

  public static string Format(Exception exception) =>
    CrashReport.Format(exception, AppInfo.ProductName + " " + AppVersion.Display(), AppInfo.Brand);
}
