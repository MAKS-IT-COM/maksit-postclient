using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;


namespace MaksIT.PostClient.Shared.Migration;


internal static class BundleCipher {
  public const string Magic = "PCBUNDLE";
  public const ushort Version = 1;
  public const int Iterations = 200_000;
  private const int SaltLength = 16;
  private const int KeyLength = 32;
  private const int NoncePrefixLength = 4;
  private const int TagLength = 16;
  private const int ChunkSize = 1024 * 1024;
  private const int HeaderLength = 8 + 2 + 4 + SaltLength + NoncePrefixLength;

  public static void Encrypt(string sourcePath, string destPath, string passphrase) {
    var salt = RandomNumberGenerator.GetBytes(SaltLength);
    var prefix = RandomNumberGenerator.GetBytes(NoncePrefixLength);
    var key = Derive(passphrase, salt, Iterations);
    var dir = Path.GetDirectoryName(destPath);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);

    using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    using var output = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
    Span<byte> header = stackalloc byte[HeaderLength];
    Encoding.ASCII.GetBytes(Magic, header);
    BinaryPrimitives.WriteUInt16LittleEndian(header[8..], Version);
    BinaryPrimitives.WriteUInt32LittleEndian(header[10..], Iterations);
    salt.CopyTo(header[14..]);
    prefix.CopyTo(header[(14 + SaltLength)..]);
    output.Write(header);

    using var aes = new AesGcm(key, TagLength);
    var plain = new byte[ChunkSize];
    var cipher = new byte[ChunkSize];
    var tag = new byte[TagLength];
    var nonce = new byte[12];
    prefix.CopyTo(nonce);
    ulong counter = 0;
    Span<byte> length = stackalloc byte[4];
    int read;
    while ((read = input.Read(plain, 0, plain.Length)) > 0) {
      BinaryPrimitives.WriteUInt64LittleEndian(nonce.AsSpan(NoncePrefixLength), counter);
      counter++;
      aes.Encrypt(nonce, plain.AsSpan(0, read), cipher.AsSpan(0, read), tag);
      BinaryPrimitives.WriteUInt32LittleEndian(length, (uint)read);
      output.Write(length);
      output.Write(cipher, 0, read);
      output.Write(tag);
    }

    Span<byte> end = stackalloc byte[4];
    output.Write(end);
  }

  public static void Decrypt(string sourcePath, string destPath, string passphrase) {
    using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    var header = new byte[HeaderLength];
    ReadExact(input, header, header.Length);
    var magic = Encoding.ASCII.GetString(header, 0, 8);
    if (!string.Equals(magic, Magic, StringComparison.Ordinal))
      throw new InvalidDataException("Not a Postclient bundle.");
    var version = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(8));
    if (version != Version)
      throw new InvalidDataException("This bundle needs a newer Postclient.");
    var iterations = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(10));
    if (iterations is < 1 or > 1_000_000)
      throw new InvalidDataException("Not a Postclient bundle.");

    var salt = header.AsSpan(14, SaltLength).ToArray();
    var prefix = header.AsSpan(14 + SaltLength, NoncePrefixLength).ToArray();
    var key = Derive(passphrase, salt, (int)iterations);
    var dir = Path.GetDirectoryName(destPath);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);

    using var output = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
    using var aes = new AesGcm(key, TagLength);
    var cipher = new byte[ChunkSize];
    var plain = new byte[ChunkSize];
    var tag = new byte[TagLength];
    var nonce = new byte[12];
    prefix.CopyTo(nonce);
    var lengthBytes = new byte[4];
    ulong counter = 0;
    while (true) {
      ReadExact(input, lengthBytes, 4);
      var length = BinaryPrimitives.ReadUInt32LittleEndian(lengthBytes);
      if (length == 0)
        break;
      if (length > ChunkSize)
        throw new InvalidDataException("Not a Postclient bundle.");
      ReadExact(input, cipher, (int)length);
      ReadExact(input, tag, TagLength);
      BinaryPrimitives.WriteUInt64LittleEndian(nonce.AsSpan(NoncePrefixLength), counter);
      counter++;
      aes.Decrypt(nonce, cipher.AsSpan(0, (int)length), tag, plain.AsSpan(0, (int)length));
      output.Write(plain, 0, (int)length);
    }
  }

  private static byte[] Derive(string passphrase, byte[] salt, int iterations) =>
    Rfc2898DeriveBytes.Pbkdf2(
      Encoding.UTF8.GetBytes(passphrase),
      salt,
      iterations,
      HashAlgorithmName.SHA256,
      KeyLength);

  private static void ReadExact(Stream input, byte[] buffer, int count) {
    var offset = 0;
    while (offset < count) {
      var read = input.Read(buffer, offset, count - offset);
      if (read == 0)
        throw new InvalidDataException("Not a Postclient bundle.");
      offset += read;
    }
  }
}
