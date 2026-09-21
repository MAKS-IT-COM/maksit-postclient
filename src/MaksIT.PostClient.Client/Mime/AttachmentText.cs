using System.Text;
using UglyToad.PdfPig;


namespace MaksIT.PostClient.Client.Mime;


public static class AttachmentText {
  public const int MaxChars = 400_000;

  public static string Extract(IEnumerable<MailFileAttachment> files) {
    ArgumentNullException.ThrowIfNull(files);
    var text = new StringBuilder();
    foreach (var file in files) {
      var chunk = FromFile(file);
      if (chunk.Length == 0)
        continue;
      if (text.Length > 0)
        text.Append('\n');
      text.Append(file.Name).Append('\n').Append(chunk);
      if (text.Length >= MaxChars)
        break;
    }

    return Trim(text.ToString());
  }

  internal static string FromFile(MailFileAttachment file) {
    var name = file.Name ?? "";
    var type = file.ContentType ?? "";
    if (LooksPdf(name, type))
      return FromPdf(file.Bytes);
    if (LooksText(name, type))
      return FromUtf8(file.Bytes);
    return "";
  }

  private static bool LooksPdf(string name, string type) =>
    type.Contains("pdf", StringComparison.OrdinalIgnoreCase)
    || name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

  private static bool LooksText(string name, string type) {
    if (type.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
      return true;
    if (type.Contains("xml", StringComparison.OrdinalIgnoreCase) || type.Contains("json", StringComparison.OrdinalIgnoreCase))
      return true;
    return name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
      || name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
      || name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
      || name.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
      || name.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)
      || name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
  }

  private static string FromUtf8(byte[] bytes) {
    if (bytes.Length == 0)
      return "";
    var take = Math.Min(bytes.Length, MaxChars * 2);
    return Trim(Encoding.UTF8.GetString(bytes.AsSpan(0, take)));
  }

  private static string FromPdf(byte[] bytes) {
    try {
      using var stream = new MemoryStream(bytes, writable: false);
      using var doc = PdfDocument.Open(stream);
      var text = new StringBuilder();
      foreach (var page in doc.GetPages()) {
        text.AppendLine(page.Text);
        if (text.Length >= MaxChars)
          break;
      }

      return Trim(text.ToString());
    }
    catch {
      return "";
    }
  }

  private static string Trim(string value) {
    if (value.Length <= MaxChars)
      return value.Trim();
    return value[..MaxChars].Trim();
  }
}
