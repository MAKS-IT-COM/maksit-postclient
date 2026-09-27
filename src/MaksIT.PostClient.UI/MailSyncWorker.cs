using Microsoft.Extensions.Hosting;


namespace MaksIT.PostClient.UI;


public sealed class MailSyncWorker : BackgroundService {
  private readonly ConfigurationFileService _files;
  private readonly MailArchiveCatalog _archive;
  private readonly ISecretStore _secrets;
  private readonly IMailAuthService _auth;
  private readonly IMailSessionFactory _sessions;
  private readonly SemanticSearchService _semantic;

  public MailSyncWorker(
    ConfigurationFileService files,
    MailArchiveCatalog archive,
    ISecretStore secrets,
    IMailAuthService auth,
    IMailSessionFactory sessions,
    SemanticSearchService semantic) {
    _files = files;
    _archive = archive;
    _secrets = secrets;
    _auth = auth;
    _sessions = sessions;
    _semantic = semantic;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
    AppPaths.EnsureDirectories();
    _files.Current.EnsureDefaults();
    PstStoreUpgrade.Apply(_files, _archive);
    LegacyArchiveUpgrade.Apply(_files.Current.Mailboxes);
    _archive.OpenAll(_files.Current.Mailboxes);
    var engine = new MailSyncEngine(_files, _archive, _secrets, _auth, _sessions, _semantic);
    await engine.RunAsync(stoppingToken).ConfigureAwait(false);
  }
}
