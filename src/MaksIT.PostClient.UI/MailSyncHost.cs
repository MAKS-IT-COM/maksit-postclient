using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MaksIT.PostClient.Client.Extensions;


namespace MaksIT.PostClient.UI;


public sealed class MailSyncHost {
  public static bool IsSyncProcess(string[] args) =>
    args.Length > 0 && string.Equals(args[0], MailSyncRegistrar.Switch, StringComparison.OrdinalIgnoreCase);

  public static int Run(string[] args) {
    var server = MailSyncIdentity.IsServiceAccount();
    if (!server && !MailSyncIdentity.IsOwnAccount(out var reason)) {
      AppLog.Write(reason);
      return 2;
    }

    var builder = Host.CreateApplicationBuilder(args);
    if (OperatingSystem.IsWindows())
      builder.Services.AddWindowsService(options => options.ServiceName = "MaksIT Postclient Sync");
    else if (OperatingSystem.IsLinux())
      builder.Services.AddSystemd();
    builder.Services.AddSingleton(_ => new ConfigurationFileService());
    builder.Services.AddPostClient();
    if (server)
      builder.Services.AddSingleton<ISecretStore>(_ => new SharedSecretStore());
    builder.Services.AddHostedService<MailSyncWorker>();
    builder.Build().Run();
    return 0;
  }
}
