using System.Text.Json;


namespace MaksIT.PostClient.Shared;


public static class HubDesktopSession {
  public static string? Normalize(string? raw) {
    var text = Unwrap(raw);
    if (text is null)
      return null;
    if (!text.Contains("token", StringComparison.OrdinalIgnoreCase))
      return null;
    return text;
  }

  public static bool IsComplete(string? raw) {
    var text = Normalize(raw);
    if (text is null)
      return false;
    try {
      using var document = JsonDocument.Parse(text);
      return HasText(document.RootElement, "token")
        && HasText(document.RootElement, "refreshToken");
    }
    catch (JsonException) {
      return false;
    }
  }

  private static bool HasText(JsonElement root, string name) {
    foreach (var property in root.EnumerateObject()) {
      if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        continue;
      return property.Value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(property.Value.GetString());
    }

    return false;
  }

  private static string? Unwrap(string? raw) {
    if (string.IsNullOrWhiteSpace(raw))
      return null;

    var text = raw.Trim();
    for (var i = 0; i < 4; i++) {
      if (text is "null" or "undefined" or "\"\"" or "''")
        return null;
      if (text.Length < 2 || text[0] != '"')
        break;

      try {
        var inner = JsonSerializer.Deserialize<string>(text);
        if (string.IsNullOrWhiteSpace(inner) || inner == text)
          break;
        text = inner.Trim();
      }
      catch (JsonException) {
        break;
      }
    }

    if (string.IsNullOrWhiteSpace(text) || text is "null" or "undefined")
      return null;
    return text;
  }
}
