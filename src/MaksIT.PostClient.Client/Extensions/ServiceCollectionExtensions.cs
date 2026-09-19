using Microsoft.Extensions.DependencyInjection;
using MaksIT.IdentityHub.Client;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public static class ServiceCollectionExtensions {
  /// <summary>
  /// Registers the IMAP/POP3/SMTP session factory, archive catalog, worker, and Identity Hub client.
  /// </summary>
  public static IServiceCollection AddPostClient(this IServiceCollection services) {
    ArgumentNullException.ThrowIfNull(services);
    services.AddIdentityHubClient(IdentityHubAddress.Origin());
    services.AddSingleton<ISecretStore, FileSecretStore>();
    services.AddSingleton<ISentReceiptStore, FileSentReceiptStore>();
    services.AddSingleton<IMailAuthService, MailAuthService>();
    services.AddSingleton<IMailSessionFactory, MailSessionFactory>();
    services.AddSingleton<MailArchiveCatalog>();
    services.AddSingleton<SemanticSearchService>();
    services.AddSingleton<ISemanticSearchService>(sp => sp.GetRequiredService<SemanticSearchService>());
    services.AddSingleton<IAppUpdateService, AppUpdateService>();
    services.AddSingleton(sp => MailWorkerClient.Create(
      sp.GetRequiredService<MailArchiveCatalog>(),
      sp.GetRequiredService<ConfigurationFileService>(),
      Environment.ProcessPath));
    return services;
  }
}
