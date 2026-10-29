using Avalonia.Controls;
using CoreWhatsNew = MaksIT.Core.UI.WhatsNew.WhatsNewWindow;


namespace MaksIT.PostClient.UI.Windows;

public static class WhatsNewWindow {
  public static Task ShowIfNeededAsync(Window owner, ConfigurationFileService files) {
    var cfg = files.Current;

    return CoreWhatsNew.ShowIfNeededAsync(
      owner,
      ReleaseNotes.Text(),
      AppVersion.Display(),
      cfg.WhatsNewSeenVersion,
      files.FileExisted,
      version => Remember(files, version));
  }

  private static void Remember(ConfigurationFileService files, string version) {
    var cfg = files.Current;

    if (string.Equals(cfg.WhatsNewSeenVersion, version, StringComparison.Ordinal))
      return;

    cfg.WhatsNewSeenVersion = version;
    files.Save(cfg);
  }
}
