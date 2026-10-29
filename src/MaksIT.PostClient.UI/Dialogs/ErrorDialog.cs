using MaksIT.Core.UI.Errors;
using CoreErrorDialog = MaksIT.Core.UI.Errors.ErrorDialog;


namespace MaksIT.PostClient.UI.Dialogs;

internal static class ErrorDialog {
  private static int _ready;

  public static void Report(Exception? exception) {
    Ensure();
    CoreErrorDialog.Report(exception);
  }

  public static void ReportBlocking(Exception? exception) {
    Ensure();
    CoreErrorDialog.ReportBlocking(exception);
  }

  private static void Ensure() {
    if (Interlocked.Exchange(ref _ready, 1) != 0)
      return;

    CoreErrorDialog.Use(new DesktopErrorOptions {
      Capture = ErrorReport.Capture,
      SuppressWindow = IsWorker,
      Text = () => {
        var copy = UiLocale.Copy;

        return new DesktopErrorText {
          Title = copy.ErrorTitle,
          Hint = copy.ErrorHint,
          CopyLabel = copy.CopyDetails,
          CloseLabel = copy.Close,
          CopiedLabel = copy.Copied
        };
      }
    });
  }

  private static bool IsWorker() {
    try {
      foreach (var arg in Environment.GetCommandLineArgs()) {
        if (string.Equals(arg, MailWorkerHost.Switch, StringComparison.OrdinalIgnoreCase))
          return true;
      }
    }
    catch {
    }

    return false;
  }
}
