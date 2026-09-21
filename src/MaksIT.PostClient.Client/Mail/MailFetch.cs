namespace MaksIT.PostClient.Client.Mail;


public static class MailFetch {
  public static readonly TimeSpan BackfillTimeout = TimeSpan.FromSeconds(25);

  public const long MaxBackfillBytes = 8L * 1024 * 1024;
  public static bool IsGone(IEnumerable<string>? messages) {
    if (messages is null)
      return false;
    foreach (var message in messages) {
      if (IsGone(message))
        return true;
    }

    return false;
  }

  public static bool IsGone(string? message) {
    if (string.IsNullOrWhiteSpace(message))
      return false;
    return message.Contains("no such message", StringComparison.OrdinalIgnoreCase)
      || message.Contains("message not found", StringComparison.OrdinalIgnoreCase)
      || message.Contains("the unique id is invalid", StringComparison.OrdinalIgnoreCase)
      || message.Contains("uid is invalid", StringComparison.OrdinalIgnoreCase)
      || message.Contains("has been expunged", StringComparison.OrdinalIgnoreCase)
      || message.Contains("could not be found", StringComparison.OrdinalIgnoreCase);
  }
}
