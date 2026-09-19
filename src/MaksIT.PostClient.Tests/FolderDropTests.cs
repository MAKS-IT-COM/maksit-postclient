using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Tests;


public class MailFolderPathTests {
  [Fact]
  public void Leaf_SplitsImapHierarchyNotLabelDots() {
    Assert.Equal("Clients", MailFolderPath.Leaf("INBOX.Clients"));
    Assert.Equal("Clients", MailFolderPath.Leaf("INBOX/Clients"));
    Assert.Equal("Inbox", MailFolderPath.Leaf("Inbox"));
    Assert.Equal("P.IVA", MailFolderPath.Leaf("P.IVA"));
    Assert.Equal("P.IVA", MailFolderPath.Leaf("[Gmail]/P.IVA"));
    Assert.Equal("2024", MailFolderPath.Leaf("P.IVA/2024"));
  }

  [Fact]
  public void IsUnder_RejectsSelfCousinsAndGmailDots() {
    Assert.True(MailFolderPath.IsUnder("INBOX.Clients.2024", "INBOX.Clients"));
    Assert.True(MailFolderPath.IsUnder("INBOX/Clients/2024", "INBOX/Clients"));
    Assert.False(MailFolderPath.IsUnder("INBOX.Clients", "INBOX.Clients"));
    Assert.False(MailFolderPath.IsUnder("INBOX2", "INBOX"));
    Assert.True(MailFolderPath.IsSelfOrUnder("Project", "Project"));
    Assert.False(MailFolderPath.IsUnder("P.IVA", "P"));
    Assert.True(MailFolderPath.IsUnder("P.IVA/2024", "P.IVA"));
  }

  [Fact]
  public void SameParent_TreatsRootAsEmpty() {
    Assert.True(MailFolderPath.SameParent("Project", null));
    Assert.True(MailFolderPath.SameParent("INBOX.Clients", "INBOX"));
    Assert.False(MailFolderPath.SameParent("INBOX.Clients", null));
    Assert.True(MailFolderPath.SameParent("P.IVA", null));
    Assert.False(MailFolderPath.SameParent("P.IVA", "P"));
  }

  [Fact]
  public void SameParent_KeepsGmailNamespace() {
    Assert.False(MailFolderPath.SameParent("[Gmail]/Allianz", null));
    Assert.True(MailFolderPath.SameParent("[Gmail]/Allianz", "[Gmail]"));
    Assert.False(MailFolderPath.SameParent("[Gmail]/Work/Projects", null));
    Assert.True(MailFolderPath.SameParent("[Gmail]/Work/Projects", "[Gmail]/Work"));
  }

  [Fact]
  public void UserParent_KeepsGmailNamespace() {
    Assert.Null(MailFolderPath.UserParent("Allianz"));
    Assert.Null(MailFolderPath.UserParent("P.IVA"));
    Assert.Equal("[Gmail]", MailFolderPath.UserParent("[Gmail]/Allianz"));
    Assert.Equal("[Gmail]", MailFolderPath.UserParent("[Gmail]/P.IVA"));
    Assert.Equal("[Gmail]/Work", MailFolderPath.UserParent("[Gmail]/Work/Projects"));
    Assert.Equal("INBOX", MailFolderPath.UserParent("INBOX.Clients"));
    Assert.Equal("P.IVA", MailFolderPath.UserParent("P.IVA/2024"));
  }

  [Fact]
  public void TreeParent_NestsVisibleAncestors() {
    string[] known = ["INBOX", "[Gmail]", "[Gmail]/Sent Mail", "[Gmail]/Work", "[Gmail]/Work/Projects", "Allianz"];
    Assert.Equal("[Gmail]", MailFolderPath.TreeParent("[Gmail]/Allianz", known));
    Assert.Equal("[Gmail]", MailFolderPath.TreeParent("[Gmail]/Sent Mail", known));
    Assert.Equal("[Gmail]/Work", MailFolderPath.TreeParent("[Gmail]/Work/Projects", known));
    Assert.Null(MailFolderPath.TreeParent("Allianz", known));
    Assert.Null(MailFolderPath.TreeParent("P.IVA", known));
    Assert.Equal("INBOX", MailFolderPath.TreeParent("INBOX.Clients", known));
    Assert.Equal("P.IVA", MailFolderPath.TreeParent("P.IVA/2024", ["INBOX", "P.IVA", "P.IVA/2024"]));
  }

  [Fact]
  public void UniqueLeaf_AddsNumberOnClash() {
    Assert.Equal("Project", MailFolderPath.UniqueLeaf("Project", ["Inbox", "Sent"]));
    Assert.Equal("Project (2)", MailFolderPath.UniqueLeaf("Project", ["Project", "Inbox"]));
    Assert.Equal("P.IVA", MailFolderPath.UniqueLeaf("P.IVA", ["Inbox", "Sent"]));
  }

  [Fact]
  public void Combine_KeepsParentSeparator() {
    Assert.Equal("INBOX/Clients", MailFolderPath.Combine("INBOX", "Clients"));
    Assert.Equal("INBOX.Work.Clients", MailFolderPath.Combine("INBOX.Work", "Clients"));
    Assert.Equal("Archive/2024", MailFolderPath.Combine("Archive", "2024"));
    Assert.Equal("P.IVA/2024", MailFolderPath.Combine("P.IVA", "2024"));
    Assert.Equal("[Gmail]/P.IVA", MailFolderPath.Combine("[Gmail]", "P.IVA"));
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
    Assert.True(MailDrop.CanDropFolder("imap", "a", null, "a", "[Gmail]/Allianz", true));
    Assert.True(MailDrop.CanDropFolder("imap", "a", "[Gmail]/Work", "a", "[Gmail]/Allianz", true));
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

  [Fact]
  public void RoundTrip_SeveralFolders() {
    Assert.True(MailDragPayload.TryUnpackFolders(
      MailDragPayload.PackFolders("box", ["Project", "Clients", "Project"]),
      out var mailbox,
      out var folders));
    Assert.Equal("box", mailbox);
    Assert.Equal(["Project", "Clients"], folders);
  }
}


public class MailSelectionTests {
  [Fact]
  public void Roots_DropsNestedAndDuplicates() {
    var roots = MailSelection.Roots([
      ("a", "Project"),
      ("a", "Project/Sub"),
      ("a", "Clients"),
      ("a", "Project"),
      ("b", "Project/Sub")
    ]);
    Assert.Equal(
      [("a", "Project"), ("a", "Clients"), ("b", "Project/Sub")],
      roots);
  }
}
