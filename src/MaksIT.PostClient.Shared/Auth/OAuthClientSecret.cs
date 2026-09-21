using System.Text.Json;


namespace MaksIT.PostClient.Shared.Auth;


public static class OAuthClientSecret {
  public static string Normalize(string? value) {
    var text = (value ?? "").Trim().Trim('"');
    if (text.Length == 0)
      return "";
    if (text[0] == '{') {
      var fromJson = FromJson(text);
      if (!string.IsNullOrWhiteSpace(fromJson))
        return fromJson;
    }

    return text;
  }

  private static string? FromJson(string json) {
    try {
      using var document = JsonDocument.Parse(json);
      return ReadSecret(document.RootElement);
    }
    catch (JsonException) {
      return null;
    }
  }

  private static string? ReadSecret(JsonElement root) {
    if (root.ValueKind != JsonValueKind.Object)
      return null;
    if (Text(root, "client_secret") is { Length: > 0 } direct)
      return direct;
    foreach (var name in new[] { "installed", "web" }) {
      if (root.TryGetProperty(name, out var nested) && Text(nested, "client_secret") is { Length: > 0 } child)
        return child;
    }

    return null;
  }

  private static string? Text(JsonElement root, string name) {
    if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
      return null;
    var text = value.GetString()?.Trim();
    return string.IsNullOrWhiteSpace(text) ? null : text;
  }
}
