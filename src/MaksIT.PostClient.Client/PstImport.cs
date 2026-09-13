using MimeKit;
using XstReader;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public static class PstImport {
  public static async Task<int> ImportAsync(
    LocalMailImport import,
    string mailboxId,
    string path,
    IMailSession? session,
    bool unwrap,
    CancellationToken cancellationToken) {
    using var file = new XstFile(path);
    return await WalkAsync(import, mailboxId, file.RootFolder, session, unwrap, cancellationToken)
      .ConfigureAwait(false);
  }

  private static async Task<int> WalkAsync(
    LocalMailImport import,
    string mailboxId,
    XstFolder folder,
    IMailSession? session,
    bool unwrap,
    CancellationToken cancellationToken) {
    var count = 0;
    var name = string.IsNullOrWhiteSpace(folder.DisplayName) ? "Imported" : folder.DisplayName;
    foreach (var message in folder.Messages) {
      cancellationToken.ThrowIfCancellationRequested();
      var eml = ToEml(message);
      if (eml.Length == 0)
        continue;
      if (await import.IngestAsync(mailboxId, name, eml, session, unwrap, cancellationToken).ConfigureAwait(false))
        count++;
    }

    foreach (var child in folder.Folders)
      count += await WalkAsync(import, mailboxId, child, session, unwrap, cancellationToken).ConfigureAwait(false);
    return count;
  }

  private static byte[] ToEml(XstMessage message) {
    var mime = new MimeMessage();
    mime.Subject = message.Subject ?? "";
    if (message.ReceivedTime is DateTime received)
      mime.Date = new DateTimeOffset(DateTime.SpecifyKind(received, DateTimeKind.Utc));
    TryAddMailbox(mime.From, message.Recipients.Sender);
    if (mime.From.Count == 0)
      TryAddFromLine(mime.From, message.From);
    foreach (var recipient in message.Recipients.To)
      TryAddMailbox(mime.To, recipient.DisplayName, recipient.Address);
    foreach (var recipient in message.Recipients.Cc)
      TryAddMailbox(mime.Cc, recipient.DisplayName, recipient.Address);
    var body = message.Body;
    if (body is not null) {
      if (body.Format == XstMessageBodyFormat.Html)
        mime.Body = new TextPart("html") { Text = body.Text ?? "" };
      else
        mime.Body = new TextPart("plain") { Text = body.Text ?? "" };
    }

    var extras = new List<MimeEntity>();
    foreach (var attachment in message.Attachments) {
      if (!attachment.IsFile)
        continue;
      var temp = Path.Combine(Path.GetTempPath(), "postclient-pst-" + Guid.NewGuid().ToString("N"));
      try {
        attachment.SaveToFile(temp);
        var bytes = File.ReadAllBytes(temp);
        extras.Add(new MimePart("application", "octet-stream") {
          FileName = attachment.FileName ?? "attachment",
          Content = new MimeContent(new MemoryStream(bytes)),
          ContentDisposition = new ContentDisposition(ContentDisposition.Attachment)
        });
      }
      catch {
      }
      finally {
        if (File.Exists(temp))
          File.Delete(temp);
      }
    }

    if (extras.Count > 0) {
      var mixed = new Multipart("mixed") { mime.Body ?? new TextPart("plain") { Text = "" } };
      foreach (var extra in extras)
        mixed.Add(extra);
      mime.Body = mixed;
    }

    using var buffer = new MemoryStream();
    mime.WriteTo(buffer);
    return buffer.ToArray();
  }

  private static void TryAddFromLine(InternetAddressList list, string? line) {
    if (string.IsNullOrWhiteSpace(line))
      return;
    try {
      list.AddRange(InternetAddressList.Parse(line));
    }
    catch {
      if (line.Contains('@', StringComparison.Ordinal))
        TryAddMailbox(list, "", line.Trim());
    }
  }

  private static void TryAddMailbox(InternetAddressList list, string? display, string? address) {
    if (string.IsNullOrWhiteSpace(address))
      return;
    try {
      list.Add(new MailboxAddress(display ?? "", address));
    }
    catch {
    }
  }

  private static void TryAddMailbox(InternetAddressList list, XstRecipient? recipient) {
    if (recipient is null)
      return;
    TryAddMailbox(list, recipient.DisplayName, recipient.Address);
  }
}
