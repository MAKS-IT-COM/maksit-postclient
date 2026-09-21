namespace MaksIT.PostClient.Shared.Mail;


public sealed class MailboxQuota {
  public string Label { get; init; } = "";

  public string Host { get; init; } = "";

  public uint? UsedKb { get; init; }

  public uint? LimitKb { get; init; }

  public int? Percent {
    get {
      if (UsedKb is not uint used || LimitKb is not uint limit || limit == 0)
        return null;
      return (int)Math.Min(100, Math.Round(used * 100.0 / limit));
    }
  }

  public string Line() {
    var name = string.IsNullOrWhiteSpace(Label) ? Host : Label;
    if (Percent is int pct && LimitKb is uint limit && UsedKb is uint used)
      return name + " " + pct + "% — " + FormatGb(used) + " of " + FormatGb(limit)
        + (string.IsNullOrWhiteSpace(Host) ? "" : " (" + Host + ")");
    if (UsedKb is uint only)
      return name + " — " + FormatGb(only) + " used"
        + (string.IsNullOrWhiteSpace(Host) ? "" : " on " + Host);
    return "";
  }

  internal static string FormatGb(uint kb) {
    var gb = kb / (1024.0 * 1024.0);
    if (gb >= 0.1)
      return gb.ToString("0.0") + " GB";
    var mb = kb / 1024.0;
    return mb.ToString("0") + " MB";
  }
}
