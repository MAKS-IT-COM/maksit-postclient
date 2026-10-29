using MaksIT.Core.Desktop.Snapshots;


namespace MaksIT.PostClient.Shared.App;

/// <summary>
/// Screenshot tour requested with <c>--screenshots</c>. Null when that flag is absent.
/// </summary>
public sealed class ScreenshotTourOptions {
  public const string InboxView = "inbox";

  public static readonly string[] DefaultViews = [InboxView];

  /// <summary>Windows opened after the inbox when <c>--views</c> is omitted.</summary>
  public static readonly string[] ToolViews = [
    "compose",
    "message",
    "accounts",
    "features",
    "rules",
    "search",
    "retention",
    "spam",
    "about",
    "logs"
  ];

  public required string Directory { get; init; }

  public string? Mailbox { get; init; }

  public string Language { get; init; } = UiLanguage.En;

  public IReadOnlyList<string> Views { get; init; } = DefaultViews;

  public bool AllFeatures { get; init; }

  public int SettleMilliseconds { get; init; } = 600;

  /// <summary>
  /// Reads <c>--screenshots</c>, <c>--mailbox</c>, <c>--language</c>, <c>--views</c>, and <c>--settle-ms</c>.
  /// Returns false when a screenshot flag is present but incomplete.
  /// With no <c>--views</c>, the tour is the inbox plus every tool window.
  /// With no <c>--language</c>, copy is English. Unknown ids fall back to English.
  /// The tour paints sample mail and does not open the saved mailbox.
  /// </summary>
  public static bool TryParse(IReadOnlyList<string>? args, out ScreenshotTourOptions? options, out string? error) {
    options = null;

    if (!AppViewSnapshotOptions.TryParse(args, out var snapshot, out error))
      return false;

    if (snapshot is null)
      return true;

    string? mailbox = null;
    string? language = null;

    if (args is not null) {
      for (var i = 0; i < args.Count; i++) {
        if (args[i] == "--mailbox") {
          if (!TryTake(args, ref i, "--mailbox", out mailbox, out error))
            return false;
        }
        else if (args[i] == "--language") {
          if (!TryTake(args, ref i, "--language", out language, out error))
            return false;
        }
      }
    }

    options = new ScreenshotTourOptions {
      Directory = snapshot.Directory,
      Mailbox = string.IsNullOrWhiteSpace(mailbox) ? null : mailbox,
      Language = UiLanguage.Normalize(language),
      Views = snapshot.Views.Count == 0 ? DefaultViews : snapshot.Views,
      AllFeatures = snapshot.Views.Count == 0,
      SettleMilliseconds = snapshot.SettleMilliseconds
    };

    return true;
  }

  private static bool TryTake(
    IReadOnlyList<string> args,
    ref int index,
    string flag,
    out string? value,
    out string? error) {
    value = null;
    error = null;
    var next = index + 1;

    if (next >= args.Count || args[next].StartsWith("--", StringComparison.Ordinal)) {
      error = $"Pass a value after {flag}.";

      return false;
    }

    index = next;
    value = args[next];

    return true;
  }
}
