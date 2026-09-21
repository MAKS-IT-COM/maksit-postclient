using System.Net;
using System.Text.RegularExpressions;


namespace MaksIT.PostClient.Shared.Content;


public static class MessageText {
  public static string StripHtml(string? html) {
    if (string.IsNullOrWhiteSpace(html))
      return "";
    var text = Regex.Replace(html, "<[^>]+>", " ");
    return WebUtility.HtmlDecode(text).Trim();
  }
}
