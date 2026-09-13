namespace MaksIT.PostClient.Shared;


public static class ReceiptStatus {
  public const string Submitted = "submitted";
  public const string Accepted = "accepted";
  public const string Delivered = "delivered";
  public const string Failed = "failed";
  public const string Retrieved = "retrieved";

  public static string NormalizeId(string? value) {
    var id = (value ?? "").Trim().Trim('<', '>', '"', ' ');
    return id;
  }

  public static string FromTipo(string? tipo) {
    var key = Fold(tipo);
    if (key.Length == 0)
      return "";
    if (key is "accettazione" or "presa-in-carico" or "submissionacceptance"
        or "submission-acceptance" or "relay" or "annahmebestaetigung")
      return Accepted;
    if (key is "avvenuta-consegna" or "consegna" or "delivery" or "delivered"
        or "avis-reception" or "avis-de-reception" or "accuse-de-reception"
        or "acuse-de-recibo" or "zustellbestaetigung" or "zustellung")
      return Delivered;
    if (key is "contentconsignment" or "content-consignment" or "retrieval"
        or "contentretrieved" or "abholbestaetigung" or "consignment")
      return Retrieved;
    if (key.Contains("non-accettazione", StringComparison.Ordinal)
        || key.Contains("errore-consegna", StringComparison.Ordinal)
        || key.Contains("mancata-consegna", StringComparison.Ordinal)
        || key.Contains("rilevazione-virus", StringComparison.Ordinal)
        || key.Contains("nondelivery", StringComparison.Ordinal)
        || key.Contains("non-delivery", StringComparison.Ordinal)
        || key.Contains("submissionrejection", StringComparison.Ordinal)
        || key.Contains("deliveryexpiration", StringComparison.Ordinal)
        || key.Contains("non-remise", StringComparison.Ordinal)
        || key.Contains("virus", StringComparison.Ordinal)
        || key.Contains("reject", StringComparison.Ordinal))
      return Failed;
    if (key.Contains("preavviso", StringComparison.Ordinal))
      return Accepted;
    return "";
  }

  public static int Rank(string? status) =>
    Normalize(status) switch {
      Retrieved => 4,
      Delivered => 3,
      Failed => 3,
      Accepted => 2,
      Submitted => 1,
      _ => 0
    };

  public static string Normalize(string? status) {
    var value = (status ?? "").Trim().ToLowerInvariant();
    return value switch {
      Retrieved => Retrieved,
      Delivered => Delivered,
      Failed => Failed,
      Accepted => Accepted,
      Submitted => Submitted,
      _ => ""
    };
  }

  public static string Combine(string? current, string? incoming) {
    var next = Normalize(incoming);
    if (next.Length == 0)
      return Normalize(current);
    if (Rank(next) >= Rank(current))
      return next;
    return Normalize(current);
  }

  public static string Mark(string? status) =>
    Normalize(status) switch {
      Retrieved => "👁",
      Delivered => "✓✓",
      Failed => "✕",
      Accepted => "✓",
      Submitted => "…",
      _ => ""
    };

  public static string Label(string? status) =>
    Normalize(status) switch {
      Retrieved => "Retrieved",
      Delivered => "Delivered",
      Failed => "Failed",
      Accepted => "Accepted",
      Submitted => "Waiting for ricevuta",
      _ => ""
    };

  public static string Explain(string? tipo) {
    var status = FromTipo(tipo);
    if (status == Delivered)
      return "Delivered to the recipient certified mailbox (not a read receipt).";
    if (status == Retrieved)
      return "Recipient retrieved the content (eIDAS REM / De-Mail pickup).";
    if (status == Accepted)
      return "The gestore accepted the message. Waiting for delivery evidence.";
    if (status == Failed)
      return "The gestore reported non-acceptance, virus, or failed delivery.";
    return "";
  }

  private static string Fold(string? tipo) {
    var key = (tipo ?? "").Trim().ToLowerInvariant().Replace('_', '-').Replace(' ', '-');
    return key
      .Replace("ä", "ae", StringComparison.Ordinal)
      .Replace("ö", "oe", StringComparison.Ordinal)
      .Replace("ü", "ue", StringComparison.Ordinal)
      .Replace("ß", "ss", StringComparison.Ordinal)
      .Replace("é", "e", StringComparison.Ordinal)
      .Replace("è", "e", StringComparison.Ordinal);
  }
}
