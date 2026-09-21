namespace MaksIT.PostClient.Shared.Envelope;


public sealed class EnvelopeInfo {
  public string Kind { get; init; } = EnvelopeKind.Ordinary;

  public string Region { get; init; } = EnvelopeRegion.Unknown;

  public string Tipo { get; init; } = "";

  public string Gestore { get; init; } = "";

  public string Identificativo { get; init; } = "";

  public string Mittente { get; init; } = "";

  public string Oggetto { get; init; } = "";

  public string Msgid { get; init; } = "";

  public string Errore { get; init; } = "";

  public string EventStatus { get; init; } = "";

  public bool HasInnerMessage { get; init; }

  public string Badge {
    get {
      if (Kind == EnvelopeKind.PecTransport)
        return "PEC";
      if (Kind == EnvelopeKind.PecReceipt)
        return "RIC";
      if (Kind == EnvelopeKind.EidasRem)
        return "REM";
      if (Kind == EnvelopeKind.SmimeSigned)
        return "SIG";
      return "";
    }
  }

  public string Line {
    get {
      if (Kind == EnvelopeKind.Ordinary)
        return "";
      var region = Region == EnvelopeRegion.Italy
        ? "Italia"
        : Region == EnvelopeRegion.Europe ? "Europa" : "";
      var title = Kind switch {
        EnvelopeKind.PecTransport => "PEC",
        EnvelopeKind.PecReceipt => "Ricevuta PEC",
        EnvelopeKind.EidasRem => "REM",
        EnvelopeKind.SmimeSigned => "Firmato",
        _ => Kind
      };
      var bits = new List<string> { title };
      if (!string.IsNullOrWhiteSpace(region))
        bits.Add(region);
      if (!string.IsNullOrWhiteSpace(Tipo))
        bits.Add(Tipo);
      if (!string.IsNullOrWhiteSpace(EventStatus))
        bits.Add(ReceiptStatus.Label(EventStatus));
      if (!string.IsNullOrWhiteSpace(Gestore))
        bits.Add(Gestore);
      if (!string.IsNullOrWhiteSpace(Identificativo))
        bits.Add(Identificativo);
      return string.Join(" · ", bits);
    }
  }
}
