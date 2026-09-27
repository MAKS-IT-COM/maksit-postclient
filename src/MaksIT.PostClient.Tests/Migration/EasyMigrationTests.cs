using System.Text.Json;


namespace MaksIT.PostClient.Tests.Migration;


public class EasyMigrationTests {
  [Fact]
  public void RoundTrip_RewritesPathsAndSecrets() {
    var root = Temp();
    var source = Layout(Path.Combine(root, "source"));
    var dest = Layout(Path.Combine(root, "dest"));
    var outside = Path.Combine(root, "outside");
    Directory.CreateDirectory(outside);
    File.WriteAllText(Path.Combine(outside, "kept.txt"), "external");
    var store = Path.Combine(source.DataDirectory, "stores", "Mail");
    Directory.CreateDirectory(store);
    File.WriteAllText(Path.Combine(store, "mail.db"), "store-db");
    var account = Path.Combine(source.DataDirectory, "accounts", "box");
    Directory.CreateDirectory(account);
    File.WriteAllText(Path.Combine(account, "note.txt"), "hello");
    File.WriteAllText(Path.Combine(source.DataDirectory, "mail.db"), "catalog");
    Directory.CreateDirectory(Path.Combine(source.DataDirectory, "logs"));
    File.WriteAllText(Path.Combine(source.DataDirectory, "logs", "skip.txt"), "log");
    Directory.CreateDirectory(Path.Combine(source.SharedDirectory, "models"));
    File.WriteAllText(Path.Combine(source.SharedDirectory, "models", "a.bin"), "model");
    var inside = new MailboxAccount {
      Id = "inside",
      IncomingProtocol = MailProtocol.Store,
      StorePath = store
    };
    var external = new MailboxAccount {
      Id = "external",
      IncomingProtocol = MailProtocol.Store,
      ImapHost = outside
    };
    WriteSettings(source.ConfigDirectory, inside, external);
    FileSecretStore.OpenUser(Path.Combine(source.ConfigDirectory, "secrets.bin")).Put("mailbox:inside", "mailbox-password-9f3a");
    FileSecretStore.OpenMachine(Path.Combine(source.SharedDirectory, "service-secrets.bin"))
      .Put("mailbox:inside", "service-password-9f3a");
    var bundle = Path.Combine(root, "move.postbundle");
    try {
      var created = EasyMigration.Create(source, bundle, "correct horse");
      Assert.True(created.IsSuccess, string.Join(" ", created.Messages));
      Assert.True(File.ReadAllBytes(bundle).AsSpan().IndexOf("mailbox-password-9f3a"u8) < 0);
      var restored = EasyMigration.Restore(bundle, "correct horse", dest);
      Assert.True(restored.IsSuccess, string.Join(" ", restored.Messages));
      var settings = ReadSettings(dest.ConfigDirectory);
      var movedStore = settings.FindMailbox("inside");
      Assert.NotNull(movedStore);
      Assert.StartsWith(Path.GetFullPath(dest.DataDirectory), movedStore.StorePath, StringComparison.OrdinalIgnoreCase);
      Assert.Equal("store-db", File.ReadAllText(Path.Combine(movedStore.StorePath, "mail.db")));
      var movedExternal = settings.FindMailbox("external");
      Assert.NotNull(movedExternal);
      Assert.StartsWith(
        Path.Combine(Path.GetFullPath(dest.DataDirectory), "stores"),
        movedExternal.ImapHost,
        StringComparison.OrdinalIgnoreCase);
      Assert.Equal("external", File.ReadAllText(Path.Combine(movedExternal.ImapHost, "kept.txt")));
      Assert.Equal("hello", File.ReadAllText(Path.Combine(dest.DataDirectory, "accounts", "box", "note.txt")));
      Assert.Equal("catalog", File.ReadAllText(Path.Combine(dest.DataDirectory, "mail.db")));
      Assert.Equal("model", File.ReadAllText(Path.Combine(dest.SharedDirectory, "models", "a.bin")));
      Assert.False(File.Exists(Path.Combine(dest.DataDirectory, "logs", "skip.txt")));
      Assert.Equal(
        "mailbox-password-9f3a",
        FileSecretStore.OpenUser(Path.Combine(dest.ConfigDirectory, "secrets.bin")).Get("mailbox:inside").Value);
      Assert.Equal(
        "service-password-9f3a",
        FileSecretStore.OpenMachine(Path.Combine(dest.SharedDirectory, "service-secrets.bin")).Get("mailbox:inside").Value);
    }
    finally {
      Directory.Delete(root, recursive: true);
    }
  }

  [Fact]
  public void Restore_WrongPassphrase_LeavesDestination() {
    var root = Temp();
    var source = Layout(Path.Combine(root, "source"));
    var dest = Layout(Path.Combine(root, "dest"));
    File.WriteAllText(Path.Combine(source.DataDirectory, "mail.db"), "catalog");
    WriteSettings(source.ConfigDirectory, new MailboxAccount { Id = "box", Address = "a@b.c" });
    var marker = Path.Combine(dest.ConfigDirectory, "settings.json");
    File.WriteAllText(marker, "{\"keep\":true}");
    var bundle = Path.Combine(root, "move.postbundle");
    try {
      var created = EasyMigration.Create(source, bundle, "correct horse");
      Assert.True(created.IsSuccess, string.Join(" ", created.Messages));
      var restored = EasyMigration.Restore(bundle, "nope", dest);
      Assert.False(restored.IsSuccess);
      Assert.Contains(EasyMigration.WrongPassphrase, string.Join(" ", restored.Messages), StringComparison.Ordinal);
      Assert.Equal("{\"keep\":true}", File.ReadAllText(marker));
      Assert.False(File.Exists(Path.Combine(dest.DataDirectory, "mail.db")));
    }
    finally {
      Directory.Delete(root, recursive: true);
    }
  }

  [Fact]
  public void SameRoot_RoundTrip_KeepsSettingsAndMail() {
    var root = Temp();
    var sourceRoot = Path.Combine(root, "source");
    var destRoot = Path.Combine(root, "dest");
    var source = new MigrationLayout {
      ConfigDirectory = sourceRoot,
      DataDirectory = sourceRoot,
      SharedDirectory = Path.Combine(root, "source-shared")
    };
    var dest = new MigrationLayout {
      ConfigDirectory = destRoot,
      DataDirectory = destRoot,
      SharedDirectory = Path.Combine(root, "dest-shared")
    };
    Directory.CreateDirectory(source.ConfigDirectory);
    Directory.CreateDirectory(source.SharedDirectory);
    Directory.CreateDirectory(dest.ConfigDirectory);
    Directory.CreateDirectory(dest.SharedDirectory);
    File.WriteAllText(Path.Combine(sourceRoot, "mail.db"), "catalog");
    WriteSettings(sourceRoot, new MailboxAccount { Id = "box", Address = "a@b.c" });
    FileSecretStore.OpenUser(Path.Combine(sourceRoot, "secrets.bin")).Put("mailbox:box", "same-root-secret");
    var bundle = Path.Combine(root, "same.postbundle");
    try {
      var created = EasyMigration.Create(source, bundle, "phrase");
      Assert.True(created.IsSuccess, string.Join(" ", created.Messages));
      var restored = EasyMigration.Restore(bundle, "phrase", dest);
      Assert.True(restored.IsSuccess, string.Join(" ", restored.Messages));
      Assert.Equal("catalog", File.ReadAllText(Path.Combine(destRoot, "mail.db")));
      Assert.Equal("a@b.c", ReadSettings(destRoot).FindMailbox("box")?.Address);
      Assert.Equal(
        "same-root-secret",
        FileSecretStore.OpenUser(Path.Combine(destRoot, "secrets.bin")).Get("mailbox:box").Value);
    }
    finally {
      Directory.Delete(root, recursive: true);
    }
  }

  private static string Temp() {
    var path = Path.Combine(Path.GetTempPath(), "postclient-bundle-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
  }

  private static MigrationLayout Layout(string root) {
    var layout = new MigrationLayout {
      ConfigDirectory = Path.Combine(root, "config"),
      DataDirectory = Path.Combine(root, "data"),
      SharedDirectory = Path.Combine(root, "shared")
    };
    Directory.CreateDirectory(layout.ConfigDirectory);
    Directory.CreateDirectory(layout.DataDirectory);
    Directory.CreateDirectory(layout.SharedDirectory);
    return layout;
  }

  private static void WriteSettings(string configDirectory, params MailboxAccount[] mailboxes) {
    var files = new ConfigurationFileService(Path.Combine(configDirectory, "settings.json"));
    files.Current.Mailboxes = [.. mailboxes];
    files.Save(files.Current);
  }

  private static Configuration ReadSettings(string configDirectory) {
    var json = File.ReadAllText(Path.Combine(configDirectory, "settings.json"));
    using var document = JsonDocument.Parse(json);
    var configuration = document.RootElement.GetProperty("Configuration");
    return JsonSerializer.Deserialize<Configuration>(
      configuration.GetRawText(),
      new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Configuration();
  }
}
