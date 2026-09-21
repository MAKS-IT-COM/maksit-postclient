using System.Xml;


namespace MaksIT.PostClient.Shared.Envelope;


public static class RemEvidenceParser {
  public sealed class Data {
    public string Event { get; init; } = "";

    public string RelatedMessageId { get; init; } = "";

    public string EvidenceId { get; init; } = "";
  }

  public static Data? Parse(string? xml) {
    if (!EnvelopeClassifier.LooksLikeEtsi(xml))
      return null;
    try {
      var settings = new XmlReaderSettings {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true
      };
      var document = new XmlDocument { XmlResolver = null };
      using (var reader = XmlReader.Create(new StringReader(xml!), settings))
        document.Load(reader);
      var ev = First(document, "EventCode", "eventCode", "EvidenceType", "eventType", "Type");
      var related = First(
        document,
        "RelatesTo",
        "RelatedTo",
        "OriginalMessageId",
        "MessageIdentifier",
        "UAMessageId",
        "msgid");
      var id = First(document, "EvidenceIdentifier", "EvidenceId", "identificativo");
      if (string.IsNullOrWhiteSpace(ev) && string.IsNullOrWhiteSpace(related))
        return null;
      return new Data {
        Event = ev,
        RelatedMessageId = related,
        EvidenceId = id
      };
    }
    catch {
      return null;
    }
  }

  private static string First(XmlDocument document, params string[] names) {
    foreach (var name in names) {
      var nodes = document.GetElementsByTagName(name);
      foreach (XmlNode node in nodes) {
        var text = (node.InnerText ?? "").Trim();
        if (text.Length > 0)
          return text;
      }
    }

    return "";
  }
}
