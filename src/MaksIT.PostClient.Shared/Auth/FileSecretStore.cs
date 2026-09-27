using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MaksIT.Results;


namespace MaksIT.PostClient.Shared.Auth;


public sealed class FileSecretStore : ISecretStore {
  private readonly string _path;
  private readonly DataProtectionScope _scope;
  private readonly Lock _gate = new();

  /// <summary>Secrets protected for the current user.</summary>
  public static FileSecretStore OpenUser(string path) =>
    new(path);

  /// <summary>Secrets protected for this machine, so the sync service can read them.</summary>
  public static FileSecretStore OpenMachine(string path) =>
    new(path, DataProtectionScope.LocalMachine);

  public FileSecretStore(string? path = null, DataProtectionScope scope = DataProtectionScope.CurrentUser) {
    _path = string.IsNullOrWhiteSpace(path) ? AppPaths.SecretsFile() : path;
    _scope = scope;
  }

  public Result Put(string key, string secret) {
    if (string.IsNullOrWhiteSpace(key))
      return Result.BadRequest("Secret key is required.");

    lock (_gate) {
      var map = Load();
      map[key] = Protect(secret);
      Save(map);
    }

    return Result.Ok();
  }

  public Result<string?> Get(string key) {
    if (string.IsNullOrWhiteSpace(key))
      return Result<string?>.BadRequest(null, "Secret key is required.");

    lock (_gate) {
      var map = Load();
      if (!map.TryGetValue(key, out var packed))
        return Result<string?>.Ok(null);
      try {
        return Result<string?>.Ok(Unprotect(packed));
      }
      catch (Exception ex) {
        return Result<string?>.UnprocessableEntity(null, ex.Message);
      }
    }
  }

  public Result Delete(string key) {
    lock (_gate) {
      var map = Load();
      map.Remove(key);
      Save(map);
    }

    return Result.Ok();
  }

  public static string MailboxKey(string mailboxId) =>
    "mailbox:" + mailboxId;

  public static string OAuthKey(string mailboxId) =>
    "oauth:" + mailboxId;

  public static string OAuthClientSecretKey(string? authKind) =>
    "oauth-client-secret:" + MailAuthKind.Normalize(authKind);

  public Dictionary<string, string> ExportPlain() {
    lock (_gate) {
      var plain = new Dictionary<string, string>(StringComparer.Ordinal);
      foreach (var pair in Load())
        plain[pair.Key] = Unprotect(pair.Value);
      return plain;
    }
  }

  public void ImportPlain(IReadOnlyDictionary<string, string> plain) {
    ArgumentNullException.ThrowIfNull(plain);
    lock (_gate) {
      var map = new Dictionary<string, string>(StringComparer.Ordinal);
      foreach (var pair in plain) {
        if (string.IsNullOrWhiteSpace(pair.Key))
          continue;
        map[pair.Key] = Protect(pair.Value ?? "");
      }

      Save(map);
    }
  }

  public static int MergeMissingKeys(string sourcePath, string destPath) {
    var source = ReadMap(sourcePath);
    if (source.Count == 0)
      return 0;
    var dest = ReadMap(destPath);
    var added = 0;
    foreach (var pair in source) {
      if (dest.ContainsKey(pair.Key))
        continue;
      dest[pair.Key] = pair.Value;
      added++;
    }

    if (added > 0)
      WriteMap(destPath, dest);
    return added;
  }

  private Dictionary<string, string> Load() =>
    ReadMap(_path);

  private void Save(Dictionary<string, string> map) =>
    WriteMap(_path, map);

  private static Dictionary<string, string> ReadMap(string path) {
    if (!File.Exists(path))
      return new Dictionary<string, string>(StringComparer.Ordinal);
    try {
      var json = File.ReadAllText(path);
      return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
        ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }
    catch {
      return new Dictionary<string, string>(StringComparer.Ordinal);
    }
  }

  private static void WriteMap(string path, Dictionary<string, string> map) {
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);
    File.WriteAllText(path, JsonSerializer.Serialize(map));
    if (!OperatingSystem.IsWindows())
      File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
  }

  private string Protect(string secret) {
    var bytes = Encoding.UTF8.GetBytes(secret);
    if (OperatingSystem.IsWindows())
      bytes = ProtectedData.Protect(bytes, null, _scope);
    return Convert.ToBase64String(bytes);
  }

  private string Unprotect(string packed) {
    var bytes = Convert.FromBase64String(packed);
    if (OperatingSystem.IsWindows())
      bytes = ProtectedData.Unprotect(bytes, null, _scope);
    return Encoding.UTF8.GetString(bytes);
  }
}
