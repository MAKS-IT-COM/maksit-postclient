using System.Text;
using System.Xml.Linq;


namespace MaksIT.PostClient.Client.Mime;


public sealed class FatturaPaDocument {
  public string Supplier { get; init; } = "";

  public string Customer { get; init; } = "";

  public string Number { get; init; } = "";

  public string Date { get; init; } = "";

  public string Currency { get; init; } = "";

  public string Total { get; init; } = "";

  public IReadOnlyList<string> Lines { get; init; } = [];

  public string Text {
    get {
      var b = new StringBuilder();
      b.AppendLine("FatturaPA");
      if (Supplier.Length > 0)
        b.Append("From: ").AppendLine(Supplier);
      if (Customer.Length > 0)
        b.Append("To: ").AppendLine(Customer);
      if (Number.Length > 0 || Date.Length > 0)
        b.Append("No. ").Append(Number).Append("  ").AppendLine(Date);
      if (Total.Length > 0)
        b.Append("Total: ").Append(Total).Append(' ').AppendLine(Currency);
      foreach (var line in Lines) {
        b.Append("- ").AppendLine(line);
        if (b.Length > 20_000)
          break;
      }

      return b.ToString().Trim();
    }
  }

  public static bool LooksLike(string? name, string? contentType) {
    var n = name ?? "";
    var t = contentType ?? "";
    if (t.Contains("xml", StringComparison.OrdinalIgnoreCase) && n.Contains("fattura", StringComparison.OrdinalIgnoreCase))
      return true;
    return n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
      && (n.Contains("fattura", StringComparison.OrdinalIgnoreCase)
        || n.Contains("IT", StringComparison.OrdinalIgnoreCase));
  }

  public static FatturaPaDocument? TryParse(byte[] bytes) {
    if (bytes.Length == 0 || bytes.Length > 8_000_000)
      return null;
    try {
      using var stream = new MemoryStream(bytes, writable: false);
      var doc = XDocument.Load(stream);
      var root = doc.Root;
      if (root is null || !Local(root).Equals("FatturaElettronica", StringComparison.OrdinalIgnoreCase))
        return null;
      var header = Child(root, "FatturaElettronicaHeader");
      var body = Child(root, "FatturaElettronicaBody");
      var generali = Child(Child(body, "DatiGenerali"), "DatiGeneraliDocumento");
      var lines = new List<string>();
      var beni = Child(body, "DatiBeniServizi");
      if (beni is not null) {
        foreach (var row in beni.Elements().Where(e => Local(e) == "DettaglioLinee")) {
          var desc = ValueOf(Child(row, "Descrizione"));
          var qty = ValueOf(Child(row, "Quantita"));
          var price = ValueOf(Child(row, "PrezzoTotale"));
          var bit = desc;
          if (qty.Length > 0)
            bit += " × " + qty;
          if (price.Length > 0)
            bit += "  " + price;
          if (bit.Length > 0)
            lines.Add(bit);
        }
      }

      return new FatturaPaDocument {
        Supplier = PartyName(Child(header, "CedentePrestatore")),
        Customer = PartyName(Child(header, "CessionarioCommittente")),
        Number = ValueOf(Child(generali, "Numero")),
        Date = ValueOf(Child(generali, "Data")),
        Currency = ValueOf(Child(generali, "Divisa")),
        Total = ValueOf(Child(generali, "ImportoTotaleDocumento")),
        Lines = lines
      };
    }
    catch {
      return null;
    }
  }

  public static FatturaPaDocument? FromAttachments(IEnumerable<MailFileAttachment> files) {
    foreach (var file in files) {
      if (!LooksLike(file.Name, file.ContentType) && !LooksXml(file))
        continue;
      var parsed = TryParse(file.Bytes);
      if (parsed is not null)
        return parsed;
    }

    return null;
  }

  private static bool LooksXml(MailFileAttachment file) {
    var type = file.ContentType ?? "";
    return type.Contains("xml", StringComparison.OrdinalIgnoreCase)
      || (file.Name ?? "").EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
  }

  private static string PartyName(XElement? party) {
    var anag = Child(Child(party, "DatiAnagrafici"), "Anagrafica");
    var name = ValueOf(Child(anag, "Denominazione"));
    if (name.Length > 0)
      return name;
    var first = ValueOf(Child(anag, "Nome"));
    var last = ValueOf(Child(anag, "Cognome"));
    return (first + " " + last).Trim();
  }

  private static XElement? Child(XElement? parent, string local) {
    if (parent is null)
      return null;
    return parent.Elements().FirstOrDefault(e => Local(e) == local);
  }

  private static string Local(XElement element) =>
    element.Name.LocalName;

  private static string ValueOf(XElement? element) =>
    element?.Value.Trim() ?? "";
}
