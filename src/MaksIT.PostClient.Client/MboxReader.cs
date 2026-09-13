using System.Text;


namespace MaksIT.PostClient.Client;


public static class MboxReader {
  public static IReadOnlyList<byte[]> Messages(string path) {
    using var stream = File.OpenRead(path);
    return Messages(stream);
  }

  public static IReadOnlyList<byte[]> Messages(Stream stream) {
    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
    var rows = new List<byte[]>();
    StringBuilder? current = null;
    while (reader.ReadLine() is { } line) {
      if (line.StartsWith("From ", StringComparison.Ordinal) && LooksSeparator(line)) {
        Flush(rows, current);
        current = new StringBuilder();
        continue;
      }

      current ??= new StringBuilder();
      if (line.StartsWith(">From ", StringComparison.Ordinal))
        current.AppendLine(line[1..]);
      else
        current.AppendLine(line);
    }

    Flush(rows, current);
    return rows;
  }

  internal static bool LooksSeparator(string line) {
    if (!line.StartsWith("From ", StringComparison.Ordinal))
      return false;
    return line.Length > 5;
  }

  private static void Flush(List<byte[]> rows, StringBuilder? current) {
    if (current is null || current.Length == 0)
      return;
    rows.Add(Encoding.UTF8.GetBytes(current.ToString()));
  }
}
