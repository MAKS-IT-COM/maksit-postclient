using System.Text.Json;


namespace MaksIT.PostClient.Shared.Auth;


public static class OAuthClientId {
  public const string GoogleSuffix = ".apps.googleusercontent.com";

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

  public static bool IsGoogle(string? value) {
    var id = Normalize(value);
    return id.EndsWith(GoogleSuffix, StringComparison.OrdinalIgnoreCase)
      && id.Contains('-', StringComparison.Ordinal)
      && !id.Contains('@', StringComparison.Ordinal);
  }

  public static bool IsMicrosoft(string? value) =>
    Guid.TryParse(Normalize(value), out var guid) && guid != Guid.Empty;

  public static string Preview(string? value) {
    var id = Normalize(value);
    if (id.Length == 0)
      return "";
    var dash = id.IndexOf('-', StringComparison.Ordinal);
    if (dash > 0 && dash + 5 < id.Length)
      return id[..(dash + 5)] + "…" + (id.EndsWith(GoogleSuffix, StringComparison.OrdinalIgnoreCase)
        ? GoogleSuffix
        : id[^Math.Min(8, id.Length)..]);
    if (id.Length <= 20)
      return id;
    return id[..8] + "…" + id[^6..];
  }

  private static string? FromJson(string json) {
    try {
      using var document = JsonDocument.Parse(json);
      return ReadClientId(document.RootElement);
    }
    catch (JsonException) {
      return null;
    }
  }

  private static string? ReadClientId(JsonElement root) {
    if (root.ValueKind != JsonValueKind.Object)
      return null;
    if (Text(root, "client_id") is { Length: > 0 } direct)
      return direct;
    foreach (var name in new[] { "installed", "web" }) {
      if (root.TryGetProperty(name, out var nested) && Text(nested, "client_id") is { Length: > 0 } child)
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
