using System.Globalization;


namespace MaksIT.PostClient.UI.ViewModels;


public sealed class SpamListItem {
  public string MailboxId { get; init; } = "";

  public string Fingerprint { get; init; } = "";

  public string From { get; init; } = "";

  public string Subject { get; init; } = "";

  public string When { get; init; } = "";

  public DateTimeOffset SortDate { get; init; }

  public bool Gone { get; init; }

  public string GoneText =>
    Gone ? UiLocale.Copy.SpamGone : "";

  public string Line =>
    string.IsNullOrWhiteSpace(Subject) ? UiLocale.Copy.NoSubject : Subject;

  public string NotSpamText =>
    UiLocale.Copy.MarkNotSpam;

  public string DeleteText =>
    UiLocale.Copy.Delete;

  public static DateTimeOffset ParseDate(string? raw) =>
    DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
      ? date
      : DateTimeOffset.MinValue;
}
