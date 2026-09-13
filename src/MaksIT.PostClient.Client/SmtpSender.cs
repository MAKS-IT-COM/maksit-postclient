using MailKit.Net.Smtp;
using MimeKit;
using MaksIT.Results;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


internal static class SmtpSender {
  public static async Task<Result<string>> SendAsync(
    MailboxAccount account,
    MailAuthMaterial material,
    MailSendRequest request,
    CancellationToken cancellationToken) {
    if (string.IsNullOrWhiteSpace(account.SmtpHost))
      return Result<string>.BadRequest(null, "SMTP host is required.");
    if (request.To.Count == 0)
      return Result<string>.BadRequest(null, "To is required.");

    try {
      var mime = new MimeMessage();
      mime.From.Add(MailboxAddress.Parse(string.IsNullOrWhiteSpace(request.From) ? account.Address : request.From));
      AddRecipients(mime.To, request.To);
      AddRecipients(mime.Cc, request.Cc);
      AddRecipients(mime.Bcc, request.Bcc);
      mime.Subject = request.Subject ?? "";
      if (string.IsNullOrWhiteSpace(mime.MessageId))
        mime.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId();
      var reply = MailId.Bracket(request.InReplyTo);
      if (reply.Length > 0)
        mime.InReplyTo = reply;
      foreach (var id in MailId.Tokens(request.References))
        mime.References.Add(id);
      if (reply.Length > 0 && mime.References.Count == 0)
        mime.References.Add(MailId.Normalize(request.InReplyTo));
      var builder = new BodyBuilder { TextBody = request.BodyText ?? "" };
      foreach (var file in request.Attachments) {
        ContentType type;
        if (string.IsNullOrWhiteSpace(file.ContentType) || !ContentType.TryParse(file.ContentType, out type!))
          type = new ContentType("application", "octet-stream");
        builder.Attachments.Add(file.Name, file.Bytes, type);
      }

      mime.Body = builder.ToMessageBody();

      using var smtp = new SmtpClient();
      try {
        await smtp.ConnectAsync(
          account.SmtpHost.Trim(),
          account.SmtpPort,
          MailSocket.Smtp(account),
          cancellationToken).ConfigureAwait(false);
        await MailKitAuth.AuthenticateAsync(smtp, account, material, cancellationToken).ConfigureAwait(false);
        await smtp.SendAsync(mime, cancellationToken).ConfigureAwait(false);
        return Result<string>.Ok(mime.MessageId ?? "");
      }
      finally {
        if (smtp.IsConnected)
          await smtp.DisconnectAsync(true, CancellationToken.None).ConfigureAwait(false);
      }
    }
    catch (Exception ex) {
      return Result<string>.UnprocessableEntity(null, MailAuthHint.Smtp(account, ex.Message));
    }
  }

  private static void AddRecipients(InternetAddressList list, IEnumerable<MailRecipient> recipients) {
    foreach (var person in recipients) {
      if (string.IsNullOrWhiteSpace(person.Address))
        continue;
      list.Add(string.IsNullOrWhiteSpace(person.Name)
        ? MailboxAddress.Parse(person.Address)
        : new MailboxAddress(person.Name, person.Address));
    }
  }
}
