using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using MaksIT.Results;
using MaksIT.Core.Extensions;


namespace MaksIT.PostClient.Shared.Migration;


/// <summary>
/// Packs this PC's Postclient folders into a passphrase-locked <c>.postbundle</c>
/// and restores them into the folders the app uses on another PC.
/// </summary>
public static class EasyMigration {
  public const string Extension = ".postbundle";
  public const string WrongPassphrase = "wrong-passphrase";
  public const string Locked = "locked";

  private static readonly JsonSerializerOptions JsonOptions = new() {
    WriteIndented = true,
    PropertyNameCaseInsensitive = true
  };

  /// <summary>Writes a passphrase-locked bundle of <paramref name="source"/>.</summary>
  public static Result Create(MigrationLayout source, string destinationFile, string passphrase) {
    ArgumentNullException.ThrowIfNull(source);
    if (string.IsNullOrWhiteSpace(passphrase))
      return Result.BadRequest("Passphrase is required.");
    if (string.IsNullOrWhiteSpace(destinationFile))
      return Result.BadRequest("Bundle path is required.");

    var dest = destinationFile.Trim();
    if (!dest.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
      dest += Extension;

    var temp = Directory.CreateTempSubdirectory("postclient-bundle-");
    var partial = dest + ".partial";
    try {
      var zip = Path.Combine(temp.FullName, "plain.zip");
      WriteZip(zip, source);
      BundleCipher.Encrypt(zip, partial, passphrase);
      var folder = Path.GetDirectoryName(dest);
      if (!string.IsNullOrEmpty(folder))
        Directory.CreateDirectory(folder);
      File.Move(partial, dest, overwrite: true);
      return Result.Ok();
    }
    catch (IOException ex) {
      return Result.UnprocessableEntity(Locked + " " + ex.Message);
    }
    catch (UnauthorizedAccessException ex) {
      return Result.UnprocessableEntity(Locked + " " + ex.Message);
    }
    catch (Exception ex) {
      return Result.UnprocessableEntity(ex.Message);
    }
    finally {
      TryDelete(partial);
      TryDeleteTree(temp.FullName);
    }
  }

  /// <summary>
  /// Replaces <paramref name="destination"/> with the bundle.
  /// A wrong passphrase leaves those folders unchanged.
  /// </summary>
  public static Result Restore(string bundleFile, string passphrase, MigrationLayout destination) {
    ArgumentNullException.ThrowIfNull(destination);
    if (string.IsNullOrWhiteSpace(passphrase))
      return Result.BadRequest("Passphrase is required.");
    if (string.IsNullOrWhiteSpace(bundleFile) || !File.Exists(bundleFile))
      return Result.BadRequest("Bundle file is required.");

    var temp = Directory.CreateTempSubdirectory("postclient-bundle-");
    try {
      var zip = Path.Combine(temp.FullName, "plain.zip");
      BundleCipher.Decrypt(bundleFile, zip, passphrase);
      var extracted = Path.Combine(temp.FullName, "out");
      Directory.CreateDirectory(extracted);
      Extract(zip, extracted);
      return Apply(extracted, destination);
    }
    catch (CryptographicException) {
      return Result.Unauthorized(WrongPassphrase);
    }
    catch (InvalidDataException ex) {
      return Result.BadRequest(ex.Message);
    }
    catch (IOException ex) {
      return Result.UnprocessableEntity(Locked + " " + ex.Message);
    }
    catch (UnauthorizedAccessException ex) {
      return Result.UnprocessableEntity(Locked + " " + ex.Message);
    }
    catch (Exception ex) {
      return Result.UnprocessableEntity(ex.Message);
    }
    finally {
      TryDeleteTree(temp.FullName);
    }
  }

  /// <summary>True when the folders already hold mail, accounts, or secrets.</summary>
  public static bool HasExistingData(MigrationLayout layout) {
    ArgumentNullException.ThrowIfNull(layout);
    if (File.Exists(Path.Combine(layout.ConfigDirectory, "secrets.bin")))
      return true;
    if (File.Exists(Path.Combine(layout.DataDirectory, "mail.db")))
      return true;
    if (File.Exists(Path.Combine(layout.DataDirectory, "spam.db")))
      return true;
    if (HasEntries(Path.Combine(layout.DataDirectory, "accounts")))
      return true;
    if (HasEntries(Path.Combine(layout.DataDirectory, "stores")))
      return true;
    if (HasEntries(Path.Combine(layout.DataDirectory, "objects")))
      return true;
    if (HasEntries(Path.Combine(layout.SharedDirectory, "accounts")))
      return true;
    if (File.Exists(Path.Combine(layout.SharedDirectory, "service-secrets.bin")))
      return true;
    return SettingsHaveMailboxes(Path.Combine(layout.ConfigDirectory, "settings.json"));
  }

  private static void WriteZip(string zipPath, MigrationLayout source) {
    var config = ReadConfiguration(Path.Combine(source.ConfigDirectory, "settings.json"));
    var external = Externals(config, source);
    var manifest = new BundleManifest {
      Version = 1,
      ConfigRoot = Path.GetFullPath(source.ConfigDirectory),
      DataRoot = Path.GetFullPath(source.DataDirectory),
      SharedRoot = Path.GetFullPath(source.SharedDirectory),
      External = external
    };
    var userSecrets = ReadSecrets(Path.Combine(source.ConfigDirectory, "secrets.bin"), machine: false);
    var sharedSecrets = ReadSecrets(Path.Combine(source.SharedDirectory, "service-secrets.bin"), machine: true);

    using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
    AddText(zip, "manifest.json", manifest.ToJson());
    AddText(zip, "config/secrets.json", userSecrets.ToJson());
    AddText(zip, "shared/secrets.json", sharedSecrets.ToJson());
    AddOptionalFile(zip, Path.Combine(source.ConfigDirectory, "settings.json"), "config/settings.json");
    AddOptionalFile(zip, Path.Combine(source.ConfigDirectory, "receipts.json"), "config/receipts.json");
    AddDatabase(zip, source.DataDirectory, "mail.db", "data");
    AddDatabase(zip, source.DataDirectory, "spam.db", "data");
    AddDirectory(zip, Path.Combine(source.DataDirectory, "accounts"), "data/accounts");
    AddDirectory(zip, Path.Combine(source.DataDirectory, "stores"), "data/stores");
    AddDirectory(zip, Path.Combine(source.DataDirectory, "objects"), "data/objects");
    AddDirectory(zip, Path.Combine(source.SharedDirectory, "accounts"), "shared/accounts");
    AddDirectory(zip, Path.Combine(source.SharedDirectory, "models"), "shared/models");
    foreach (var item in external) {
      var prefix = "external/" + item.Id;
      if (item.IsFile)
        AddFile(zip, item.OriginalPath, prefix + "/" + Path.GetFileName(item.OriginalPath));
      else
        AddDirectory(zip, item.OriginalPath, prefix);
    }
  }

  private static Result Apply(string extracted, MigrationLayout destination) {
    var manifestPath = Path.Combine(extracted, "manifest.json");
    if (!File.Exists(manifestPath))
      return Result.BadRequest("Not a Postclient bundle.");
    var manifest = File.ReadAllText(manifestPath).ToObject<BundleManifest>();
    if (manifest is null || manifest.Version != 1)
      return Result.BadRequest("This bundle needs a newer Postclient.");

    Directory.CreateDirectory(destination.ConfigDirectory);
    Directory.CreateDirectory(destination.DataDirectory);
    Directory.CreateDirectory(destination.SharedDirectory);
    var writable = EnsureWritable(destination);
    if (!writable.IsSuccess)
      return writable;
    ClearData(destination);
    ClearShared(destination);
    CopyBundleData(extracted, destination);
    var placed = PlaceExternal(extracted, destination, manifest);
    CopyOptional(
      Path.Combine(extracted, "config", "settings.json"),
      Path.Combine(destination.ConfigDirectory, "settings.json"));
    CopyOptional(
      Path.Combine(extracted, "config", "receipts.json"),
      Path.Combine(destination.ConfigDirectory, "receipts.json"));
    RewriteSettings(Path.Combine(destination.ConfigDirectory, "settings.json"), manifest, destination, placed);
    WriteSecrets(extracted, destination);
    return Result.Ok();
  }

  private static Dictionary<string, string> PlaceExternal(
    string extracted,
    MigrationLayout destination,
    BundleManifest manifest) {
    var placed = new Dictionary<string, string>(StringComparer.Ordinal);
    var stores = Path.Combine(destination.DataDirectory, "stores");
    Directory.CreateDirectory(stores);
    foreach (var item in manifest.External) {
      var source = Path.Combine(extracted, "external", item.Id);
      if (!Directory.Exists(source))
        continue;
      var folder = UniqueDirectory(stores, string.IsNullOrWhiteSpace(item.FolderName) ? "store" : item.FolderName);
      Directory.CreateDirectory(folder);
      if (item.IsFile) {
        string? file = null;
        foreach (var candidate in Directory.EnumerateFiles(source)) {
          file = candidate;
          break;
        }

        if (file is null)
          continue;
        var target = Path.Combine(folder, Path.GetFileName(file));
        CopyFileShared(file, target);
        placed[item.Id] = target;
      }
      else {
        CopyDirectory(source, folder);
        placed[item.Id] = folder;
      }
    }

    return placed;
  }

  private static void RewriteSettings(
    string settingsPath,
    BundleManifest manifest,
    MigrationLayout destination,
    IReadOnlyDictionary<string, string> placed) {
    if (!File.Exists(settingsPath))
      return;
    var root = JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject;
    if (root?["Configuration"] is null)
      return;
    var config = root["Configuration"]!.Deserialize<Configuration>(JsonOptions) ?? new Configuration();
    config.Mailboxes ??= [];
    foreach (var box in config.Mailboxes) {
      box.StorePath = RewritePath(box.StorePath, manifest, destination, placed);
      box.ImapHost = RewritePath(box.ImapHost, manifest, destination, placed);
    }

    root["Configuration"] = JsonSerializer.SerializeToNode(config, JsonOptions);
    File.WriteAllText(settingsPath, root.ToJsonString(JsonOptions));
  }

  private static string RewritePath(
    string? path,
    BundleManifest manifest,
    MigrationLayout destination,
    IReadOnlyDictionary<string, string> placed) {
    if (string.IsNullOrWhiteSpace(path))
      return path ?? "";
    var trimmed = path.Trim();
    foreach (var item in manifest.External) {
      if (!placed.TryGetValue(item.Id, out var target))
        continue;
      if (!IsUnder(trimmed, item.OriginalPath))
        continue;
      return MapExternal(trimmed, item, target);
    }

    if (IsUnder(trimmed, manifest.DataRoot))
      return Swap(trimmed, manifest.DataRoot, destination.DataDirectory);
    if (IsUnder(trimmed, manifest.SharedRoot))
      return Swap(trimmed, manifest.SharedRoot, destination.SharedDirectory);
    if (IsUnder(trimmed, manifest.ConfigRoot))
      return Swap(trimmed, manifest.ConfigRoot, destination.ConfigDirectory);
    return trimmed;
  }

  private static string MapExternal(string path, BundleExternal item, string placed) {
    if (item.IsFile)
      return placed;
    return Swap(path, item.OriginalPath, placed);
  }

  private static List<BundleExternal> Externals(Configuration config, MigrationLayout source) {
    var list = new List<BundleExternal>();
    var seen = new HashSet<string>(PathComparer);
    config.Mailboxes ??= [];
    foreach (var box in config.Mailboxes) {
      Consider(box.StorePath);
      if (box.IsLocalStore)
        Consider(box.ImapHost);
    }

    return list;

    void Consider(string? path) {
      if (string.IsNullOrWhiteSpace(path))
        return;
      var trimmed = path.Trim();
      if (!PathExists(trimmed))
        return;
      if (IsUnder(trimmed, source.DataDirectory)
          || IsUnder(trimmed, source.SharedDirectory)
          || IsUnder(trimmed, source.ConfigDirectory))
        return;
      if (!TryFull(trimmed, out var full) || !seen.Add(full))
        return;
      var file = File.Exists(full);
      var stem = file
        ? Path.GetFileNameWithoutExtension(full)
        : Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
      list.Add(new BundleExternal {
        Id = Guid.NewGuid().ToString("N"),
        OriginalPath = full,
        FolderName = SafeName(stem),
        IsFile = file
      });
    }
  }

  private static void WriteSecrets(string extracted, MigrationLayout destination) {
    WriteSecretFile(
      Path.Combine(extracted, "config", "secrets.json"),
      Path.Combine(destination.ConfigDirectory, "secrets.bin"),
      machine: false,
      protectShared: false);
    WriteSecretFile(
      Path.Combine(extracted, "shared", "secrets.json"),
      Path.Combine(destination.SharedDirectory, "service-secrets.bin"),
      machine: true,
      protectShared: true);
  }

  private static void WriteSecretFile(string jsonPath, string destPath, bool machine, bool protectShared) {
    if (File.Exists(destPath))
      File.Delete(destPath);
    if (!File.Exists(jsonPath))
      return;
    var map = File.ReadAllText(jsonPath).ToObject<Dictionary<string, string>>();
    if (map is null || map.Count == 0)
      return;
    var store = machine ? FileSecretStore.OpenMachine(destPath) : FileSecretStore.OpenUser(destPath);
    store.ImportPlain(map);
    if (protectShared)
      SharedSecretStore.Protect(destPath);
  }

  private static Dictionary<string, string> ReadSecrets(string path, bool machine) {
    if (!File.Exists(path))
      return new Dictionary<string, string>(StringComparer.Ordinal);
    var store = machine ? FileSecretStore.OpenMachine(path) : FileSecretStore.OpenUser(path);
    return store.ExportPlain();
  }

  private static void CopyBundleData(string extracted, MigrationLayout destination) {
    var data = Path.Combine(extracted, "data");
    CopyDatabaseFile(data, destination.DataDirectory, "mail.db");
    CopyDatabaseFile(data, destination.DataDirectory, "spam.db");
    CopyDirectoryIfPresent(Path.Combine(data, "accounts"), Path.Combine(destination.DataDirectory, "accounts"));
    CopyDirectoryIfPresent(Path.Combine(data, "stores"), Path.Combine(destination.DataDirectory, "stores"));
    CopyDirectoryIfPresent(Path.Combine(data, "objects"), Path.Combine(destination.DataDirectory, "objects"));
    CopyDirectoryIfPresent(
      Path.Combine(extracted, "shared", "accounts"),
      Path.Combine(destination.SharedDirectory, "accounts"));
    CopyDirectoryIfPresent(
      Path.Combine(extracted, "shared", "models"),
      Path.Combine(destination.SharedDirectory, "models"));
  }

  private static Result EnsureWritable(MigrationLayout destination) {
    foreach (var root in new[] {
      Path.Combine(destination.DataDirectory, "accounts"),
      Path.Combine(destination.DataDirectory, "stores"),
      Path.Combine(destination.DataDirectory, "objects"),
      Path.Combine(destination.SharedDirectory, "accounts"),
      Path.Combine(destination.SharedDirectory, "models")
    }) {
      var tree = ProbeTree(root);
      if (!tree.IsSuccess)
        return tree;
    }

    foreach (var file in new[] {
      Path.Combine(destination.DataDirectory, "mail.db"),
      Path.Combine(destination.DataDirectory, "spam.db"),
      Path.Combine(destination.ConfigDirectory, "settings.json"),
      Path.Combine(destination.ConfigDirectory, "receipts.json"),
      Path.Combine(destination.ConfigDirectory, "secrets.bin"),
      Path.Combine(destination.SharedDirectory, "service-secrets.bin")
    }) {
      var probed = ProbeFile(file);
      if (!probed.IsSuccess)
        return probed;
    }

    return Result.Ok();
  }

  private static Result ProbeTree(string root) {
    if (!Directory.Exists(root))
      return Result.Ok();
    foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) {
      var probed = ProbeFile(file);
      if (!probed.IsSuccess)
        return probed;
    }

    return Result.Ok();
  }

  private static Result ProbeFile(string path) {
    if (!File.Exists(path))
      return Result.Ok();
    try {
      using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
      return Result.Ok();
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
      return Result.UnprocessableEntity(Locked + " " + ex.Message);
    }
  }

  private static void ClearData(MigrationLayout destination) {
    DeleteDirectory(Path.Combine(destination.DataDirectory, "accounts"));
    DeleteDirectory(Path.Combine(destination.DataDirectory, "stores"));
    DeleteDirectory(Path.Combine(destination.DataDirectory, "objects"));
    DeleteDatabase(destination.DataDirectory, "mail.db");
    DeleteDatabase(destination.DataDirectory, "spam.db");
  }

  private static void ClearShared(MigrationLayout destination) {
    DeleteDirectory(Path.Combine(destination.SharedDirectory, "accounts"));
    DeleteDirectory(Path.Combine(destination.SharedDirectory, "models"));
    var secrets = Path.Combine(destination.SharedDirectory, "service-secrets.bin");
    if (File.Exists(secrets))
      File.Delete(secrets);
  }

  private static void AddDatabase(ZipArchive zip, string directory, string name, string prefix) {
    AddOptionalFile(zip, Path.Combine(directory, name), prefix + "/" + name);
    AddOptionalFile(zip, Path.Combine(directory, name + "-wal"), prefix + "/" + name + "-wal");
    AddOptionalFile(zip, Path.Combine(directory, name + "-shm"), prefix + "/" + name + "-shm");
  }

  private static void AddDirectory(ZipArchive zip, string diskPath, string entryPrefix) {
    if (!Directory.Exists(diskPath))
      return;
    foreach (var file in Directory.EnumerateFiles(diskPath, "*", SearchOption.AllDirectories)) {
      var relative = Path.GetRelativePath(diskPath, file).Replace('\\', '/');
      AddFile(zip, file, entryPrefix + "/" + relative);
    }
  }

  private static void AddOptionalFile(ZipArchive zip, string diskPath, string entryName) {
    if (File.Exists(diskPath))
      AddFile(zip, diskPath, entryName);
  }

  private static void AddFile(ZipArchive zip, string diskPath, string entryName) {
    var entry = zip.CreateEntry(entryName.Replace('\\', '/'), CompressionLevel.Fastest);
    using var input = new FileStream(diskPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    using var output = entry.Open();
    input.CopyTo(output);
  }

  private static void AddText(ZipArchive zip, string entryName, string text) {
    var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
    using var output = entry.Open();
    using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    writer.Write(text);
  }

  private static void Extract(string zipPath, string destDir) {
    var root = Path.GetFullPath(destDir);
    using var zip = ZipFile.OpenRead(zipPath);
    foreach (var entry in zip.Entries) {
      if (string.IsNullOrEmpty(entry.Name))
        continue;
      var target = Path.GetFullPath(Path.Combine(root, entry.FullName));
      if (!IsUnder(target, root))
        throw new InvalidDataException("Bundle path is not allowed.");
      var parent = Path.GetDirectoryName(target);
      if (!string.IsNullOrEmpty(parent))
        Directory.CreateDirectory(parent);
      entry.ExtractToFile(target, overwrite: true);
    }
  }

  private static Configuration ReadConfiguration(string path) {
    if (!File.Exists(path))
      return new Configuration();
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    if (!document.RootElement.TryGetProperty("Configuration", out var value))
      return new Configuration();
    return JsonSerializer.Deserialize<Configuration>(value.GetRawText(), JsonOptions) ?? new Configuration();
  }

  private static bool SettingsHaveMailboxes(string path) {
    if (!File.Exists(path))
      return false;
    try {
      using var document = JsonDocument.Parse(File.ReadAllText(path));
      if (!document.RootElement.TryGetProperty("Configuration", out var configuration))
        return false;
      if (!configuration.TryGetProperty("Mailboxes", out var mailboxes))
        return false;
      return mailboxes.ValueKind == JsonValueKind.Array && mailboxes.GetArrayLength() > 0;
    }
    catch {
      return true;
    }
  }

  private static void CopyDatabaseFile(string sourceDir, string destDir, string name) {
    CopyOptional(Path.Combine(sourceDir, name), Path.Combine(destDir, name));
    CopyOptional(Path.Combine(sourceDir, name + "-wal"), Path.Combine(destDir, name + "-wal"));
    CopyOptional(Path.Combine(sourceDir, name + "-shm"), Path.Combine(destDir, name + "-shm"));
  }

  private static void CopyOptional(string source, string dest) {
    if (File.Exists(source))
      CopyFileShared(source, dest);
  }

  private static void CopyDirectoryIfPresent(string source, string dest) {
    if (Directory.Exists(source))
      CopyDirectory(source, dest);
  }

  private static void CopyDirectory(string source, string dest) {
    Directory.CreateDirectory(dest);
    foreach (var file in Directory.EnumerateFiles(source))
      CopyFileShared(file, Path.Combine(dest, Path.GetFileName(file)));
    foreach (var child in Directory.EnumerateDirectories(source))
      CopyDirectory(child, Path.Combine(dest, Path.GetFileName(child)));
  }

  private static void CopyFileShared(string source, string dest) {
    var parent = Path.GetDirectoryName(dest);
    if (!string.IsNullOrEmpty(parent))
      Directory.CreateDirectory(parent);
    IOException? last = null;
    for (var attempt = 0; attempt < 8; attempt++) {
      try {
        File.Copy(source, dest, overwrite: true);
        return;
      }
      catch (IOException ex) {
        last = ex;
        if (attempt < 7)
          Thread.Sleep(150);
      }
    }

    if (!TrySqliteBackup(source, dest))
      throw last ?? new IOException("The file is in use: " + source);
  }

  private static bool TrySqliteBackup(string source, string dest) {
    try {
      using var from = new SqliteConnection("Data Source=" + source + ";Mode=ReadOnly;Pooling=False");
      from.Open();
      if (File.Exists(dest))
        File.Delete(dest);
      using var to = new SqliteConnection("Data Source=" + dest + ";Pooling=False");
      to.Open();
      from.BackupDatabase(to);
      return true;
    }
    catch {
      return false;
    }
  }

  private static void DeleteDatabase(string directory, string name) {
    foreach (var suffix in new[] { "", "-wal", "-shm" }) {
      var path = Path.Combine(directory, name + suffix);
      if (File.Exists(path))
        File.Delete(path);
    }
  }

  private static void DeleteDirectory(string path) {
    if (Directory.Exists(path))
      Directory.Delete(path, recursive: true);
  }

  private static string UniqueDirectory(string parent, string stem) {
    var candidate = Path.Combine(parent, stem);
    if (!Directory.Exists(candidate) && !File.Exists(candidate))
      return candidate;
    for (var i = 2; i < 10_000; i++) {
      var next = Path.Combine(parent, stem + "-" + i);
      if (!Directory.Exists(next) && !File.Exists(next))
        return next;
    }

    return Path.Combine(parent, stem + "-" + Guid.NewGuid().ToString("N")[..8]);
  }

  private static string Swap(string path, string oldRoot, string newRoot) {
    if (!TryFull(path, out var full) || !TryFull(oldRoot, out var root))
      return path;
    var relative = Path.GetRelativePath(root, full);
    return Path.GetFullPath(Path.Combine(newRoot, relative));
  }

  private static bool IsUnder(string path, string root) {
    if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
      return false;
    if (!TryFull(path, out var full) || !TryFull(root, out var basePath))
      return false;
    if (full.Equals(basePath, PathComparison))
      return true;
    var prefix = basePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
      + Path.DirectorySeparatorChar;
    return full.StartsWith(prefix, PathComparison);
  }

  private static bool PathExists(string path) =>
    File.Exists(path) || Directory.Exists(path);

  private static bool TryFull(string path, out string full) {
    try {
      full = Path.GetFullPath(path);
      return true;
    }
    catch {
      full = "";
      return false;
    }
  }

  private static bool HasEntries(string path) =>
    Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();

  private static string SafeName(string? stem) {
    var name = string.IsNullOrWhiteSpace(stem) ? "store" : stem.Trim();
    foreach (var c in Path.GetInvalidFileNameChars())
      name = name.Replace(c, '_');
    return string.IsNullOrWhiteSpace(name) ? "store" : name;
  }

  private static void TryDelete(string path) {
    try {
      if (File.Exists(path))
        File.Delete(path);
    }
    catch {
    }
  }

  private static void TryDeleteTree(string path) {
    try {
      if (Directory.Exists(path))
        Directory.Delete(path, recursive: true);
    }
    catch {
    }
  }

  private static StringComparison PathComparison =>
    OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
      ? StringComparison.OrdinalIgnoreCase
      : StringComparison.Ordinal;

  private static StringComparer PathComparer =>
    OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
      ? StringComparer.OrdinalIgnoreCase
      : StringComparer.Ordinal;
}


internal sealed class BundleManifest {
  public int Version { get; set; } = 1;

  public string ConfigRoot { get; set; } = "";

  public string DataRoot { get; set; } = "";

  public string SharedRoot { get; set; } = "";

  public List<BundleExternal> External { get; set; } = [];
}


internal sealed class BundleExternal {
  public string Id { get; set; } = "";

  public string OriginalPath { get; set; } = "";

  public string FolderName { get; set; } = "";

  public bool IsFile { get; set; }
}
