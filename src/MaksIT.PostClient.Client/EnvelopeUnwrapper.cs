using MimeKit;
using MimeKit.Cryptography;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public sealed class EnvelopeParse {
  public required EnvelopeInfo Info { get; init; }

  public MimeMessage? Inner { get; init; }
}


public static class EnvelopeUnwrapper {
  public static EnvelopeParse Inspect(MimeMessage mime) {
    ArgumentNullException.ThrowIfNull(mime);
    MimeMessage? inner = null;
    string? xml = null;
    var names = new List<string>();
    Collect(mime.Body, names, ref inner, ref xml);
    var headers = mime.Headers.Select(h => new KeyValuePair<string, string>(h.Field, h.Value));
    var info = EnvelopeClassifier.Classify(headers, names, xml);
    if (inner is not null && !info.HasInnerMessage) {
      info = new EnvelopeInfo {
        Kind = info.Kind,
        Region = info.Region,
        Tipo = info.Tipo,
        Gestore = info.Gestore,
        Identificativo = info.Identificativo,
        Mittente = info.Mittente,
        Oggetto = info.Oggetto,
        Msgid = info.Msgid,
        Errore = info.Errore,
        EventStatus = info.EventStatus,
        HasInnerMessage = true
      };
    }

    return new EnvelopeParse { Info = info, Inner = inner };
  }

  private static void Collect(
    MimeEntity? entity,
    List<string> names,
    ref MimeMessage? inner,
    ref string? xml) {
    if (entity is null)
      return;
    if (entity is MultipartSigned signed) {
      if (signed.Count > 0)
        Collect(signed[0], names, ref inner, ref xml);
      if (signed.Count > 1)
        AddName(names, signed[1]);
      return;
    }

    if (entity is MessagePart rfc) {
      AddName(names, rfc);
      inner ??= rfc.Message;
      return;
    }

    if (entity is Multipart multi) {
      foreach (var child in multi)
        Collect(child, names, ref inner, ref xml);
      return;
    }

    if (entity is MimePart part) {
      AddName(names, part);
      if (xml is not null)
        return;
      var name = PartName(part);
      if (!EnvelopeClassifier.IsDaticertName(name) && !part.ContentType.IsMimeType("application", "xml") && !part.ContentType.IsMimeType("text", "xml"))
        return;
      var text = ReadText(part);
      if (EnvelopeClassifier.IsDaticertName(name)
          || EnvelopeClassifier.LooksLikeDaticert(text)
          || EnvelopeClassifier.LooksLikeEtsi(text))
        xml = text;
    }
  }

  private static void AddName(List<string> names, MimeEntity entity) {
    var name = PartName(entity);
    if (!string.IsNullOrWhiteSpace(name))
      names.Add(name);
  }

  private static string PartName(MimeEntity entity) =>
    entity.ContentDisposition?.FileName ?? entity.ContentType.Name ?? "";

  private static string? ReadText(MimePart part) {
    try {
      using var buffer = new MemoryStream();
      part.Content?.DecodeTo(buffer);
      if (buffer.Length == 0)
        return null;
      return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }
    catch {
      return null;
    }
  }
}
