namespace MaksIT.PostClient.Shared;


public static class EnvelopeKind {
  public const string Ordinary = "ordinary";
  public const string SmimeSigned = "smime_signed";
  public const string PecTransport = "pec_transport";
  public const string PecReceipt = "pec_receipt";
  public const string EidasRem = "eidas_rem";

  public static string Label(string? kind) =>
    kind switch {
      PecTransport => "Certified mail (PEC)",
      PecReceipt => "PEC receipt",
      EidasRem => "Certified mail (REM)",
      SmimeSigned => "Digitally signed",
      _ => "Ordinary mail"
    };
}


public static class EnvelopeRegion {
  public const string Unknown = "unknown";
  public const string Italy = "italy";
  public const string Europe = "europe";
}
