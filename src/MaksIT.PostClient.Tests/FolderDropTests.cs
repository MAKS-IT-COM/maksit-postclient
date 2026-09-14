using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Tests;


public class MailFolderPathTests {
  [Fact]
  public void Leaf_SplitsDotAndSlash() {
    Assert.Equal("Clients", MailFolderPath.Leaf("INBOX.Clients"));
    Assert.Equal("Clients", MailFolderPath.Leaf("INBOX/Clients"));
    Assert.Equal("Inbox", MailFolderPath.Leaf("Inbox"));
  }

  [Fact]
  public void IsUnder_RejectsSelfAndCousins() {
    Assert.True(MailFolderPath.IsUnder("INBOX.Clients.2024", "INBOX.Clients"));
    Assert.True(MailFolderPath.IsUnder("INBOX/Clients/2024", "INBOX/Clients"));
    Assert.False(MailFolderPath.IsUnder("INBOX.Clients", "INBOX.Clients"));
    Assert.False(MailFolderPath.IsUnder("INBOX2", "INBOX"));
    Assert.True(MailFolderPath.IsSelfOrUnder("Project", "Project"));
  }

  [Fact]
  public void SameParent_TreatsRootAsEmpty() {
    Assert.True(MailFolderPath.SameParent("Project", null));
    Assert.True(MailFolderPath.SameParent("INBOX.Clients", "INBOX"));
    Assert.False(MailFolderPath.SameParent("INBOX.Clients", null));
  }

  [Fact]
  public void UniqueLeaf_AddsNumberOnClash() {
    Assert.Equal("Project", MailFolderPath.UniqueLeaf("Project", ["Inbox", "Sent"]));
    Assert.Equal("Project (2)", MailFolderPath.UniqueLeaf("Project", ["Project", "Inbox"]));
  }

  [Fact]
  public void Combine_KeepsParentSeparator() {
    Assert.Equal("INBOX/Clients", MailFolderPath.Combine("INBOX", "Clients"));
    Assert.Equal("INBOX.Work.Clients", MailFolderPath.Combine("INBOX.Work", "Clients"));
    Assert.Equal("Archive/2024", MailFolderPath.Combine("Archive", "2024"));
  }
}


public class MailDropTests {
  [Fact]
  public void Pop3_RejectsDrops() {
    Assert.False(MailDrop.CanDropMessages("pop3", "b", "INBOX", "a", "INBOX"));
    Assert.False(MailDrop.CanDropFolder("pop3", "b", null, "a", "Project", true));
  }

  [Fact]
  public void Messages_AllowOtherMailboxAndAccountNode() {
    Assert.True(MailDrop.CanDropMessages("imap", "b", "Archive", "a", "INBOX"));
    Assert.True(MailDrop.CanDropMessages("pst", "b", null, "a", "INBOX"));
    Assert.False(MailDrop.CanDropMessages("imap", "a", "INBOX", "a", "INBOX"));
  }

  [Fact]
  public void Folder_RejectsSystemSelfAndCurrentParent() {
    Assert.False(MailDrop.CanDropFolder("imap", "a", "Archive", "a", "INBOX", false));
    Assert.False(MailDrop.CanDropFolder("imap", "a", "Project", "a", "Project", true));
    Assert.False(MailDrop.CanDropFolder("imap", "a", "Project/Sub", "a", "Project", true));
    Assert.False(MailDrop.CanDropFolder("imap", "a", null, "a", "Project", true));
    Assert.True(MailDrop.CanDropFolder("imap", "a", "Inbox", "a", "Project", true));
    Assert.True(MailDrop.CanDropFolder("pst", "pst-1", null, "imap-1", "Project", true));
  }
}


public class MailDragPayloadTests {
  [Fact]
  public void RoundTrip_MessagesAndFolder() {
    Assert.True(MailDragPayload.TryUnpackMessages(
      MailDragPayload.PackMessages("box", "INBOX", [1u, 2u]),
      out var mailbox,
      out var folder,
      out var ids));
    Assert.Equal("box", mailbox);
    Assert.Equal("INBOX", folder);
    Assert.Equal([1u, 2u], ids);

    Assert.True(MailDragPayload.TryUnpackFolder(
      MailDragPayload.PackFolder("box", "Project"),
      out mailbox,
      out folder));
    Assert.Equal("box", mailbox);
    Assert.Equal("Project", folder);
    Assert.False(MailDragPayload.TryUnpackMessages("folder\nbox\nProject", out _, out _, out _));
  }
}
