namespace MaksIT.PostClient.Shared.Envelope;


public static class EnvelopeClassifier {
  private static readonly HashSet<string> ReceiptTipi = new(StringComparer.OrdinalIgnoreCase) {
    "accettazione",
    "non-accettazione",
    "presa-in-carico",
    "avvenuta-consegna",
    "rilevazione-virus",
    "errore-consegna",
    "preavviso-errore-consegna",
    "mancata-consegna"
  };

  public static EnvelopeInfo Classify(
    IEnumerable<KeyValuePair<string, string>>? headers,
    IEnumerable<string>? partNames,
    string? xml = null) {
    var headerMap = ToMap(headers);
    var names = (partNames ?? []).Select(n => n.Trim()).Where(n => n.Length > 0).ToList();
    var hasDaticert = names.Any(IsDaticertName);
    var hasPostacert = names.Any(IsPostacertName);
    var hasSmime = names.Any(IsSmimeName);
    var etsiXml = LooksLikeEtsi(xml);
    var hasEtsiName = names.Any(IsEtsiName);
    var daticert = DaticertParser.Parse(xml);
    var rem = RemEvidenceParser.Parse(xml);
    var trasporto = Get(headerMap, "X-Trasporto");
    var ricevuta = Get(headerMap, "X-Ricevuta");
    var tipo = FirstNonEmpty(
      daticert?.Tipo,
      rem?.Event,
      ricevuta,
      Get(headerMap, "X-TipoRicevuta"),
      trasporto);
    var msgid = FirstNonEmpty(
      daticert?.Msgid,
      rem?.RelatedMessageId,
      Get(headerMap, "X-Riferimento-Message-ID"),
      Get(headerMap, "In-Reply-To"));
    var italy = hasDaticert || daticert is not null || LooksLikeTrasporto(trasporto) || !string.IsNullOrWhiteSpace(ricevuta);
    var europe = !italy && (etsiXml || hasEtsiName || rem is not null);

    if (europe) {
      return new EnvelopeInfo {
        Kind = EnvelopeKind.EidasRem,
        Region = EnvelopeRegion.Europe,
        Tipo = FirstNonEmpty(tipo, "rem"),
        Identificativo = rem?.EvidenceId ?? "",
        Msgid = msgid,
        EventStatus = ReceiptStatus.FromTipo(tipo),
        HasInnerMessage = hasPostacert
      };
    }

    if (italy || hasPostacert) {
      var receipt = IsReceiptTipo(tipo) || !string.IsNullOrWhiteSpace(ricevuta);
      return new EnvelopeInfo {
        Kind = receipt ? EnvelopeKind.PecReceipt : EnvelopeKind.PecTransport,
        Region = EnvelopeRegion.Italy,
        Tipo = tipo,
        Gestore = daticert?.Gestore ?? "",
        Identificativo = daticert?.Identificativo ?? "",
        Mittente = daticert?.Mittente ?? "",
        Oggetto = daticert?.Oggetto ?? "",
        Msgid = msgid,
        Errore = daticert?.Errore ?? "",
        EventStatus = receipt ? ReceiptStatus.FromTipo(tipo) : "",
        HasInnerMessage = hasPostacert
      };
    }

    if (hasSmime) {
      return new EnvelopeInfo {
        Kind = EnvelopeKind.SmimeSigned,
        Region = EnvelopeRegion.Unknown,
        HasInnerMessage = false
      };
    }

    return new EnvelopeInfo();
  }

  public static bool IsDaticertName(string? name) =>
    FileName(name).Equals("daticert.xml", StringComparison.OrdinalIgnoreCase);

  public static bool IsPostacertName(string? name) =>
    FileName(name).Equals("postacert.eml", StringComparison.OrdinalIgnoreCase);

  public static bool IsSmimeName(string? name) {
    var file = FileName(name);
    return file.Equals("smime.p7s", StringComparison.OrdinalIgnoreCase)
      || file.Equals("smime.p7m", StringComparison.OrdinalIgnoreCase);
  }

  public static bool IsEtsiName(string? name) {
    var file = FileName(name);
    return file.Contains("rem-md", StringComparison.OrdinalIgnoreCase)
      || file.Contains("remevidence", StringComparison.OrdinalIgnoreCase)
      || file.Contains("etsi", StringComparison.OrdinalIgnoreCase);
  }

  public static bool IsReceiptTipo(string? tipo) {
    if (string.IsNullOrWhiteSpace(tipo))
      return false;
    return ReceiptTipi.Contains(tipo.Trim());
  }

  public static bool LooksLikeTrasporto(string? value) =>
    !string.IsNullOrWhiteSpace(value)
    && value.Contains("posta-certificata", StringComparison.OrdinalIgnoreCase);

  public static bool LooksLikeDaticert(string? xml) =>
    !string.IsNullOrWhiteSpace(xml)
    && xml.Contains("<postacert", StringComparison.OrdinalIgnoreCase);

  public static bool LooksLikeEtsi(string? xml) {
    if (string.IsNullOrWhiteSpace(xml))
      return false;
    return xml.Contains("etsi.org", StringComparison.OrdinalIgnoreCase)
      || xml.Contains("urn:etsi:rem", StringComparison.OrdinalIgnoreCase)
      || xml.Contains("REMEvidence", StringComparison.OrdinalIgnoreCase)
      || xml.Contains("REMDispatch", StringComparison.OrdinalIgnoreCase);
  }

  private static Dictionary<string, string> ToMap(IEnumerable<KeyValuePair<string, string>>? headers) {
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (headers is null)
      return map;
    foreach (var pair in headers) {
      if (string.IsNullOrWhiteSpace(pair.Key))
        continue;
      map[pair.Key.Trim()] = pair.Value ?? "";
    }

    return map;
  }

  private static string Get(Dictionary<string, string> map, string key) =>
    map.TryGetValue(key, out var value) ? value : "";

  private static string FirstNonEmpty(params string?[] values) {
    foreach (var value in values) {
      if (!string.IsNullOrWhiteSpace(value))
        return value.Trim();
    }

    return "";
  }

  private static string FileName(string? name) {
    if (string.IsNullOrWhiteSpace(name))
      return "";
    return Path.GetFileName(name.Trim().Trim('"'));
  }
}
