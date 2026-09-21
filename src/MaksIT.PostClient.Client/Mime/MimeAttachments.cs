using MailKit;
using MimeKit;
using MimeKit.Cryptography;


namespace MaksIT.PostClient.Client.Mime;


internal static class MimeAttachments {
  public static IReadOnlyList<MailFileAttachment> Collect(MimeMessage mime, bool skipEnvelope) {
    ArgumentNullException.ThrowIfNull(mime);
    var list = new List<MailFileAttachment>();
    Walk(mime.Body, list, skipEnvelope);
    return list;
  }

  public static IReadOnlyList<string> Names(IEnumerable<MailFileAttachment> files) =>
    files.Select(f => f.Name).ToList();

  public static bool HasVisible(IEnumerable<MailFileAttachment> files, string? html) {
    foreach (var file in files) {
      if (!IsInlineImage(file, html))
        return true;
    }

    return false;
  }

  public static bool IsInlineImage(MailFileAttachment file, string? html) {
    if (string.IsNullOrWhiteSpace(file.ContentId))
      return false;
    if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
      return false;
    if (string.IsNullOrWhiteSpace(html))
      return false;
    return html.Contains("cid:" + file.ContentId, StringComparison.OrdinalIgnoreCase);
  }

  public static bool HasFromStructure(BodyPart? part) =>
    HasFromStructure(part, skipEnvelope: false);

  private static bool HasFromStructure(BodyPart? part, bool skipEnvelope) {
    if (part is null)
      return false;
    if (part is BodyPartMultipart multi) {
      if (multi.ContentType.IsMimeType("multipart", "signed") && multi.BodyParts.Count > 0)
        return HasFromStructure(multi.BodyParts[0], skipEnvelope);
      foreach (var child in multi.BodyParts) {
        if (HasFromStructure(child, skipEnvelope))
          return true;
      }

      return false;
    }

    if (part is BodyPartMessage rfc) {
      if (!skipEnvelope)
        return true;
      return HasFromStructure(rfc.Body, skipEnvelope: true);
    }

    if (part is BodyPartBasic basic)
      return IsVisiblePart(basic, skipEnvelope);
    return false;
  }

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

  private static bool IsVisiblePart(BodyPartBasic part, bool skipEnvelope) {
    var name = part.FileName ?? "";
    if (skipEnvelope && IsEnvelopeName(name))
      return false;
    if (IsBodyText(part, name))
      return false;
    if (IsInlineImage(part))
      return false;
    if (part.IsAttachment || !string.IsNullOrWhiteSpace(name))
      return true;
    if (!string.IsNullOrWhiteSpace(part.ContentId))
      return true;
    var media = part.ContentType.MediaType ?? "";
    if (media.Equals("text", StringComparison.OrdinalIgnoreCase)
        || media.Equals("multipart", StringComparison.OrdinalIgnoreCase)
        || media.Equals("message", StringComparison.OrdinalIgnoreCase))
      return false;
    return part.Octets > 0;
  }

  private static bool IsBodyText(MimePart part, string name) {
    if (part is not TextPart || part.IsAttachment || !string.IsNullOrWhiteSpace(name))
      return false;
    return part.ContentType.IsMimeType("text", "plain") || part.ContentType.IsMimeType("text", "html");
  }

  private static bool IsBodyText(BodyPartBasic part, string name) {
    if (part.IsAttachment || !string.IsNullOrWhiteSpace(name))
      return false;
    return part.ContentType.IsMimeType("text", "plain") || part.ContentType.IsMimeType("text", "html");
  }

  private static bool IsInlineImage(BodyPartBasic part) {
    if (part.IsAttachment || string.IsNullOrWhiteSpace(part.ContentId))
      return false;
    return string.Equals(part.ContentType.MediaType, "image", StringComparison.OrdinalIgnoreCase);
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
