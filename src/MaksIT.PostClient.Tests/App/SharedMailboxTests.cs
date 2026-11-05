namespace MaksIT.PostClient.Tests.App;


[Collection(ProcessEnvironment.Name)]
public class SharedMailboxTests {
  [Fact]
  public void SharedMailLeavesTheUserProfile() {
    var previous = Environment.GetEnvironmentVariable(SharedMailPaths.Env);
    var root = Path.Combine(Path.GetTempPath(), "postclient-shared-" + Guid.NewGuid().ToString("N"));
    Environment.SetEnvironmentVariable(SharedMailPaths.Env, root);
    try {
      var box = new MailboxAccount { Id = "box1", Address = "shared@example.com" };
      Assert.StartsWith(root, SharedMailPaths.AccountDirectory(box.Id), StringComparison.OrdinalIgnoreCase);
      var privateRoot = Path.Combine(AppPaths.AccountsDirectory(), box.Id);
      Directory.CreateDirectory(privateRoot);
      File.WriteAllText(Path.Combine(privateRoot, "mail.db"), "x");
      var placed = SharedMailboxStore.Place(box, shared: true, privateRoot);
      Assert.True(placed.IsSuccess);
      Assert.True(File.Exists(SharedMailPaths.AccountFile(box.Id)));
      Assert.True(File.Exists(Path.Combine(SharedMailPaths.AccountDirectory(box.Id), "mail.db")));
      Assert.False(Directory.Exists(privateRoot));
      var configuration = new Configuration();
      Assert.True(SharedMailboxStore.MergeMissing(configuration));
      Assert.Equal("box1", configuration.Mailboxes[0].Id);
      Assert.True(configuration.Mailboxes[0].Shared);
      Assert.Equal(
        SharedMailPaths.AccountDirectory(box.Id),
        MailArchiveLayout.MailRoot(configuration.Mailboxes[0], configuration.Mailboxes));
    }
    finally {
      Environment.SetEnvironmentVariable(SharedMailPaths.Env, previous);
      if (Directory.Exists(root))
        Directory.Delete(root, recursive: true);
    }
  }

  [Fact]
  public void PrivateMailStaysInTheAccountFolder() {
    var box = new MailboxAccount { Id = "box2" };
    var mailboxes = new List<MailboxAccount> { box };
    Assert.Equal(
      Path.Combine(AppPaths.AccountsDirectory(), "box2"),
      MailArchiveLayout.MailRoot(box, mailboxes));
  }

  [Fact]
  public void PrivatePlaceDoesNotPublish_OnlySharedOnesAreImported() {
    var previous = Environment.GetEnvironmentVariable(SharedMailPaths.Env);
    var root = Path.Combine(Path.GetTempPath(), "postclient-central-" + Guid.NewGuid().ToString("N"));
    Environment.SetEnvironmentVariable(SharedMailPaths.Env, root);
    try {
      var privateBox = new MailboxAccount { Id = "mine", Address = "mine@example.com" };
      var sharedBox = new MailboxAccount { Id = "ours", Address = "ours@example.com" };
      var privateRoot = Path.Combine(AppPaths.AccountsDirectory(), privateBox.Id);
      Directory.CreateDirectory(privateRoot);
      Assert.True(SharedMailboxStore.Place(privateBox, shared: false, privateRoot).IsSuccess);
      Assert.False(File.Exists(SharedMailPaths.AccountFile(privateBox.Id)));
      Assert.True(SharedMailboxStore.Place(
        sharedBox,
        shared: true,
        Path.Combine(AppPaths.AccountsDirectory(), sharedBox.Id)).IsSuccess);
      var listed = SharedMailboxStore.List();
      Assert.Single(listed);
      Assert.Equal("ours", listed[0].Id);
      var configuration = new Configuration();
      Assert.True(SharedMailboxStore.MergeMissing(configuration));
      Assert.Single(configuration.Mailboxes);
      Assert.Equal("ours", configuration.Mailboxes[0].Id);
    }
    finally {
      Environment.SetEnvironmentVariable(SharedMailPaths.Env, previous);
      if (Directory.Exists(root))
        Directory.Delete(root, recursive: true);
    }
  }

  [Fact]
  public void TogglingSharedMovesTheArchive() {
    var previous = Environment.GetEnvironmentVariable(SharedMailPaths.Env);
    var root = Path.Combine(Path.GetTempPath(), "postclient-toggle-" + Guid.NewGuid().ToString("N"));
    Environment.SetEnvironmentVariable(SharedMailPaths.Env, root);
    var box = new MailboxAccount { Id = "toggle", Address = "toggle@example.com" };
    var privateRoot = Path.Combine(AppPaths.AccountsDirectory(), box.Id);
    try {
      if (Directory.Exists(privateRoot))
        Directory.Delete(privateRoot, recursive: true);
      Directory.CreateDirectory(privateRoot);
      File.WriteAllText(Path.Combine(privateRoot, "mail.db"), "live");
      Assert.True(SharedMailboxStore.Place(box, shared: true, privateRoot).IsSuccess);
      var sharedDb = Path.Combine(SharedMailPaths.AccountDirectory(box.Id), "mail.db");
      Assert.Equal("live", File.ReadAllText(sharedDb));
      Assert.False(Directory.Exists(privateRoot));

      Assert.True(SharedMailboxStore.Place(box, shared: false, privateRoot).IsSuccess);
      Assert.False(File.Exists(SharedMailPaths.AccountFile(box.Id)));
      Assert.Equal("live", File.ReadAllText(Path.Combine(privateRoot, "mail.db")));
      Assert.Equal(privateRoot, MailArchiveLayout.MailRoot(box, [box]));
    }
    finally {
      Environment.SetEnvironmentVariable(SharedMailPaths.Env, previous);
      if (Directory.Exists(root))
        Directory.Delete(root, recursive: true);
      if (Directory.Exists(privateRoot))
        Directory.Delete(privateRoot, recursive: true);
    }
  }
}
