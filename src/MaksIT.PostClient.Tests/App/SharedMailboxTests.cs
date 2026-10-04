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
      var placed = SharedMailboxStore.Place(box, shared: true, privateRoot: Path.Combine(root, "unused"));
      Assert.True(placed.IsSuccess);
      Assert.True(File.Exists(SharedMailPaths.AccountFile(box.Id)));
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
  public void EveryEnrolledAccountIsListed_OnlySharedOnesAreImported() {
    var previous = Environment.GetEnvironmentVariable(SharedMailPaths.Env);
    var root = Path.Combine(Path.GetTempPath(), "postclient-central-" + Guid.NewGuid().ToString("N"));
    Environment.SetEnvironmentVariable(SharedMailPaths.Env, root);
    try {
      var privateBox = new MailboxAccount { Id = "mine", Address = "mine@example.com" };
      var sharedBox = new MailboxAccount { Id = "ours", Address = "ours@example.com" };
      Assert.True(SharedMailboxStore.Place(privateBox, shared: false, Path.Combine(root, "a")).IsSuccess);
      Assert.True(SharedMailboxStore.Place(sharedBox, shared: true, Path.Combine(root, "b")).IsSuccess);
      var listed = SharedMailboxStore.List();
      Assert.Equal(2, listed.Count);
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
}
