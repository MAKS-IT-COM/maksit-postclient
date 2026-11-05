using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using MaksIT.Results;


namespace MaksIT.PostClient.Shared.Auth;


/// <summary>
/// Machine-scoped secrets for shared mailboxes and the sync service.
/// Windows protects them with the machine key so LocalService and other users on this PC can read them.
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

  /// <summary>
  /// Moves mailbox password and OAuth tokens from the user store into the shared store.
  /// </summary>
  public static Result PublishMailbox(ISecretStore user, string mailboxId) {
    ArgumentNullException.ThrowIfNull(user);
    if (string.IsNullOrWhiteSpace(mailboxId))
      return Result.BadRequest("Mailbox id is required.");
    var shared = new SharedSecretStore();
    foreach (var key in MailboxKeys(mailboxId)) {
      var stored = user.Get(key);
      if (!stored.IsSuccess || string.IsNullOrWhiteSpace(stored.Value))
        continue;
      var saved = shared.Put(key, stored.Value);
      if (!saved.IsSuccess)
        return saved;
      user.Delete(key);
    }

    return Result.Ok();
  }

  /// <summary>
  /// Moves mailbox password and OAuth tokens from the shared store back into the user store.
  /// </summary>
  public static Result WithdrawMailbox(ISecretStore user, string mailboxId) {
    ArgumentNullException.ThrowIfNull(user);
    if (string.IsNullOrWhiteSpace(mailboxId))
      return Result.BadRequest("Mailbox id is required.");
    var shared = new SharedSecretStore();
    foreach (var key in MailboxKeys(mailboxId)) {
      var stored = shared.Get(key);
      if (stored.IsSuccess && !string.IsNullOrWhiteSpace(stored.Value)) {
        var saved = user.Put(key, stored.Value);
        if (!saved.IsSuccess)
          return saved;
      }

      shared.Delete(key);
    }

    return Result.Ok();
  }

  public static void Protect(string path) =>
    Harden(path);

  private static IEnumerable<string> MailboxKeys(string mailboxId) {
    yield return FileSecretStore.MailboxKey(mailboxId);
    yield return FileSecretStore.OAuthKey(mailboxId);
  }

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
      security.AddAccessRule(UsersRule(FileSystemRights.ReadAndExecute));
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

  private static FileSystemAccessRule UsersRule(FileSystemRights rights) =>
    new(
      new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
      rights,
      InheritanceFlags.None,
      PropagationFlags.None,
      AccessControlType.Allow);
}
