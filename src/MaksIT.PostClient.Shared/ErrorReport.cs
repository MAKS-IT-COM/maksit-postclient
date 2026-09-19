using System.Text;
using System.Runtime.InteropServices;


namespace MaksIT.PostClient.Shared;


/// <summary>Formats unhandled exceptions for a copyable dialog and a crash log file.</summary>
public static class ErrorReport {
  public static string Capture(Exception exception) {
    ArgumentNullException.ThrowIfNull(exception);
    var body = Format(exception);
    var path = TryWrite(body);
    if (string.IsNullOrWhiteSpace(path))
      return body;
    return body + Environment.NewLine + Environment.NewLine + "Log: " + path;
  }

  public static string Format(Exception exception) {
    ArgumentNullException.ThrowIfNull(exception);
    var text = new StringBuilder();
    text.AppendLine(AppInfo.ProductName + " " + AppVersion.Display());
    text.AppendLine(AppInfo.Brand);
    text.AppendLine(DateTimeOffset.UtcNow.ToString("u"));
    text.AppendLine(RuntimeInformation.OSDescription);
    text.AppendLine(RuntimeInformation.FrameworkDescription);
    text.AppendLine(Environment.OSVersion.VersionString);
    text.AppendLine((Environment.Is64BitProcess ? "64-bit" : "32-bit") + " process");
    text.AppendLine();
    AppendException(text, exception);
    return text.ToString().TrimEnd();
  }

  public static string? TryWrite(string report) {
    try {
      AppPaths.EnsureDirectories();
      var name = "crash-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Environment.ProcessId + ".txt";
      var path = Path.Combine(AppPaths.LogsDirectory(), name);
      File.WriteAllText(path, report ?? "");
      return path;
    }
    catch {
      return null;
    }
  }

  private static void AppendException(StringBuilder text, Exception exception) {
    var seen = new HashSet<Exception>();
    var current = exception;
    var depth = 0;
    while (current is not null && seen.Add(current)) {
      if (depth > 0)
        text.AppendLine().AppendLine("--- inner ---");
      text.AppendLine(current.GetType().FullName);
      text.AppendLine(current.Message);
      if (current.HResult != 0)
        text.AppendLine("HResult: 0x" + current.HResult.ToString("X8"));
      if (!string.IsNullOrWhiteSpace(current.Source))
        text.AppendLine("Source: " + current.Source);
      if (!string.IsNullOrWhiteSpace(current.TargetSite?.ToString()))
        text.AppendLine("Target: " + current.TargetSite);
      var stack = current.StackTrace;
      if (!string.IsNullOrWhiteSpace(stack))
        text.AppendLine(stack);
      if (current is AggregateException aggregate) {
        foreach (var inner in aggregate.InnerExceptions) {
          if (inner is null || ReferenceEquals(inner, current.InnerException) || !seen.Add(inner))
            continue;
          text.AppendLine().AppendLine("--- aggregate ---");
          text.AppendLine(inner.ToString());
        }
      }

      current = current.InnerException;
      depth++;
    }
  }
}
