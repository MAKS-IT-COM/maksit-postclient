using System.Text.Json;
using MaksIT.Results;


namespace MaksIT.PostClient.Shared.Archive;


/// <summary>
/// Moves one opted-in mailbox between the user profile and the shared folder.
/// The account file has no password. Each user keeps their own secret.
/// </summary>
public static class SharedMailboxStore {
  private static readonly JsonSerializerOptions Json = new() {
    WriteIndented = true,
    PropertyNameCaseInsensitive = true
  };

  public static bool MergeMissing(Configuration configuration) {
    ArgumentNullException.ThrowIfNull(configuration);
    var added = false;
    foreach (var shared in List()) {
      if (configuration.FindMailbox(shared.Id) is not null)
        continue;
      if (!shared.Shared)
        continue;
      configuration.Mailboxes.Add(shared);
      added = true;
    }

    return added;
  }

  public static IReadOnlyList<MailboxAccount> List() {
    var root = SharedMailPaths.AccountsDirectory();
    if (!Directory.Exists(root))
      return [];
    var rows = new List<MailboxAccount>();
    foreach (var dir in Directory.GetDirectories(root)) {
      var file = Path.Combine(dir, "account.json");
      if (!File.Exists(file))
        continue;
      try {
        var box = JsonSerializer.Deserialize<MailboxAccount>(File.ReadAllText(file), Json);
        if (box is null || string.IsNullOrWhiteSpace(box.Id))
          continue;
        rows.Add(box);
      }
      catch {
      }
    }

    return rows;
  }

  public static Result Place(MailboxAccount account, bool shared, string privateRoot) {
    ArgumentNullException.ThrowIfNull(account);
    if (account.IsLocalStore)
      return Result.BadRequest("A local store keeps the folder you chose.");
    account.Shared = shared;
    var central = SharedMailPaths.AccountDirectory(account.Id);
    if (!privateRoot.Equals(central, StringComparison.OrdinalIgnoreCase)) {
      var moved = Move(privateRoot, central);
      if (!moved.IsSuccess)
        return moved;
    }

    account.ArchiveStoreId = "";
    return Publish(account);
  }

  public static Result Publish(MailboxAccount account) {
    try {
      var dir = SharedMailPaths.AccountDirectory(account.Id);
      Directory.CreateDirectory(dir);
      ShareWithOtherUsers(dir);
      var copy = Clone(account);
      copy.Shared = account.Shared;
      copy.ArchiveStoreId = "";
      File.WriteAllText(SharedMailPaths.AccountFile(account.Id), JsonSerializer.Serialize(copy, Json));
      if (!OperatingSystem.IsWindows())
        File.SetUnixFileMode(
          SharedMailPaths.AccountFile(account.Id),
          UnixFileMode.UserRead | UnixFileMode.UserWrite
            | UnixFileMode.GroupRead | UnixFileMode.GroupWrite
            | UnixFileMode.OtherRead | UnixFileMode.OtherWrite);
      return Result.Ok();
    }
    catch (Exception ex) {
      return Result.BadRequest(ex.Message);
    }
  }

  private static void RemovePublished(string mailboxId) {
    var file = SharedMailPaths.AccountFile(mailboxId);
    if (File.Exists(file))
      File.Delete(file);
    var dir = SharedMailPaths.AccountDirectory(mailboxId);
    if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0)
      Directory.Delete(dir);
  }

  private static Result Move(string source, string dest) {
    try {
      if (!Directory.Exists(source)) {
        Directory.CreateDirectory(dest);
        ShareWithOtherUsers(dest);
        return Result.Ok();
      }

      if (Directory.Exists(dest) && Directory.GetFileSystemEntries(dest).Length > 0)
        return Result.BadRequest("The shared folder already has mail for this account.");
      var parent = Path.GetDirectoryName(dest);
      if (!string.IsNullOrWhiteSpace(parent))
        Directory.CreateDirectory(parent);
      if (Directory.Exists(dest))
        Directory.Delete(dest);
      Directory.Move(source, dest);
      ShareWithOtherUsers(dest);
      return Result.Ok();
    }
    catch (Exception ex) {
      return Result.BadRequest(ex.Message);
    }
  }

  private static void ShareWithOtherUsers(string directory) {
    if (!Directory.Exists(directory))
      return;
    if (OperatingSystem.IsWindows()) {
      GrantUsersModify(directory);
      return;
    }

    File.SetUnixFileMode(
      directory,
      UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
  }

  private static void GrantUsersModify(string directory) {
    try {
      if (!OperatingSystem.IsWindows())
        return;
      var info = new DirectoryInfo(directory);
      var security = info.GetAccessControl();
      security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
        new System.Security.Principal.SecurityIdentifier(
          System.Security.Principal.WellKnownSidType.BuiltinUsersSid,
          null),
        System.Security.AccessControl.FileSystemRights.Modify,
        System.Security.AccessControl.InheritanceFlags.ContainerInherit
          | System.Security.AccessControl.InheritanceFlags.ObjectInherit,
        System.Security.AccessControl.PropagationFlags.None,
        System.Security.AccessControl.AccessControlType.Allow));
      info.SetAccessControl(security);
    }
    catch {
    }
  }

  private static MailboxAccount Clone(MailboxAccount account) =>
    JsonSerializer.Deserialize<MailboxAccount>(JsonSerializer.Serialize(account, Json), Json)
    ?? account;
}
