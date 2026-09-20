using System.Text;
using MaksIT.IdentityHub.Client;
using MaksIT.IdentityHub.Contracts.Health;
using MaksIT.IdentityHub.Contracts.Identity;
using MaksIT.IdentityHub.Contracts.Identity.Login;
using MaksIT.IdentityHub.Contracts.Identity.Mailbox;
using MaksIT.PostClient.Client;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Tests;


public class IdentityHubAddressTests {
  [Fact]
  public void DesktopLoginUrl_UsesOfficialPathAndProvider() {
    var previous = Environment.GetEnvironmentVariable(IdentityHubAddress.Env);
    try {
      Environment.SetEnvironmentVariable(IdentityHubAddress.Env, null);
      var google = IdentityHubAddress.DesktopLoginUrl(MailAuthKind.Google);
      Assert.StartsWith(IdentityHubAddress.Production, google);
      Assert.Contains("/desktop-login?provider=Google", google);
      Assert.DoesNotContain("127.0.0.1", google);
      Assert.DoesNotContain("purpose=", google);
      var microsoft = IdentityHubAddress.DesktopLoginUrl(MailAuthKind.Microsoft);
      Assert.Contains("/desktop-login?provider=Microsoft", microsoft);
    }
    finally {
      Environment.SetEnvironmentVariable(IdentityHubAddress.Env, previous);
    }
  }

  [Fact]
  public void IsHubUrl_AcceptsHubHostOnly() {
    var previous = Environment.GetEnvironmentVariable(IdentityHubAddress.Env);
    try {
      Environment.SetEnvironmentVariable(IdentityHubAddress.Env, null);
      Assert.True(IdentityHubAddress.IsHubUrl(new Uri("https://identity.maks-it.com/desktop-callback")));
      Assert.True(IdentityHubAddress.IsHubUrl(new Uri("https://identity.maks-it.com/login-external/callback")));
      Assert.False(IdentityHubAddress.IsHubUrl(new Uri("https://login.microsoftonline.com/common/oauth2/v2.0/authorize")));
      Assert.False(IdentityHubAddress.IsHubUrl(new Uri("about:blank")));
      Assert.False(IdentityHubAddress.IsHubUrl(null));
    }
    finally {
      Environment.SetEnvironmentVariable(IdentityHubAddress.Env, previous);
    }
  }
}


public class MailAuthServiceHubTests {
  [Fact]
  public async Task ResolveAsync_UsesMailboxTokenNotHubJwt() {
    var secrets = new FileSecretStore(Path.Combine(Path.GetTempPath(), "postclient-hub-" + Guid.NewGuid().ToString("N") + ".bin"));
    var hub = new FakeIdentityHub();
    var auth = new MailAuthService(secrets, hub);
    var box = new MailboxAccount { AuthKind = MailAuthKind.Google };
    auth.SaveTokens(box.Id, new OAuthTokenSet {
      HubToken = "hub-jwt",
      HubRefreshToken = "hub-refresh",
      HubExpires = DateTimeOffset.UtcNow.AddMinutes(1),
      Email = "user@gmail.com"
    });
    var material = await auth.ResolveAsync(box, password: null, TestContext.Current.CancellationToken);
    Assert.True(material.IsSuccess);
    Assert.Equal("imap-mailbox-token", material.Value?.AccessToken);
    Assert.NotEqual("hub-jwt", material.Value?.AccessToken);
    Assert.Equal("hub-jwt-refreshed", hub.AccessToken);
    Assert.Equal(1, hub.RefreshCalls);
  }

  [Fact]
  public async Task ResolveAsync_RefreshesHubSessionWhenStillValid() {
    var secrets = new FileSecretStore(Path.Combine(Path.GetTempPath(), "postclient-hub-" + Guid.NewGuid().ToString("N") + ".bin"));
    var hub = new FakeIdentityHub();
    var auth = new MailAuthService(secrets, hub);
    var box = new MailboxAccount { AuthKind = MailAuthKind.Microsoft };
    auth.SaveTokens(box.Id, new OAuthTokenSet {
      HubToken = "hub-jwt",
      HubRefreshToken = "hub-refresh",
      HubExpires = DateTimeOffset.UtcNow.AddHours(8),
      Email = "user@outlook.com"
    });
    var material = await auth.ResolveAsync(box, password: null, TestContext.Current.CancellationToken);
    Assert.True(material.IsSuccess);
    Assert.Equal(1, hub.RefreshCalls);
    Assert.Equal("hub-jwt-refreshed", hub.AccessToken);
    var stored = auth.HasTokens(box.Id);
    Assert.True(stored);
  }

  [Fact]
  public void CompleteHubSignIn_ReadsRefreshToken() {
    var secrets = new FileSecretStore(Path.Combine(Path.GetTempPath(), "postclient-hub-" + Guid.NewGuid().ToString("N") + ".bin"));
    var auth = new MailAuthService(secrets, new FakeIdentityHub());
    var result = auth.CompleteHubSignIn(
      """{"token":"hub-jwt","refreshToken":"hub-refresh","expiresAt":"2030-01-01T00:00:00Z","username":"user@gmail.com"}""",
      MailAuthKind.Google);
    Assert.True(result.IsSuccess);
    Assert.Equal("hub-jwt", result.Value?.HubToken);
    Assert.Equal("hub-refresh", result.Value?.HubRefreshToken);
    Assert.Equal("user@gmail.com", result.Value?.Email);
    Assert.Equal(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero), result.Value?.HubExpires);
  }

  [Fact]
  public void CompleteHubSignIn_TreatsUnspecifiedExpiryAsUtc() {
    var secrets = new FileSecretStore(Path.Combine(Path.GetTempPath(), "postclient-hub-" + Guid.NewGuid().ToString("N") + ".bin"));
    var auth = new MailAuthService(secrets, new FakeIdentityHub());
    var result = auth.CompleteHubSignIn(
      """{"token":"hub-jwt","refreshToken":"hub-refresh","expiresAt":"2030-06-15T12:00:00","username":"user@gmail.com"}""",
      MailAuthKind.Google);
    Assert.True(result.IsSuccess);
    Assert.Equal(new DateTimeOffset(2030, 6, 15, 12, 0, 0, TimeSpan.Zero), result.Value?.HubExpires);
  }

  [Fact]
  public void CompleteHubSignIn_RejectsJwtOnly() {
    var secrets = new FileSecretStore(Path.Combine(Path.GetTempPath(), "postclient-hub-" + Guid.NewGuid().ToString("N") + ".bin"));
    var auth = new MailAuthService(secrets, new FakeIdentityHub());
    var result = auth.CompleteHubSignIn("""{"token":"hub-jwt"}""", MailAuthKind.Google);
    Assert.False(result.IsSuccess);
  }

  [Fact]
  public void CompleteHubSignIn_UnwrapsInvokeScriptString() {
    var secrets = new FileSecretStore(Path.Combine(Path.GetTempPath(), "postclient-hub-" + Guid.NewGuid().ToString("N") + ".bin"));
    var auth = new MailAuthService(secrets, new FakeIdentityHub());
    var wrapped = "\"{\\\"token\\\":\\\"hub-jwt\\\",\\\"refreshToken\\\":\\\"hub-refresh\\\",\\\"username\\\":\\\"user@gmail.com\\\"}\"";
    var result = auth.CompleteHubSignIn(wrapped, MailAuthKind.Google);
    Assert.True(result.IsSuccess);
    Assert.Equal("hub-jwt", result.Value?.HubToken);
    Assert.Equal("hub-refresh", result.Value?.HubRefreshToken);
    Assert.Equal("user@gmail.com", result.Value?.Email);
  }

  [Fact]
  public async Task CompleteHubSignInAsync_FetchesMailboxToken() {
    var secrets = new FileSecretStore(Path.Combine(Path.GetTempPath(), "postclient-hub-" + Guid.NewGuid().ToString("N") + ".bin"));
    var auth = new MailAuthService(secrets, new FakeIdentityHub());
    var result = await auth.CompleteHubSignInAsync(
      """{"token":"hub-jwt","refreshToken":"hub-refresh","username":"user@outlook.com"}""",
      MailAuthKind.Microsoft,
      TestContext.Current.CancellationToken);
    Assert.True(result.IsSuccess);
    Assert.Equal("hub-jwt", result.Value?.HubToken);
    Assert.Equal("imap-mailbox-token", result.Value?.AccessToken);
    Assert.Equal("user@outlook.com", result.Value?.Email);
  }

  [Fact]
  public async Task ResolveAsync_UsesCachedMailboxTokenWhenHubRefreshFails() {
    var secrets = new FileSecretStore(Path.Combine(Path.GetTempPath(), "postclient-hub-" + Guid.NewGuid().ToString("N") + ".bin"));
    var hub = new FakeIdentityHub { FailRefresh = true };
    var auth = new MailAuthService(secrets, hub);
    var box = new MailboxAccount { AuthKind = MailAuthKind.Google };
    auth.SaveTokens(box.Id, new OAuthTokenSet {
      HubToken = "hub-jwt",
      HubRefreshToken = "hub-refresh",
      HubExpires = DateTimeOffset.UtcNow.AddHours(1),
      AccessToken = "cached-imap",
      AccessExpires = DateTimeOffset.UtcNow.AddMinutes(30),
      Email = "user@gmail.com"
    });
    var material = await auth.ResolveAsync(box, password: null, TestContext.Current.CancellationToken);
    Assert.True(material.IsSuccess);
    Assert.Equal("cached-imap", material.Value?.AccessToken);
    Assert.Equal(1, hub.RefreshCalls);
  }
}


public class HubDesktopSessionTests {
  [Fact]
  public void Normalize_RejectsEmpty() {
    Assert.Null(HubDesktopSession.Normalize(null));
    Assert.Null(HubDesktopSession.Normalize(""));
    Assert.Null(HubDesktopSession.Normalize("null"));
    Assert.Null(HubDesktopSession.Normalize("\"\""));
  }

  [Fact]
  public void Normalize_UnwrapsQuotedJson() {
    var raw = "\"{\\\"token\\\":\\\"abc\\\"}\"";
    Assert.Equal("""{"token":"abc"}""", HubDesktopSession.Normalize(raw));
  }

  [Fact]
  public void IsComplete_RequiresRefreshToken() {
    Assert.False(HubDesktopSession.IsComplete("""{"token":"abc"}"""));
    Assert.False(HubDesktopSession.IsComplete("""{"token":"abc","refreshToken":""}"""));
    Assert.True(HubDesktopSession.IsComplete("""{"token":"abc","refreshToken":"xyz"}"""));
    Assert.True(HubDesktopSession.IsComplete("""{"Token":"abc","RefreshToken":"xyz"}"""));
  }
}


file sealed class FakeIdentityHub : IIdentityHubClient {
  public string? AccessToken { get; set; }

  public int RefreshCalls { get; private set; }

  public bool FailRefresh { get; set; }

  public Task CheckHealthLiveAsync(CancellationToken cancellationToken = default) =>
    Task.CompletedTask;

  public Task CheckHealthReadyAsync(CancellationToken cancellationToken = default) =>
    Task.CompletedTask;

  public Task<StartupHealthResponse> GetStartupHealthAsync(CancellationToken cancellationToken = default) =>
    throw new NotImplementedException();

  public Task<CoveredApplicationsResponse> GetCoveredApplicationsAsync(CancellationToken cancellationToken = default) =>
    throw new NotImplementedException();

  public Task<string> GetLoginExternalChallengeAsync(
    string provider,
    string postLogin,
    string codeChallenge,
    bool mailboxPurpose = false,
    CancellationToken cancellationToken = default) =>
    throw new NotImplementedException();

  public Task<LoginResponse> RedeemLoginExternalAsync(
    string code,
    string codeVerifier,
    CancellationToken cancellationToken = default) =>
    throw new NotImplementedException();

  public Task<MailboxAccessTokenResponse> GetMailboxAccessTokenAsync(CancellationToken cancellationToken = default) =>
    Task.FromResult(new MailboxAccessTokenResponse { AccessToken = "imap-mailbox-token" });

  public Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
    throw new NotImplementedException();

  public Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default) {
    RefreshCalls++;
    if (FailRefresh)
      throw new IdentityHubApiException(401, "refresh failed", "", null);
    return Task.FromResult(new LoginResponse(
      "Bearer",
      "hub-jwt-refreshed",
      DateTime.UtcNow.AddHours(1),
      "hub-refresh-2",
      DateTime.UtcNow.AddDays(1),
      "user@gmail.com"));
  }

  public Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default) =>
    Task.CompletedTask;
}


public class LocalStoreTests {
  [Fact]
  public void ProposedStoreDirectory_LivesUnderDataStoresStem() {
    var previous = Environment.GetEnvironmentVariable(AppPaths.DataEnv);
    var root = Path.Combine(Path.GetTempPath(), "postclient-data-" + Guid.NewGuid().ToString("N"));
    try {
      Environment.SetEnvironmentVariable(AppPaths.DataEnv, root);
      var path = AppPaths.ProposedStoreDirectory("D:\\mail\\archive.pst");
      Assert.Equal(Path.Combine(AppPaths.StoresDirectory(), "archive"), path);
    }
    finally {
      Environment.SetEnvironmentVariable(AppPaths.DataEnv, previous);
      if (Directory.Exists(root))
        Directory.Delete(root, recursive: true);
    }
  }

  [Fact]
  public void CreateStore_WritesSidecarAndSystemFolders() {
    var dir = Path.Combine(Path.GetTempPath(), "postclient-store-" + Guid.NewGuid().ToString("N"));
    try {
      var box = new MailboxAccount {
        DisplayName = "Mail",
        Address = "Mail",
        StorePath = dir,
        IncomingProtocol = MailProtocol.Store,
        Provider = MailProvider.Store
      };
      LocalStoreSidecar.Write(dir, box.Id, box.Label);
      MailArchiveLayout.EnsureSystemFolders(dir);
      var sidecar = LocalStoreSidecar.TryRead(dir);
      Assert.NotNull(sidecar);
      Assert.Equal(box.Id, sidecar!.Id);
      foreach (var folder in MailArchiveLayout.SystemFolders)
        Assert.True(Directory.Exists(Path.Combine(dir, folder)));
    }
    finally {
      if (Directory.Exists(dir))
        Directory.Delete(dir, recursive: true);
    }
  }

  [Fact]
  public void AttachStore_SameSidecarId_KeepsMailboxId() {
    var first = Path.Combine(Path.GetTempPath(), "postclient-store-" + Guid.NewGuid().ToString("N"));
    var second = Path.Combine(Path.GetTempPath(), "postclient-store-" + Guid.NewGuid().ToString("N"));
    try {
      var id = Guid.NewGuid().ToString("N");
      LocalStoreSidecar.Write(first, id, "Mail");
      MailArchiveLayout.EnsureSystemFolders(first);
      using var catalog = new MailArchiveCatalog();
      catalog.Open(id, Path.Combine(first, "mail.db"));
      catalog.UpsertBody(
        id,
        new MailArchiveHeader { Uid = 1, Folder = "Inbox", Subject = "IMU avviso", From = "a@b.c", Date = DateTimeOffset.UtcNow },
        Path.Combine(first, "Inbox", "1.eml"),
        "pagamento IMU",
        "");
      CopyTree(first, second);
      catalog.Close(id);
      catalog.Open(id, Path.Combine(second, "mail.db"));
      Assert.Single(catalog.Search(id, "Inbox", "IMU"));
      Assert.Equal(id, LocalStoreSidecar.TryRead(second)!.Id);
    }
    finally {
      if (Directory.Exists(first))
        Directory.Delete(first, recursive: true);
      if (Directory.Exists(second))
        Directory.Delete(second, recursive: true);
    }
  }

  [Fact]
  public void SearchAll_MergesAccountAndStoreDatabases() {
    var root = Path.Combine(Path.GetTempPath(), "postclient-cat-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try {
      using var catalog = new MailArchiveCatalog();
      catalog.Open("imap", Path.Combine(root, "imap.db"));
      catalog.Open("store", Path.Combine(root, "store.db"));
      catalog.UpsertBody(
        "imap",
        new MailArchiveHeader { Uid = 1, Folder = "INBOX", Subject = "Condominio X", From = "a@b.c", Date = DateTimeOffset.UtcNow },
        "a.eml",
        "avviso IMU",
        "");
      catalog.UpsertBody(
        "store",
        new MailArchiveHeader { Uid = 2, Folder = "Inbox", Subject = "Fattura", From = "c@d.e", Date = DateTimeOffset.UtcNow },
        "b.eml",
        "pagamento IMU",
        "");
      var hits = catalog.SearchAll("IMU", queryVector: null);
      Assert.Equal(2, hits.Count);
      Assert.Contains(hits, h => h.MailboxId == "imap");
      Assert.Contains(hits, h => h.MailboxId == "store");
    }
    finally {
      Directory.Delete(root, recursive: true);
    }
  }

  [Fact]
  public void CopyIndexed_KeepsFtsAndEmbeddingWithoutNewJob() {
    var root = Path.Combine(Path.GetTempPath(), "postclient-copy-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try {
      using var catalog = new MailArchiveCatalog();
      catalog.Open("imap", Path.Combine(root, "imap.db"));
      catalog.Open("store", Path.Combine(root, "store.db"));
      catalog.UpsertBody(
        "imap",
        new MailArchiveHeader {
          Uid = 1,
          Folder = "INBOX",
          Subject = "IMU avviso",
          From = "comune@pec.it",
          Date = DateTimeOffset.UtcNow,
          MessageId = "<one@x>"
        },
        Path.Combine(root, "1.eml"),
        "pagamento tributo",
        "");
      var item = Assert.Single(catalog.PendingEmbeddings(EmbeddingModelSpec.Id, 8));
      var vector = Towards(1);
      catalog.UpsertEmbedding("imap", item.MessageId, EmbeddingModelSpec.Id, vector);
      Assert.Empty(catalog.PendingEmbeddings(EmbeddingModelSpec.Id, 8));
      File.WriteAllText(Path.Combine(root, "dest.eml"), "copy");
      catalog.CopyIndexed("imap", "INBOX", 1, "store", "Inbox", Path.Combine(root, "dest.eml"), destUid: 9);
      Assert.Single(catalog.Search("store", "Inbox", "IMU"));
      Assert.Empty(catalog.PendingEmbeddings(EmbeddingModelSpec.Id, 8));
      Assert.Equal(2, catalog.EmbeddingCount(EmbeddingModelSpec.Id));
      catalog.RemoveUids("imap", "INBOX", [1u]);
      Assert.Empty(catalog.Search("imap", "INBOX", "IMU"));
      Assert.Single(catalog.Search("store", "Inbox", "IMU"));
    }
    finally {
      Directory.Delete(root, recursive: true);
    }
  }

  [Fact]
  public void MoveStore_CopiesMailDbAndKeepsIndexCounts() {
    var source = Path.Combine(Path.GetTempPath(), "postclient-move-" + Guid.NewGuid().ToString("N"));
    var dest = Path.Combine(Path.GetTempPath(), "postclient-moved-" + Guid.NewGuid().ToString("N"));
    try {
      LocalStoreSidecar.Write(source, "store1", "Mail");
      MailArchiveLayout.EnsureSystemFolders(source);
      using var catalog = new MailArchiveCatalog();
      catalog.Open("store1", Path.Combine(source, "mail.db"));
      catalog.UpsertBody(
        "store1",
        new MailArchiveHeader { Uid = 1, Folder = "Inbox", Subject = "Keep me", From = "a@b.c", Date = DateTimeOffset.UtcNow },
        Path.Combine(source, "Inbox", "1.eml"),
        "hello IMU",
        "");
      var item = Assert.Single(catalog.PendingEmbeddings(EmbeddingModelSpec.Id, 8));
      catalog.UpsertEmbedding("store1", item.MessageId, EmbeddingModelSpec.Id, Towards(1));
      var before = catalog.IndexStats(EmbeddingModelSpec.Id);
      catalog.CopyDirectory(source, dest);
      catalog.Close("store1");
      catalog.Open("store1", Path.Combine(dest, "mail.db"));
      var after = catalog.IndexStats(EmbeddingModelSpec.Id);
      Assert.Equal(before.KeywordRows, after.KeywordRows);
      Assert.Equal(before.MeaningRows, after.MeaningRows);
      Assert.Single(catalog.Search("store1", "Inbox", "IMU"));
    }
    finally {
      if (Directory.Exists(source))
        Directory.Delete(source, recursive: true);
      if (Directory.Exists(dest))
        Directory.Delete(dest, recursive: true);
    }
  }

  [Fact]
  public async Task ImportEml_SecondPassSkipsMessageId() {
    var dir = Path.Combine(Path.GetTempPath(), "postclient-import-" + Guid.NewGuid().ToString("N"));
    try {
      var box = new MailboxAccount {
        StorePath = dir,
        IncomingProtocol = MailProtocol.Store,
        Provider = MailProvider.Store
      };
      LocalStoreSidecar.Write(dir, box.Id, "Mail");
      MailArchiveLayout.EnsureSystemFolders(dir);
      using var catalog = new MailArchiveCatalog();
      catalog.Open(box.Id, Path.Combine(dir, "mail.db"));
      var session = new LocalStoreSession();
      await session.ConnectAsync(box, "", TestContext.Current.CancellationToken);
      var import = new LocalMailImport(catalog);
      var eml = Encoding.ASCII.GetBytes("""
        From: a@b.com
        To: c@d.com
        Subject: Hello IMU
        Message-ID: <dup@test>
        Date: Thu, 1 Jan 2026 00:00:00 +0000

        Hi
        """);
      Assert.True(await import.IngestAsync(box.Id, "Inbox", eml, session, unwrap: false, TestContext.Current.CancellationToken));
      Assert.False(await import.IngestAsync(box.Id, "Inbox", eml, session, unwrap: false, TestContext.Current.CancellationToken));
      Assert.Single(catalog.Search(box.Id, "Inbox", "IMU"));
    }
    finally {
      if (Directory.Exists(dir))
        Directory.Delete(dir, recursive: true);
    }
  }

  [Fact]
  public void Retention_ZeroKeeps_PositiveMovesToTrashThenPurgesTrash() {
    var root = Path.Combine(Path.GetTempPath(), "postclient-ret-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var settings = Path.Combine(root, "settings.json");
    try {
      using var catalog = new MailArchiveCatalog();
      catalog.Open("box", Path.Combine(root, "mail.db"));
      catalog.UpsertBody(
        "box",
        new MailArchiveHeader {
          Uid = 1,
          Folder = "INBOX",
          Subject = "Old",
          From = "a@b.c",
          Date = DateTimeOffset.UtcNow.AddDays(-10)
        },
        Path.Combine(root, "1.eml"),
        "old IMU",
        "");
      catalog.UpsertBody(
        "box",
        new MailArchiveHeader {
          Uid = 2,
          Folder = "INBOX",
          Subject = "New",
          From = "a@b.c",
          Date = DateTimeOffset.UtcNow
        },
        Path.Combine(root, "2.eml"),
        "new IMU",
        "");
      File.WriteAllText(Path.Combine(root, "1.eml"), "old");
      var files = new ConfigurationFileService(settings);
      files.Current.Mailboxes =
      [
        new MailboxAccount {
          Id = "box",
          IncomingProtocol = MailProtocol.Store,
          Provider = MailProvider.Store,
          StorePath = root
        }
      ];
      files.Current.Retention =
      [
        new FolderRetention { MailboxId = "box", Folder = "INBOX", Days = 0 }
      ];
      files.Save(files.Current);
      using var keepHost = new MailWorkerHost(catalog, files);
      keepHost.Retention(new WorkerRequest { Op = "retention" });
      Assert.Equal(2, catalog.Search("box", "INBOX", "IMU").Count);
      files.Current.Retention =
      [
        new FolderRetention { MailboxId = "box", Folder = "INBOX", Days = 5 }
      ];
      files.Save(files.Current);
      using var dropHost = new MailWorkerHost(catalog, files);
      dropHost.Retention(new WorkerRequest { Op = "retention" });
      var left = catalog.Search("box", "INBOX", "IMU");
      Assert.Single(left);
      Assert.Equal(2u, left[0].Uid);
      var trashed = catalog.Search("box", MailRetention.TrashFolder, "IMU");
      Assert.Single(trashed);
      Assert.Equal("Old", trashed[0].Subject);
      Assert.False(File.Exists(Path.Combine(root, "1.eml")));
      Assert.True(File.Exists(MailArchiveLayout.EmlPath(
        files.Current.Mailboxes[0],
        files.Current.Mailboxes,
        MailRetention.TrashFolder,
        trashed[0].Uid)));
      files.Current.Retention =
      [
        new FolderRetention { MailboxId = "box", Folder = MailRetention.TrashFolder, Days = 5 }
      ];
      files.Save(files.Current);
      using var purgeHost = new MailWorkerHost(catalog, files);
      purgeHost.Retention(new WorkerRequest { Op = "retention" });
      Assert.Single(catalog.Search("box", MailRetention.TrashFolder, "IMU"));
    }
    finally {
      Directory.Delete(root, recursive: true);
    }
  }

  [Fact]
  public void Worker_PingAndArchiveWorkSkipSynchronizationContext() {
    var root = Path.Combine(Path.GetTempPath(), "postclient-worker-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var previous = SynchronizationContext.Current;
    try {
      using var catalog = new MailArchiveCatalog();
      catalog.Open("box", Path.Combine(root, "mail.db"));
      catalog.UpsertBody(
        "box",
        new MailArchiveHeader { Uid = 1, Folder = "INBOX", Subject = "IMU", From = "a@b.c", Date = DateTimeOffset.UtcNow },
        "a.eml",
        "hello",
        "");
      var files = new ConfigurationFileService(Path.Combine(root, "settings.json"));
      using var host = new MailWorkerHost(catalog, files);
      var context = new FlagSynchronizationContext();
      SynchronizationContext.SetSynchronizationContext(context);
      var ping = host.Handle(new WorkerRequest { Op = "ping" });
      var hits = catalog.SearchAll("IMU", null);
      Assert.True(ping.Ok);
      Assert.Equal("pong", ping.Value);
      Assert.Single(hits);
      Assert.False(context.Posted);
    }
    finally {
      SynchronizationContext.SetSynchronizationContext(previous);
      Directory.Delete(root, recursive: true);
    }
  }

  [Fact]
  public void WorkerClient_CreateDoesNotNeedProcessForPing() {
    var root = Path.Combine(Path.GetTempPath(), "postclient-lazy-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try {
      using var catalog = new MailArchiveCatalog();
      catalog.Open("box", Path.Combine(root, "mail.db"));
      var files = new ConfigurationFileService(Path.Combine(root, "settings.json"));
      using var client = MailWorkerClient.Create(catalog, files, processPath: null);
      var ping = client.Call(new WorkerRequest { Op = "ping" });
      Assert.True(ping.Ok);
      Assert.Equal("pong", ping.Value);
    }
    finally {
      Directory.Delete(root, recursive: true);
    }
  }

  private static float[] Towards(float sign) {
    var values = new float[EmbeddingModelSpec.StoredDimensions];
    values[0] = sign;
    return EmbeddingVector.Normalize(values);
  }

  private static void CopyTree(string source, string dest) {
    Directory.CreateDirectory(dest);
    foreach (var file in Directory.EnumerateFiles(source))
      File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
    foreach (var child in Directory.EnumerateDirectories(source))
      CopyTree(child, Path.Combine(dest, Path.GetFileName(child)));
  }
}


file sealed class FlagSynchronizationContext : SynchronizationContext {
  public bool Posted { get; private set; }

  public override void Post(SendOrPostCallback d, object? state) {
    Posted = true;
    base.Post(d, state);
  }

  public override void Send(SendOrPostCallback d, object? state) {
    Posted = true;
    base.Send(d, state);
  }
}
