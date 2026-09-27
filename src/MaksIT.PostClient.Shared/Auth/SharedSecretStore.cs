using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using MaksIT.Results;


namespace MaksIT.PostClient.Shared.Auth;


/// <summary>
/// Secrets for mailboxes the machine service syncs.
/// Windows protects them with the machine key so LocalService can read them after logoff.
/// The file is not granted to every user.
/// </summary>
public sealed class SharedSecretStore : ISecretStore {
  public static string FilePath() =>
    Path.Combine(SharedMailPaths.Root(), "service-secrets.bin");

  private readonly FileSecretStore _inner = new(FilePath(), DataProtectionScope.LocalMachine);

  public Result Put(string key, string secret) {
    var saved = _inner.Put(key, secret);
    if (saved.IsSuccess)
      Harden(FilePath());
    return saved;
  }

  public Result<string?> Get(string key) =>
    _inner.Get(key);

  public Result Delete(string key) =>
    _inner.Delete(key);

  public static void Protect(string path) =>
    Harden(path);

  private static void Harden(string path) {
    try {
      if (!File.Exists(path))
        return;
      if (!OperatingSystem.IsWindows()) {
        File.SetUnixFileMode(
          path,
          UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite);
        return;
      }

      var info = new FileInfo(path);
      var security = info.GetAccessControl();
      security.AddAccessRule(ServiceRule(FileSystemRights.Modify));
      info.SetAccessControl(security);
    }
    catch {
    }
  }

  private static FileSystemAccessRule ServiceRule(FileSystemRights rights) =>
    new(
      new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null),
      rights,
      InheritanceFlags.None,
      PropagationFlags.None,
      AccessControlType.Allow);
}
