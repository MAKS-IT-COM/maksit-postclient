using System.Diagnostics;


namespace MaksIT.PostClient.UI;


public static class DesktopNotice {
  public static void Show(string title, string body) {
    try {
      if (OperatingSystem.IsWindows())
        Windows(title, body);
      else if (OperatingSystem.IsMacOS())
        Mac(title, body);
      else
        Linux(title, body);
    }
    catch {
    }
  }

  private static void Windows(string title, string body) {
    var xml = "<toast><visual><binding template=\"ToastGeneric\"><text>"
      + Esc(title) + "</text><text>" + Esc(body) + "</text></binding></visual></toast>";
    var script = "[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null; "
      + "[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null; "
      + "$xml = New-Object Windows.Data.Xml.Dom.XmlDocument; $xml.LoadXml('"
      + xml.Replace("'", "''")
      + "'); $toast = [Windows.UI.Notifications.ToastNotification]::new($xml); "
      + "[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Postclient').Show($toast)";
    Process.Start(new ProcessStartInfo {
      FileName = "powershell.exe",
      Arguments = "-NoProfile -STA -WindowStyle Hidden -Command " + script,
      UseShellExecute = false,
      CreateNoWindow = true
    })?.Dispose();
  }

  private static void Mac(string title, string body) {
    Process.Start(new ProcessStartInfo {
      FileName = "osascript",
      Arguments = "-e \"display notification \\\"" + Esc(body) + "\\\" with title \\\"" + Esc(title) + "\\\"\"",
      UseShellExecute = false
    })?.Dispose();
  }

  private static void Linux(string title, string body) {
    Process.Start(new ProcessStartInfo {
      FileName = "notify-send",
      Arguments = "\"" + Esc(title) + "\" \"" + Esc(body) + "\"",
      UseShellExecute = false
    })?.Dispose();
  }

  private static string Esc(string value) =>
    value.Replace("\"", "'").Replace("<", "").Replace(">", "");
}
