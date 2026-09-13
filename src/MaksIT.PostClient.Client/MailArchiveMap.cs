using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public static class MailArchiveMap {
  public static MailArchiveHeader FromHeader(MailMessageHeader header) =>
    new() {
      Uid = header.Id,
      Folder = header.Folder,
      Subject = header.Subject,
      From = header.From,
      Date = header.Date,
      IsSeen = header.IsSeen,
      IsFlagged = header.IsFlagged,
      HasAttachments = header.HasAttachments,
      Priority = header.Priority,
      EnvelopeKind = header.EnvelopeKind,
      EnvelopeBadge = header.EnvelopeBadge,
      EnvelopeTipo = header.EnvelopeTipo,
      MessageId = header.MessageId,
      InReplyTo = header.InReplyTo,
      Labels = header.Labels
    };

  public static MailMessageHeader ToHeader(MailArchiveHeader row) =>
    new() {
      Id = row.Uid,
      Folder = row.Folder,
      Subject = row.Subject,
      From = row.From,
      Date = row.Date,
      IsSeen = row.IsSeen,
      IsFlagged = row.IsFlagged,
      HasAttachments = row.HasAttachments,
      Priority = string.IsNullOrWhiteSpace(row.Priority) ? MailPriority.Normal : row.Priority,
      EnvelopeKind = row.EnvelopeKind,
      EnvelopeBadge = row.EnvelopeBadge,
      EnvelopeTipo = row.EnvelopeTipo,
      MessageId = row.MessageId,
      InReplyTo = row.InReplyTo,
      Labels = row.Labels
    };

  public static string BodyText(MailMessageBody body, bool unwrap) {
    var inner = unwrap && body.Envelope.HasInnerMessage;
    var text = inner ? body.InnerText : body.Text;
    if (!string.IsNullOrWhiteSpace(text))
      return text;
    var html = inner ? body.InnerHtml : body.Html;
    return MessageText.StripHtml(html);
  }

  public static string AttachmentIndex(MailMessageBody body, bool unwrap) {
    var files = unwrap && body.Envelope.HasInnerMessage ? body.InnerAttachmentFiles : body.AttachmentFiles;
    return AttachmentText.Extract(files);
  }
}
