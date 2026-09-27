using System.Security.Principal;


namespace MaksIT.PostClient.Shared.App;


/// <summary>
/// Background sync may run only as the interactive user who owns the mailbox secrets.
/// A machine account (Local System, root) can read every profile on a shared PC.
/// </summary>
public static class MailSyncIdentity {
  public static bool IsSharedAccount(string? name) {
    if (string.IsNullOrWhiteSpace(name))
      return true;
    var leaf = name.Trim();
    var slash = leaf.LastIndexOf('\\');
    if (slash >= 0 && slash < leaf.Length - 1)
      leaf = leaf[(slash + 1)..];
    return leaf.Equals("root", StringComparison.OrdinalIgnoreCase)
      || leaf.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase)
      || leaf.Equals("LOCALSYSTEM", StringComparison.OrdinalIgnoreCase)
      || leaf.Equals("LocalService", StringComparison.OrdinalIgnoreCase)
      || leaf.Equals("NetworkService", StringComparison.OrdinalIgnoreCase);
  }

  public static bool IsMachineSid(string? sid) =>
    sid is "S-1-5-18" or "S-1-5-19" or "S-1-5-20";

  public static bool LivesInHome(string? home, string? path) {
    if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(path))
      return false;
    var root = Path.GetFullPath(home.Trim());
    var full = Path.GetFullPath(path.Trim());
    var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
      + Path.DirectorySeparatorChar;
    return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
      || full.Equals(root, StringComparison.OrdinalIgnoreCase);
  }

  public static bool IsServiceAccount() {
    if (OperatingSystem.IsWindows()) {
      try {
        using var current = WindowsIdentity.GetCurrent();
        if (current.User?.Value == "S-1-5-19")
          return true;
      }
      catch {
      }
    }

    var name = Environment.UserName ?? "";
    return name.Equals("postclient", StringComparison.OrdinalIgnoreCase)
      || name.Equals("LocalService", StringComparison.OrdinalIgnoreCase);
  }

  public static bool IsOwnAccount(out string reason) {
    if (Environment.IsPrivilegedProcess) {
      reason = "Background sync must run as your own account, not as administrator or root.";
      return false;
    }

    if (IsSharedAccount(Environment.UserName)) {
      reason = "Background sync cannot run as a shared machine account.";
      return false;
    }

    if (OperatingSystem.IsWindows()) {
      using var current = WindowsIdentity.GetCurrent();
      var sid = current.User?.Value;
      if (current.IsSystem || IsMachineSid(sid)) {
        reason = "Background sync cannot run as Local System or a machine service account.";
        return false;
      }
    }

    reason = "";
    return true;
  }
}
