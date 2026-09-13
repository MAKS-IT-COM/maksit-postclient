using System.IO.Compression;


namespace MaksIT.PostClient.Client;


public static class AttachmentZip {
  public static byte[] FromFiles(IEnumerable<MailFileAttachment> files) {
    ArgumentNullException.ThrowIfNull(files);
    using var output = new MemoryStream();
    using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true)) {
      var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var file in files) {
        var name = Unique(used, SafeName(file.Name));
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        stream.Write(file.Bytes);
      }
    }

    return output.ToArray();
  }

  public static string SuggestedName(string? subject) {
    var stem = subject ?? "";
    foreach (var ch in Path.GetInvalidFileNameChars())
      stem = stem.Replace(ch, '-');
    stem = stem.Trim().Trim('-');
    if (stem.Length > 60)
      stem = stem[..60].TrimEnd('-');
    if (stem.Length == 0)
      stem = "message";
    return stem + "-attachments.zip";
  }

  internal static string SafeName(string? name) {
    var file = Path.GetFileName((name ?? "").Replace('\\', '/'));
    foreach (var ch in Path.GetInvalidFileNameChars())
      file = file.Replace(ch, '_');
    if (string.IsNullOrWhiteSpace(file) || file is "." or "..")
      return "attachment";
    return file;
  }

  internal static string Unique(HashSet<string> used, string name) {
    var candidate = name;
    var stem = Path.GetFileNameWithoutExtension(name);
    var ext = Path.GetExtension(name);
    var n = 2;
    while (!used.Add(candidate)) {
      candidate = stem + " (" + n + ")" + ext;
      n++;
    }

    return candidate;
  }
}
