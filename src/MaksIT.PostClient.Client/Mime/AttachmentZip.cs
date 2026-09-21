using System.IO.Compression;
using ICSharpCode.SharpZipLib.Zip;


namespace MaksIT.PostClient.Client.Mime;


public static class AttachmentZip {
  public static byte[] FromFiles(IEnumerable<MailFileAttachment> files, string? password = null) {
    ArgumentNullException.ThrowIfNull(files);
    var rows = files.ToList();
    return string.IsNullOrEmpty(password) ? FromPlain(rows) : FromEncrypted(rows, password);
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

  public static string FileName(string? name) {
    var file = SafeName(name);
    if (!file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
      file += ".zip";
    return file;
  }

  private static byte[] FromPlain(IReadOnlyList<MailFileAttachment> files) {
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

  private static byte[] FromEncrypted(IReadOnlyList<MailFileAttachment> files, string password) {
    using var output = new MemoryStream();
    using (var zip = new ZipOutputStream(output) { IsStreamOwner = false }) {
      zip.SetLevel(1);
      zip.Password = password;
      var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var file in files) {
        var name = Unique(used, SafeName(file.Name));
        var entry = new ZipEntry(ZipEntry.CleanName(name)) {
          DateTime = DateTime.UtcNow,
          Size = file.Bytes.Length
        };
        zip.PutNextEntry(entry);
        zip.Write(file.Bytes, 0, file.Bytes.Length);
        zip.CloseEntry();
      }

      zip.Finish();
    }

    return output.ToArray();
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
