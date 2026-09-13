using System.Security.Cryptography;
using System.Text;


namespace MaksIT.PostClient.Shared;


public static class ArchiveFiles {
  public static string EmlPath(string mailboxId, string folder, uint uid) {
    AppPaths.EnsureDirectories();
    var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(folder ?? ""))[..4]);
    var dir = Path.Combine(AppPaths.ObjectsDirectory(), mailboxId, key);
    Directory.CreateDirectory(dir);
    return Path.Combine(dir, uid + ".eml");
  }

  public static string Hint() =>
    UiLocale.Copy.ArchivePathHint(AppPaths.DataDirectory());

  public static void ExportTo(string destDir) {
    ArgumentException.ThrowIfNullOrWhiteSpace(destDir);
    Directory.CreateDirectory(destDir);
    var db = AppPaths.ArchiveDatabase();
    if (File.Exists(db))
      File.Copy(db, Path.Combine(destDir, "mail.db"), overwrite: true);
    var objects = AppPaths.ObjectsDirectory();
    if (Directory.Exists(objects))
      CopyDirectory(objects, Path.Combine(destDir, "objects"));
  }

  private static void CopyDirectory(string source, string dest) {
    Directory.CreateDirectory(dest);
    foreach (var file in Directory.EnumerateFiles(source))
      File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
    foreach (var child in Directory.EnumerateDirectories(source))
      CopyDirectory(child, Path.Combine(dest, Path.GetFileName(child)));
  }
}
