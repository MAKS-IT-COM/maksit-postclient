using System.Diagnostics;
using System.Security.Principal;
using MaksIT.Results;


namespace MaksIT.PostClient.Client.Mail;


/// <summary>
/// Registers the mail sync server.
/// Windows: an auto-start service as LocalService. Linux: a system systemd unit as user postclient.
/// It starts at boot, keeps running with nobody signed in, and syncs every enrolled mailbox.
/// </summary>
public sealed class MailSyncRegistrar {
  public const string Switch = "--sync";
  public const string UnitName = "maksit-postclient-sync.service";
  public const string WindowsServiceName = "MaksITPostclientSync";
  public const string LinuxServiceUser = "postclient";

  public static string LinuxUnitPath() =>
    Path.Combine("/etc/systemd/system", UnitName);

  public static string LinuxUnit(string executable) {
    var start = executable.Contains(' ') || executable.Contains('\t')
      ? "\"" + executable + "\" " + Switch
      : executable + " " + Switch;
    return "[Unit]\n"
      + "Description=MaksIT Postclient mail sync\n"
      + "After=network-online.target\n"
      + "\n"
      + "[Service]\n"
      + "Type=notify\n"
      + "User=" + LinuxServiceUser + "\n"
      + "Group=" + LinuxServiceUser + "\n"
      + "ExecStart=" + start + "\n"
      + "Restart=on-failure\n"
      + "RestartSec=15\n"
      + "NoNewPrivileges=true\n"
      + "ProtectHome=read-only\n"
      + "ReadWritePaths=/var/lib/maksit/postclient\n"
      + "\n"
      + "[Install]\n"
      + "WantedBy=multi-user.target\n";
  }

  public static string WindowsCreateArguments(string executable) =>
    "create " + WindowsServiceName
    + " binPath= \"\\\"" + executable + "\\\" " + Switch + "\""
    + " start= auto obj= \"NT AUTHORITY\\LocalService\""
    + " DisplayName= \"MaksIT Postclient Sync\"";

  public static string WindowsDeleteArguments() =>
    "delete " + WindowsServiceName;

  public static string WindowsQueryArguments() =>
    "query " + WindowsServiceName;

  public bool IsInstalled() {
    if (OperatingSystem.IsWindows())
      return Run("sc.exe", WindowsQueryArguments(), elevate: false).IsSuccess;
    if (OperatingSystem.IsLinux())
      return File.Exists(LinuxUnitPath());
    return false;
  }

  public Result Install(string? executable = null) {
    var exe = string.IsNullOrWhiteSpace(executable) ? Environment.ProcessPath : executable;
    if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
      return Result.BadRequest("The Postclient program file was not found.");
    if (OperatingSystem.IsWindows())
      return InstallWindows(exe);
    if (OperatingSystem.IsLinux())
      return InstallLinux(exe);
    return Result.BadRequest("Background sync can be registered on Windows and Linux.");
  }

  public Result Remove() {
    if (OperatingSystem.IsWindows()) {
      Run("sc.exe", "stop " + WindowsServiceName, elevate: true);
      return Run("sc.exe", WindowsDeleteArguments(), elevate: true);
    }

    if (OperatingSystem.IsLinux())
      return RemoveLinux();
    return Result.BadRequest("Background sync can be registered on Windows and Linux.");
  }

  private static Result InstallWindows(string executable) {
    if (Run("sc.exe", WindowsQueryArguments(), elevate: false).IsSuccess)
      return Run("sc.exe", "start " + WindowsServiceName, elevate: true);
    var created = Run("sc.exe", WindowsCreateArguments(executable), elevate: true);
    if (!created.IsSuccess)
      return created;
    Run("sc.exe", "description " + WindowsServiceName + " \"Syncs shared Postclient mailboxes.\"", elevate: true);
    return Run("sc.exe", "start " + WindowsServiceName, elevate: true);
  }

  private static Result InstallLinux(string executable) {
    var unit = Path.Combine(Path.GetTempPath(), UnitName);
    File.WriteAllText(unit, LinuxUnit(executable));
    var script = "id " + LinuxServiceUser + " >/dev/null 2>&1 || useradd --system --home-dir /var/lib/maksit/postclient --shell /usr/sbin/nologin " + LinuxServiceUser
      + " && install -d -o " + LinuxServiceUser + " -g " + LinuxServiceUser + " /var/lib/maksit/postclient"
      + " && cp " + Quote(unit) + " " + LinuxUnitPath()
      + " && systemctl daemon-reload && systemctl enable --now " + UnitName;
    return Run("pkexec", "sh -c " + Quote(script), elevate: false);
  }

  private static Result RemoveLinux() =>
    Run("pkexec", "sh -c " + Quote(
      "systemctl disable --now " + UnitName
      + "; rm -f " + LinuxUnitPath()
      + "; systemctl daemon-reload"), elevate: false);

  private static string Quote(string value) =>
    "'" + value.Replace("'", "'\\''") + "'";

  private static Result Run(string file, string arguments, bool elevate) {
    try {
      if (elevate && OperatingSystem.IsWindows() && !IsWindowsAdmin()) {
        using var elevated = Process.Start(new ProcessStartInfo(file, arguments) {
          UseShellExecute = true,
          Verb = "runas",
          WindowStyle = ProcessWindowStyle.Hidden
        });
        if (elevated is null)
          return Result.BadRequest("Could not start " + file + ".");
        if (!elevated.WaitForExit(120000))
          return Result.BadRequest(file + " did not finish.");
        return elevated.ExitCode == 0
          ? Result.Ok()
          : Result.BadRequest("The service command failed. Approve the administrator prompt and try again.");
      }

      var start = new ProcessStartInfo(file, arguments) {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };
      using var process = Process.Start(start);
      if (process is null)
        return Result.BadRequest("Could not start " + file + ".");
      var stdout = process.StandardOutput.ReadToEndAsync();
      var stderr = process.StandardError.ReadToEndAsync();
      if (!process.WaitForExit(20000)) {
        try {
          process.Kill(entireProcessTree: true);
        }
        catch {
        }

        return Result.BadRequest(file + " did not finish.");
      }

      Task.WaitAll(stdout, stderr);
      if (process.ExitCode == 0)
        return Result.Ok();
      var detail = string.IsNullOrWhiteSpace(stderr.Result) ? stdout.Result : stderr.Result;
      detail = detail.Trim();
      return Result.BadRequest(string.IsNullOrWhiteSpace(detail) ? file + " failed." : detail);
    }
    catch (Exception ex) {
      return Result.BadRequest(ex.Message);
    }
  }

  private static bool IsWindowsAdmin() {
    using var current = WindowsIdentity.GetCurrent();
    return new WindowsPrincipal(current).IsInRole(WindowsBuiltInRole.Administrator);
  }
}
