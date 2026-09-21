using MimeKit;


namespace MaksIT.PostClient.Client.Mime;


public sealed class MailRecipient {
  public required string Address { get; init; }

  public string Name { get; init; } = "";

  public bool IsValid { get; init; } = true;

  public string Display =>
    string.IsNullOrWhiteSpace(Name) ? Address : Name.Trim();

  public string Tooltip =>
    string.IsNullOrWhiteSpace(Name) ? Address : $"{Name.Trim()} <{Address}>";
}


public static class RecipientParser {
  public static IReadOnlyList<MailRecipient> Parse(string? text) {
    if (string.IsNullOrWhiteSpace(text))
      return [];
    var trimmed = text.Trim().TrimEnd(',', ';');
    if (InternetAddressList.TryParse(trimmed, out var list) && list.Mailboxes.Any())
      return list.Mailboxes.Select(FromMailbox).ToList();

    var tags = new List<MailRecipient>();
    foreach (var part in trimmed.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
      if (InternetAddress.TryParse(part, out var parsed) && parsed is MailboxAddress mailbox)
        tags.Add(FromMailbox(mailbox));
      else if (part.Contains('@', StringComparison.Ordinal))
        tags.Add(new MailRecipient { Address = part, IsValid = true });
      else
        tags.Add(new MailRecipient { Address = part, IsValid = false });
    }

    return tags;
  }

  public static MailRecipient FromMailbox(MailboxAddress mailbox) =>
    new() {
      Address = mailbox.Address,
      Name = mailbox.Name ?? "",
      IsValid = mailbox.Address.Contains('@', StringComparison.Ordinal)
    };
}
