using System.Net;
using System.Text.RegularExpressions;


namespace MaksIT.PostClient.Shared;


public static class MessageHtml {
  private static readonly Regex Cid = new(
    @"cid:([^""'\s>]+)",
    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
  private static readonly Regex Markup = new(
    @"<(html|head|body|div|p|br|table|tr|td|span|a|img|h[1-6]|ul|ol|li|pre|font|center|strong|em|b|i|hr|blockquote|style)\b",
    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
  private static readonly Regex Head = new(
    @"<head[^>]*>",
    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
  private const string Csp =
    """<meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data: https: http:; style-src 'unsafe-inline'; font-src data: https:; media-src data:">""";
  private const string Style =
    "<style>html{color-scheme:light;background:#fff;} img{max-width:100%;height:auto;} a{color:#1565c0;}</style>";

  public static string Document(string? html) {
    var body = (html ?? "").Trim();
    if (LooksLikeDocument(body)) {
      if (Head.IsMatch(body))
        return Head.Replace(body, m => m.Value + Csp + Style, 1);
      return Csp + Style + body;
    }

    return """
<!DOCTYPE html>
<html><head>
<meta charset="utf-8">
""" + Csp + Style + """
</head>
<body style="margin:12px;background:#fff;color:#222;font:14px/1.45 Segoe UI, Ubuntu, sans-serif;">
""" + body + "</body></html>";
  }

  public static bool LooksLikeDocument(string? html) {
    var value = (html ?? "").TrimStart();
    return value.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
      || value.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
  }

  public static bool HasMarkup(string? html) {
    if (string.IsNullOrWhiteSpace(html))
      return false;
    return LooksLikeDocument(html) || Markup.IsMatch(html);
  }

  public static string FromPlain(string? text) {
    if (string.IsNullOrWhiteSpace(text))
      return "";
    return WebUtility.HtmlEncode(text).Replace("\r\n", "\n").Replace("\n", "<br>\n");
  }

  public static string InlineCid(string? html, IReadOnlyDictionary<string, string> dataUris) {
    if (string.IsNullOrWhiteSpace(html) || dataUris.Count == 0)
      return html ?? "";
    return Cid.Replace(html, match => {
      var id = WebUtility.UrlDecode(match.Groups[1].Value).Trim().Trim('<', '>');
      return dataUris.TryGetValue(id, out var uri) ? uri : match.Value;
    });
  }

  public static Dictionary<string, string> DataUris(
    IEnumerable<(string Id, string ContentType, byte[] Bytes)> files) {
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var file in files) {
      if (string.IsNullOrWhiteSpace(file.Id) || file.Bytes is not { Length: > 0 })
        continue;
      var type = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
      map[file.Id.Trim().Trim('<', '>')] = "data:" + type + ";base64," + Convert.ToBase64String(file.Bytes);
    }

    return map;
  }
}
