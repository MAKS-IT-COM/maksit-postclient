using OfficeIMO.Email.Store;


namespace MaksIT.PostClient.Client;


public static class OutlookPst {
  public const string MailContainer = "IPF.Note";

  public static void CreateEmpty(string path, string displayName = "Postclient") {
    ArgumentException.ThrowIfNullOrWhiteSpace(path);
    using var writer = EmailStorePstWriter.Create(
      path,
      new EmailStorePstWriterOptions(displayName: displayName));
    writer.AddFolder("Inbox", containerClass: MailContainer);
    writer.AddFolder("Drafts", containerClass: MailContainer);
    writer.AddFolder("Sent Items", containerClass: MailContainer);
    writer.AddFolder("Deleted Items", containerClass: MailContainer);
    writer.Complete();
  }

  public static string SiblingUnicodePstPath(string source) {
    var directory = Path.GetDirectoryName(source);
    var stem = Path.GetFileNameWithoutExtension(source);
    if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(stem))
      throw new InvalidOperationException("Cannot choose a writable PST path next to " + source);

    var pst = Path.Combine(directory, stem + ".pst");
    if (!File.Exists(pst) || pst.Equals(source, StringComparison.OrdinalIgnoreCase))
      return pst;

    var copy = UnicodeCopyPath(source);
    if (!File.Exists(copy))
      return copy;

    return Path.Combine(directory, stem + ".postclient-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".pst");
  }

  public static string UnicodeCopyPath(string source) {
    var directory = Path.GetDirectoryName(source);
    var stem = Path.GetFileNameWithoutExtension(source);
    if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(stem))
      throw new InvalidOperationException("Cannot choose a Unicode PST path next to " + source);
    return Path.Combine(directory, stem + ".postclient.pst");
  }

  public static bool LooksLikeOst(string path) =>
    Path.GetExtension(path).Equals(".ost", StringComparison.OrdinalIgnoreCase);

  public static bool IsAnsi(string path) {
    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
      return false;
    try {
      using var stream = File.OpenRead(path);
      Span<byte> header = stackalloc byte[12];
      if (stream.Read(header) < 12)
        return false;
      if (header[0] != (byte)'!' || header[1] != (byte)'B' || header[2] != (byte)'D' || header[3] != (byte)'N')
        return false;
      var version = BitConverter.ToUInt16(header[10..12]);
      return version is 14 or 15;
    }
    catch {
      return false;
    }
  }
}
