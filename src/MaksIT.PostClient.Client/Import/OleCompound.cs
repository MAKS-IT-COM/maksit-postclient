using System.Text;


namespace MaksIT.PostClient.Client.Import;


internal static class OleCompound {
  private static readonly byte[] Signature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

  public static bool IsOle(byte[] bytes) =>
    bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(Signature);

  public static IReadOnlyList<byte[]> StreamBytes(byte[] file) {
    var streams = new List<byte[]>();
    if (!IsOle(file) || file.Length < 0x200)
      return streams;
    var shift = BitConverter.ToUInt16(file, 0x1E);
    var sector = 1 << (shift == 0 ? 9 : shift);
    var fatCount = BitConverter.ToInt32(file, 0x2C);
    var dirStart = BitConverter.ToInt32(file, 0x30);
    if (sector < 512 || fatCount <= 0 || dirStart < 0)
      return streams;
    var fat = new List<int>();
    for (var i = 0; i < 109; i++) {
      var sec = BitConverter.ToInt32(file, 0x4C + i * 4);
      if (sec >= 0)
        fat.Add(sec);
    }

    var fatMap = ReadFat(file, sector, fat);
    var directory = ReadChain(file, sector, fatMap, dirStart);
    for (var i = 0; i + 128 <= directory.Length; i += 128) {
      var type = directory[i + 0x42];
      if (type != 2)
        continue;
      var nameBytes = BitConverter.ToUInt16(directory, i + 0x40);
      var start = BitConverter.ToInt32(directory, i + 0x74);
      var size = BitConverter.ToInt32(directory, i + 0x78);
      if (size <= 0 || size > file.Length || start < 0)
        continue;
      _ = Encoding.Unicode.GetString(directory, i, Math.Min(64, Math.Max(0, nameBytes - 2)));
      var data = ReadChain(file, sector, fatMap, start);
      if (data.Length > size)
        data = data[..size];
      if (data.Length > 0)
        streams.Add(data);
    }

    return streams;
  }

  private static Dictionary<int, int> ReadFat(byte[] file, int sector, List<int> fatSectors) {
    var map = new Dictionary<int, int>();
    foreach (var fatSec in fatSectors) {
      var offset = (fatSec + 1) * sector;
      if (offset + sector > file.Length)
        continue;
      for (var i = 0; i < sector; i += 4) {
        var next = BitConverter.ToInt32(file, offset + i);
        var index = (offset - sector) / 4 + i / 4;
        if (index >= 0)
          map[index] = next;
      }
    }

    return map;
  }

  private static byte[] ReadChain(byte[] file, int sector, Dictionary<int, int> fat, int start) {
    using var buffer = new MemoryStream();
    var current = start;
    var guard = 0;
    while (current >= 0 && guard++ < 1_000_000) {
      var offset = (current + 1) * sector;
      if (offset + sector > file.Length)
        break;
      buffer.Write(file, offset, sector);
      if (!fat.TryGetValue(current, out var next) || next < 0)
        break;
      current = next;
    }

    return buffer.ToArray();
  }
}
