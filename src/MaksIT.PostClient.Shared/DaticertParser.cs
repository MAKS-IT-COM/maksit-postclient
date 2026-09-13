using System.Xml;


namespace MaksIT.PostClient.Shared;


public static class DaticertParser {
  public sealed class Data {
    public string Tipo { get; init; } = "";

    public string Errore { get; init; } = "";

    public string Gestore { get; init; } = "";

    public string Identificativo { get; init; } = "";

    public string Mittente { get; init; } = "";

    public string Oggetto { get; init; } = "";

    public string Msgid { get; init; } = "";

    public string Destinatari { get; init; } = "";

    public string Consegna { get; init; } = "";
  }

  public static Data? Parse(string? xml) {
    if (string.IsNullOrWhiteSpace(xml))
      return null;
    try {
      var settings = new XmlReaderSettings {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true
      };
      var document = new XmlDocument { XmlResolver = null };
      using (var reader = XmlReader.Create(new StringReader(xml), settings))
        document.Load(reader);
      var root = document.DocumentElement;
      if (root is null)
        return null;
      var tipo = Attr(root, "tipo");
      var errore = Attr(root, "errore");
      if (string.IsNullOrWhiteSpace(tipo))
        tipo = LocalText(document, "tipo");
      var gestore = LocalText(document, "gestore-emittente");
      var identificativo = LocalText(document, "identificativo");
      var mittente = LocalText(document, "mittente");
      var oggetto = LocalText(document, "oggetto");
      var msgid = LocalText(document, "msgid");
      var consegna = LocalText(document, "consegna");
      var destinatari = JoinLocal(document, "destinatari");
      if (string.IsNullOrWhiteSpace(tipo)
        && string.IsNullOrWhiteSpace(gestore)
        && string.IsNullOrWhiteSpace(identificativo))
        return null;
      return new Data {
        Tipo = tipo,
        Errore = errore,
        Gestore = gestore,
        Identificativo = identificativo,
        Mittente = mittente,
        Oggetto = oggetto,
        Msgid = msgid,
        Destinatari = destinatari,
        Consegna = consegna
      };
    }
    catch {
      return null;
    }
  }

  private static string Attr(XmlElement element, string name) =>
    element.GetAttribute(name)?.Trim() ?? "";

  private static string JoinLocal(XmlDocument document, string localName) {
    var nodes = document.GetElementsByTagName(localName);
    var parts = new List<string>();
    foreach (XmlNode node in nodes) {
      var text = (node.InnerText ?? "").Trim();
      if (text.Length > 0 && !parts.Contains(text, StringComparer.OrdinalIgnoreCase))
        parts.Add(text);
    }

    return string.Join(", ", parts);
  }

  private static string LocalText(XmlDocument document, string localName) {
    var nodes = document.GetElementsByTagName(localName);
    return nodes.Count == 0 ? "" : (nodes[0]?.InnerText ?? "").Trim();
  }
}
