using System.Globalization;


namespace MaksIT.PostClient.Shared.Mail;


public static class MailSize {
  public static string Line(long bytes) {
    if (bytes <= 0)
      return "";
    if (bytes < 1024)
      return bytes.ToString(CultureInfo.InvariantCulture) + " B";
    if (bytes < 1024 * 1024)
      return (bytes / 1024.0).ToString("0.#", CultureInfo.InvariantCulture) + " KB";
    if (bytes < 1024L * 1024 * 1024)
      return (bytes / (1024.0 * 1024)).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
    return (bytes / (1024.0 * 1024 * 1024)).ToString("0.##", CultureInfo.InvariantCulture) + " GB";
  }
}
