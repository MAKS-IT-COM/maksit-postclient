using MimeKit;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public static class MimeBody {
  public static async Task<MailMessageBody?> TryFromEmlAsync(
    string folder,
    uint id,
    string path,
    CancellationToken cancellationToken) {
    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
      return null;
    await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    var mime = await MimeMessage.LoadAsync(stream, cancellationToken).ConfigureAwait(false);
    return await FromMimeAsync(folder, id, mime, cancellationToken).ConfigureAwait(false);
  }

  public static async Task<MailMessageBody> FromMimeAsync(
    string folder,
    uint id,
    MimeMessage mime,
    CancellationToken cancellationToken,
    bool seen = false,
    bool flagged = false) {
    using var buffer = new MemoryStream();
    await mime.WriteToAsync(buffer, cancellationToken).ConfigureAwait(false);
    var parsed = EnvelopeUnwrapper.Inspect(mime);
    var inner = parsed.Inner;
    var outerFiles = MimeAttachments.Collect(mime, skipEnvelope: false);
    var innerFiles = inner is null ? [] : MimeAttachments.Collect(inner, skipEnvelope: true);
    var header = ToHeader(
      folder,
      id,
      mime,
      parsed.Info,
      outerFiles.Count > 0 || innerFiles.Count > 0,
      seen,
      flagged);
    return new MailMessageBody {
      Header = header,
      Text = mime.TextBody ?? "",
      Html = mime.HtmlBody ?? "",
      Attachments = MimeAttachments.Names(outerFiles),
      RawEml = buffer.ToArray(),
      Envelope = parsed.Info,
      InnerSubject = inner?.Subject ?? "",
      InnerFrom = inner?.From.ToString() ?? "",
      To = mime.To.ToString(),
      Cc = mime.Cc.ToString(),
      InnerTo = inner?.To.ToString() ?? "",
      InnerCc = inner?.Cc.ToString() ?? "",
      InnerText = inner?.TextBody ?? "",
      InnerHtml = inner?.HtmlBody ?? "",
      InnerAttachments = MimeAttachments.Names(innerFiles),
      AttachmentFiles = outerFiles,
      InnerAttachmentFiles = innerFiles
    };
  }

  public static MailMessageHeader ToHeader(
    string folder,
    uint id,
    MimeMessage mime,
    EnvelopeInfo info,
    bool hasAttachments,
    bool seen = false,
    bool flagged = false) =>
    new() {
      Id = id,
      Folder = folder,
      Subject = mime.Subject ?? "",
      From = mime.From.ToString(),
      Date = mime.Date,
      IsSeen = seen,
      IsFlagged = flagged,
      HasAttachments = hasAttachments,
      Priority = mime.Priority == MessagePriority.Urgent
        ? MailPriority.High
        : mime.Priority == MessagePriority.NonUrgent
          ? MailPriority.Low
          : MailPriority.Normal,
      EnvelopeKind = info.Kind,
      EnvelopeBadge = info.Badge,
      EnvelopeTipo = info.Tipo,
      MessageId = mime.MessageId ?? "",
      InReplyTo = MailId.Parent(mime.InReplyTo, string.Join(" ", mime.References)),
      DeliveryStatus = info.EventStatus
    };

  public static MailMessageHeader ToListHeader(
    string folder,
    uint id,
    string subject,
    string from,
    DateTimeOffset date,
    bool seen,
    bool flagged,
    bool hasAttachments,
    EnvelopeInfo info,
    string priority = MailPriority.Normal,
    string? messageId = null,
    string? inReplyTo = null) =>
    new() {
      Id = id,
      Folder = folder,
      Subject = subject,
      From = from,
      Date = date,
      IsSeen = seen,
      IsFlagged = flagged,
      HasAttachments = hasAttachments,
      Priority = string.IsNullOrWhiteSpace(priority) ? MailPriority.Normal : priority,
      EnvelopeKind = info.Kind,
      EnvelopeBadge = info.Badge,
      EnvelopeTipo = info.Tipo,
      MessageId = messageId ?? "",
      InReplyTo = inReplyTo ?? "",
      DeliveryStatus = info.EventStatus
    };
}
