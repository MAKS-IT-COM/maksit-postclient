using System.Text;


namespace MaksIT.PostClient.Shared.Content;


public static class SimplePdf {
  public static byte[] FromLines(string title, IReadOnlyList<string> lines) {
    var text = new StringBuilder();
    text.AppendLine(Sanitize(title));
    text.AppendLine();
    foreach (var line in lines) {
      foreach (var wrap in Wrap(Sanitize(line), 90))
        text.AppendLine(wrap);
    }

    var content = "BT /F1 11 Tf 50 780 Td 14 TL (" + Escape(text.ToString().Replace("\r\n", "\n").Replace('\r', '\n'))
      .Replace("\n", ") Tj T* (") + ") Tj ET";
    var stream = Encoding.ASCII.GetBytes(content);
    var objects = new List<byte[]> {
      Obj(1, "<< /Type /Catalog /Pages 2 0 R >>"),
      Obj(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
      Obj(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>"),
      Obj(4, "<< /Length " + stream.Length + " >>\nstream\n" + Encoding.ASCII.GetString(stream) + "\nendstream"),
      Obj(5, "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>")
    };
    using var output = new MemoryStream();
    var header = "%PDF-1.4\n"u8.ToArray();
    output.Write(header);
    var offsets = new List<long> { 0 };
    foreach (var obj in objects) {
      offsets.Add(output.Position);
      output.Write(obj);
      output.Write("\n"u8);
    }

    var xref = output.Position;
    var xrefText = new StringBuilder();
    xrefText.Append("xref\n0 ").Append(objects.Count + 1).Append('\n');
    xrefText.Append("0000000000 65535 f \n");
    for (var i = 1; i < offsets.Count; i++)
      xrefText.Append(offsets[i].ToString("0000000000")).Append(" 00000 n \n");
    xrefText.Append("trailer << /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\n");
    xrefText.Append("startxref\n").Append(xref).Append("\n%%EOF\n");
    output.Write(Encoding.ASCII.GetBytes(xrefText.ToString()));
    return output.ToArray();
  }

  private static byte[] Obj(int id, string body) =>
    Encoding.ASCII.GetBytes(id + " 0 obj\n" + body + "\nendobj");

  private static string Sanitize(string value) {
    var chars = value.Select(ch => ch < 32 || ch > 126 ? '?' : ch).ToArray();
    return new string(chars);
  }

  private static string Escape(string value) =>
    value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

  private static IEnumerable<string> Wrap(string line, int width) {
    if (line.Length == 0) {
      yield return "";
      yield break;
    }

    for (var i = 0; i < line.Length; i += width)
      yield return line.Substring(i, Math.Min(width, line.Length - i));
  }
}
