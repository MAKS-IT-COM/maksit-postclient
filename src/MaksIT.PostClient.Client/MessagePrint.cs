using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public static class MessagePrint {
  public static string Html(MailMessageBody body, bool unwrap) {
    var inner = unwrap && body.Envelope.HasInnerMessage;
    var subject = inner && body.InnerSubject.Length > 0 ? body.InnerSubject : body.Header.Subject;
    var from = inner && body.InnerFrom.Length > 0 ? body.InnerFrom : body.Header.From;
    var to = inner && body.InnerTo.Length > 0 ? body.InnerTo : body.To;
    var text = inner ? body.InnerText : body.Text;
    if (string.IsNullOrWhiteSpace(text))
      text = MessageText.StripHtml(inner ? body.InnerHtml : body.Html);
    var files = inner ? body.InnerAttachmentFiles : body.AttachmentFiles;
    var fattura = FatturaPaDocument.FromAttachments(files);
    var bits = new List<string> {
      "<html><head><meta charset=\"utf-8\"><title>" + Esc(subject) + "</title></head><body>",
      "<h1>" + Esc(subject) + "</h1>",
      "<p>From: " + Esc(from) + "<br>To: " + Esc(to) + "<br>Date: "
        + Esc(body.Header.Date.ToLocalTime().ToString("f")) + "</p>",
      "<pre>" + Esc(text) + "</pre>"
    };
    if (fattura is not null)
      bits.Add("<h2>FatturaPA</h2><pre>" + Esc(fattura.Text) + "</pre>");
    if (files.Count > 0) {
      bits.Add("<h2>Attachments</h2><ul>");
      foreach (var file in files)
        bits.Add("<li>" + Esc(file.Name) + "</li>");
      bits.Add("</ul>");
    }

    bits.Add("</body></html>");
    return string.Join("\n", bits);
  }

  public static byte[] Pdf(MailMessageBody body, bool unwrap) {
    var inner = unwrap && body.Envelope.HasInnerMessage;
    var subject = inner && body.InnerSubject.Length > 0 ? body.InnerSubject : body.Header.Subject;
    var from = inner && body.InnerFrom.Length > 0 ? body.InnerFrom : body.Header.From;
    var to = inner && body.InnerTo.Length > 0 ? body.InnerTo : body.To;
    var text = inner ? body.InnerText : body.Text;
    if (string.IsNullOrWhiteSpace(text))
      text = MessageText.StripHtml(inner ? body.InnerHtml : body.Html);
    var files = inner ? body.InnerAttachmentFiles : body.AttachmentFiles;
    var lines = new List<string> {
      "From: " + from,
      "To: " + to,
      "Date: " + body.Header.Date.ToLocalTime().ToString("f"),
      ""
    };
    lines.AddRange((text ?? "").Replace("\r\n", "\n").Split('\n'));
    var fattura = FatturaPaDocument.FromAttachments(files);
    if (fattura is not null) {
      lines.Add("");
      lines.Add("--- FatturaPA ---");
      lines.AddRange(fattura.Text.Replace("\r\n", "\n").Split('\n'));
    }

    if (files.Count > 0) {
      lines.Add("");
      lines.Add("Attachments:");
      foreach (var file in files)
        lines.Add(" - " + file.Name);
    }

    return SimplePdf.FromLines(subject, lines);
  }

  public static string EmlFileName(MailMessageHeader header) {
    var name = $"{header.Date:yyyyMMdd}-{header.Subject}";
    foreach (var ch in Path.GetInvalidFileNameChars())
      name = name.Replace(ch, '-');
    if (name.Length > 80)
      name = name[..80];
    if (string.IsNullOrWhiteSpace(name.Trim('-')))
      name = header.Id.ToString();
    return name + ".eml";
  }

  private static string Esc(string? value) =>
    (value ?? "")
      .Replace("&", "&amp;")
      .Replace("<", "&lt;")
      .Replace(">", "&gt;");
}
