using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MaksIT.Results;


namespace MaksIT.PostClient.Shared;


public sealed class FileSecretStore : ISecretStore {
  private readonly string _path;
  private readonly Lock _gate = new();

  public FileSecretStore(string? path = null) {
    _path = string.IsNullOrWhiteSpace(path) ? AppPaths.SecretsFile() : path;
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

  private Dictionary<string, string> Load() {
    if (!File.Exists(_path))
      return new Dictionary<string, string>(StringComparer.Ordinal);
    try {
      var json = File.ReadAllText(_path);
      return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
        ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }
    catch {
      return new Dictionary<string, string>(StringComparer.Ordinal);
    }
  }

  private void Save(Dictionary<string, string> map) {
    var dir = Path.GetDirectoryName(_path);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);
    File.WriteAllText(_path, JsonSerializer.Serialize(map));
    if (!OperatingSystem.IsWindows())
      File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
  }

  private static string Protect(string secret) {
    var bytes = Encoding.UTF8.GetBytes(secret);
    if (OperatingSystem.IsWindows())
      bytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
    return Convert.ToBase64String(bytes);
  }

  private static string Unprotect(string packed) {
    var bytes = Convert.FromBase64String(packed);
    if (OperatingSystem.IsWindows())
      bytes = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
    return Encoding.UTF8.GetString(bytes);
  }
}
