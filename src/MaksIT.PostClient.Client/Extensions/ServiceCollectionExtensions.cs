using Microsoft.Extensions.DependencyInjection;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public static class ServiceCollectionExtensions {
  /// <summary>
  /// Registers the IMAP/POP3/SMTP session factory and OS secret store.
  /// </summary>
  public static IServiceCollection AddPostClient(this IServiceCollection services) {
    ArgumentNullException.ThrowIfNull(services);
    services.AddSingleton<ISecretStore, FileSecretStore>();
    services.AddSingleton<ISentReceiptStore, FileSentReceiptStore>();
    services.AddSingleton<IMailAuthService, MailAuthService>();
    services.AddSingleton<IMailSessionFactory, MailSessionFactory>();
    services.AddSingleton<MailArchiveStore>();
    return services;
  }
}
