using MimeKit;
using MimeKit.Cryptography;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


internal static class MimeAttachments {
  public static IReadOnlyList<MailFileAttachment> Collect(MimeMessage mime, bool skipEnvelope) {
    ArgumentNullException.ThrowIfNull(mime);
    var list = new List<MailFileAttachment>();
    Walk(mime.Body, list, skipEnvelope);
    return list;
  }

  public static IReadOnlyList<string> Names(IEnumerable<MailFileAttachment> files) =>
    files.Select(f => f.Name).ToList();

  private static void Walk(MimeEntity? entity, List<MailFileAttachment> list, bool skipEnvelope) {
    if (entity is null)
      return;
    if (entity is MultipartSigned signed) {
      if (signed.Count > 0)
        Walk(signed[0], list, skipEnvelope);
      return;
    }

    if (entity is MessagePart rfc) {
      if (!skipEnvelope)
        AddMessage(list, rfc);
      return;
    }

    if (entity is Multipart multi) {
      foreach (var child in multi)
        Walk(child, list, skipEnvelope);
      return;
    }

    if (entity is MimePart part)
      TryAdd(part, list, skipEnvelope);
  }

  private static void TryAdd(MimePart part, List<MailFileAttachment> list, bool skipEnvelope) {
    var name = PartName(part);
    if (skipEnvelope && IsEnvelopeName(name))
      return;
    if (IsBodyText(part, name))
      return;
    var cid = (part.ContentId ?? "").Trim().Trim('<', '>');
    if (!part.IsAttachment && string.IsNullOrWhiteSpace(name) && cid.Length == 0)
      return;
    var bytes = Read(part);
    if (bytes.Length == 0)
      return;
    list.Add(new MailFileAttachment {
      Name = string.IsNullOrWhiteSpace(name) ? (cid.Length > 0 ? cid : "attachment") : name,
      Bytes = bytes,
      ContentType = part.ContentType.MimeType,
      ContentId = cid
    });
  }

  private static void AddMessage(List<MailFileAttachment> list, MessagePart rfc) {
    var name = PartName(rfc);
    if (string.IsNullOrWhiteSpace(name))
      name = "message.eml";
    using var buffer = new MemoryStream();
    rfc.Message?.WriteTo(buffer);
    if (buffer.Length == 0)
      return;
    list.Add(new MailFileAttachment {
      Name = name,
      Bytes = buffer.ToArray(),
      ContentType = "message/rfc822"
    });
  }

  private static bool IsBodyText(MimePart part, string name) {
    if (part is not TextPart || part.IsAttachment || !string.IsNullOrWhiteSpace(name))
      return false;
    return part.ContentType.IsMimeType("text", "plain") || part.ContentType.IsMimeType("text", "html");
  }

  private static bool IsEnvelopeName(string name) =>
    EnvelopeClassifier.IsDaticertName(name)
    || EnvelopeClassifier.IsPostacertName(name)
    || EnvelopeClassifier.IsSmimeName(name);

  private static string PartName(MimeEntity entity) =>
    entity.ContentDisposition?.FileName ?? entity.ContentType.Name ?? "";

  private static byte[] Read(MimePart part) {
    try {
      using var buffer = new MemoryStream();
      part.Content?.DecodeTo(buffer);
      return buffer.ToArray();
    }
    catch {
      return [];
    }
  }
}
