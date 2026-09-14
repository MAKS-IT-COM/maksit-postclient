namespace MaksIT.PostClient.Client;


public static class PstFile {
  public static bool IsName(string? name) {
    var value = name ?? "";
    return value.EndsWith(".pst", StringComparison.OrdinalIgnoreCase)
      || value.EndsWith(".ost", StringComparison.OrdinalIgnoreCase);
  }

  public static bool IsStore(byte[]? bytes) =>
    bytes is { Length: >= 4 }
    && bytes[0] == (byte)'!'
    && bytes[1] == (byte)'B'
    && bytes[2] == (byte)'D'
    && bytes[3] == (byte)'N';

  public static string TempPath(string? name) {
    var ext = (name ?? "").EndsWith(".ost", StringComparison.OrdinalIgnoreCase) ? ".ost" : ".pst";
    return Path.Combine(Path.GetTempPath(), "postclient-pst-" + Guid.NewGuid().ToString("N") + ext);
  }
}
