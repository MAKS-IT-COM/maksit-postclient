using System.Text;
using MaksIT.PostClient.Client;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Tests;


public class MailRuleEngineTests {
  [Fact]
  public void Matches_FromAndSubject() {
    var rule = new MailRule {
      Action = MailRuleAction.Move,
      Folder = "Archive",
      Logic = "and",
      Conditions = [
        new() { Field = "from", Op = "contains", Value = "comune.it" },
        new() { Field = "subject", Op = "contains", Value = "IMU" }
      ]
    };
    Assert.True(MailRuleEngine.Matches(rule, "sportello@comune.it", "me@studio.it", "Avviso IMU 2026", "", false));
    Assert.False(MailRuleEngine.Matches(rule, "sportello@comune.it", "me@studio.it", "TARI", "", false));
  }

  [Fact]
  public void ResolveFolder_MapsOutlookDeletedItems() {
    var folders = new List<(string Name, string FullName)> {
      ("INBOX", "INBOX"),
      ("Cestino", "Cestino")
    };
    Assert.Equal("Cestino", MailRuleEngine.ResolveFolder("Deleted Items", folders));
  }

  [Fact]
  public void Merge_ReplacesSameName() {
    var into = new List<MailRule> {
      new() {
        Name = "IMU",
        Action = MailRuleAction.Flag,
        Conditions = [new() { Field = "subject", Value = "old" }]
      }
    };
    var added = MailRuleEngine.Merge(into, [
      new MailRule {
        Name = "IMU",
        Action = MailRuleAction.Move,
        Folder = "Pratiche",
        Conditions = [new() { Field = "subject", Value = "IMU" }]
      }
    ]);
    Assert.Equal(1, added);
    Assert.Single(into);
    Assert.Equal(MailRuleAction.Move, into[0].Action);
    Assert.Equal("Pratiche", into[0].Folder);
  }

  [Fact]
  public void ExactFolder_RequiresNameOrFullName() {
    var folders = new List<(string Name, string FullName)> {
      ("Inbox", "INBOX"),
      ("Temu", "Temu"),
      ("Cestino", "Cestino")
    };
    Assert.Equal("Temu", MailRuleEngine.ExactFolder("Temu", folders));
    Assert.Equal("INBOX", MailRuleEngine.ExactFolder("Inbox", folders));
    Assert.Equal("Projects/Temu", MailRuleEngine.ExactFolder("Temu", [("Temu", "Projects/Temu")]));
    Assert.Equal("Temu", MailRuleEngine.ExactFolder("Inbox/Temu", [("Temu", "Temu")]));
    Assert.Equal("Temu", MailRuleEngine.ExactFolder(@"Personal Folders\Temu", [("Temu", "Temu")]));
    Assert.Null(MailRuleEngine.ExactFolder("Deleted Items", folders));
    Assert.Null(MailRuleEngine.ExactFolder("emu", folders));
  }

  [Fact]
  public void CanApply_NeedsMailboxAndExactFolder() {
    var folders = new List<(string Name, string FullName)> { ("Temu", "Temu") };
    var rule = new MailRule {
      Action = MailRuleAction.Move,
      Folder = "Temu",
      MailboxId = "box-1",
      Conditions = [new() { Field = "from", Value = "temuemail.com" }]
    };
    Assert.True(MailRuleEngine.CanApply(rule, "box-1", folders));
    Assert.False(MailRuleEngine.CanApply(rule, "box-2", folders));
    rule.MailboxId = "";
    Assert.False(MailRuleEngine.CanApply(rule, "box-1", folders));
    rule.MailboxId = "box-1";
    rule.Folder = "Amazon";
    Assert.False(MailRuleEngine.CanApply(rule, "box-1", folders));
  }

  [Fact]
  public void FolderMailbox_FallsBackToRuleMailbox() {
    var rule = new MailRule { MailboxId = "imap-1", Folder = "Archive" };
    Assert.Equal("imap-1", MailRuleEngine.FolderMailbox(rule));
    rule.FolderMailboxId = "pst-1";
    Assert.Equal("pst-1", MailRuleEngine.FolderMailbox(rule));
  }

  [Fact]
  public void CanApply_MoveFolderOnAnotherMailbox() {
    var dest = new List<(string Name, string FullName)> { ("Archive", "Archive") };
    var rule = new MailRule {
      Action = MailRuleAction.Move,
      Folder = "Archive",
      MailboxId = "imap-1",
      FolderMailboxId = "pst-1",
      Conditions = [new() { Field = "from", Value = "comune.it" }]
    };
    Assert.True(MailRuleEngine.CanApply(rule, "imap-1", dest));
    Assert.False(MailRuleEngine.CanApply(rule, "pst-1", dest));
  }

  [Fact]
  public void RetargetFolders_RewritesPathAndMailbox() {
    var rules = new List<MailRule> {
      new() {
        Action = MailRuleAction.Move,
        Folder = "Project",
        MailboxId = "imap-1",
        FolderMailboxId = "imap-1",
        Conditions = [new() { Field = "from", Value = "a" }]
      }
    };
    Assert.Equal(1, MailRuleEngine.RetargetFolders(rules, "imap-1", "Project", "Archive/Project", "pst-1"));
    Assert.Equal("Archive/Project", rules[0].Folder);
    Assert.Equal("pst-1", rules[0].FolderMailboxId);
  }

  [Fact]
  public void Merge_KeepsMailboxId() {
    var into = new List<MailRule> {
      new() {
        Name = "Temu",
        Action = MailRuleAction.Move,
        Folder = "Temu",
        Conditions = [new() { Field = "from", Value = "temu" }]
      }
    };
    MailRuleEngine.Merge(into, [
      new MailRule {
        Name = "Temu",
        Action = MailRuleAction.Move,
        Folder = "Temu",
        MailboxId = "box-9",
        Conditions = [new() { Field = "from", Value = "temuemail.com" }]
      }
    ]);
    Assert.Equal("box-9", into[0].MailboxId);
  }

  [Fact]
  public void Merge_KeepsFolderMailboxId() {
    var into = new List<MailRule> {
      new() {
        Name = "Temu",
        Action = MailRuleAction.Move,
        Folder = "Temu",
        MailboxId = "imap-1",
        Conditions = [new() { Field = "from", Value = "temu" }]
      }
    };
    MailRuleEngine.Merge(into, [
      new MailRule {
        Name = "Temu",
        Action = MailRuleAction.Move,
        Folder = "Temu",
        MailboxId = "imap-1",
        FolderMailboxId = "pst-1",
        Conditions = [new() { Field = "from", Value = "temuemail.com" }]
      }
    ]);
    Assert.Equal("pst-1", into[0].FolderMailboxId);
  }
}


public class MailRetentionTests {
  [Fact]
  public void Jobs_SkipsZeroDays() {
    var jobs = MailRetention.Jobs(
      [
        new FolderRetention { MailboxId = "box", Folder = "INBOX", Days = 0 },
        new FolderRetention { MailboxId = "box", Folder = "Pratiche", Days = 30 }
      ]);
    var job = Assert.Single(jobs);
    Assert.Equal("box", job.MailboxId);
    Assert.Equal("Pratiche", job.Folder);
    Assert.Equal(30, job.Days);
  }

  [Fact]
  public void ResolveTrash_PrefersExistingTrashFolder() {
    Assert.Equal("Cestino", MailRetention.ResolveTrash(["INBOX", "Cestino"]));
    Assert.Equal(MailRetention.TrashFolder, MailRetention.ResolveTrash(["INBOX"]));
    Assert.True(MailRetention.IsTrash("Deleted Items"));
    Assert.True(MailRetention.IsTrash("Trash", "[Gmail]/Trash"));
  }

  [Fact]
  public void DaysFor_MatchesGmailTrashPath() {
    var rows = new[] {
      new FolderRetention { MailboxId = "gmail", Folder = "[Gmail]/Trash", Days = 7 }
    };
    Assert.Equal(7, MailRetention.DaysFor("gmail", "[Gmail]/Trash", rows));
    Assert.Equal(7, MailRetention.DaysFor("gmail", "Trash", rows));
    Assert.Equal(0, MailRetention.DaysFor("gmail", "INBOX", rows));
    Assert.Equal(0, MailRetention.DaysFor("other", "[Gmail]/Trash", rows));
  }

  [Fact]
  public void BindFolder_ResolvesGmailTrash() {
    Assert.Equal(
      "[Gmail]/Trash",
      MailRetention.BindFolder("[Gmail]/Trash", ["INBOX", "[Gmail]/Trash", "[Gmail]/Spam"]));
    Assert.Equal(
      "[Gmail]/Trash",
      MailRetention.BindFolder("Trash", ["INBOX", "[Gmail]/Trash"]));
  }
}


public class MapiRestrictionTests {
  [Fact]
  public void Parse_SubjectContainsAndMarkRead() {
    var subject = Encoding.Unicode.GetBytes("fattura\0");
    var condition = new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00, 0x37, 0x00, 0x1F, 0x00 }
      .Concat(subject)
      .ToArray();
    var actions = new byte[] { 0x01, 0x00, 0x0B, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
    var rule = MapiRestriction.Parse("Fatture", condition, actions);
    Assert.NotNull(rule);
    Assert.Equal("Fatture", rule!.Name);
    Assert.Equal(MailRuleAction.MarkRead, rule.Action);
    Assert.Equal("subject", rule.Conditions[0].Field);
    Assert.Equal("fattura", rule.Conditions[0].Value);
    Assert.True(MailRuleEngine.Matches(rule, "a@b.it", "", "Fattura PA", "", false));
  }
}


public class OutlookRulesTests {
  [Fact]
  public void FromBytes_ReadsJsonRules() {
    var json = """
      [
        {
          "name": "Comune",
          "action": "move",
          "folder": "Archive",
          "mailboxId": "box-1",
          "conditions": [{ "field": "from", "op": "contains", "value": "comune.it" }]
        }
      ]
      """u8.ToArray();
    var rules = OutlookRules.FromBytes(json, "rules.json");
    Assert.Single(rules);
    Assert.Equal("Comune", rules[0].Name);
    Assert.Equal(MailRuleAction.Move, rules[0].Action);
    Assert.Equal("Archive", rules[0].Folder);
    Assert.Equal("box-1", rules[0].MailboxId);
  }

  [Fact]
  public void FromBytes_ReadsModernRwz() {
    var bytes = ModernRwz(
      ("temuemail.com", "temuemail.com", "Temu"),
      ("iliad", "iliad.it", "Iliad"),
      ("'amazon.it' or 'amazon.com'", "amazon.it", "Amazon"));
    var rules = OutlookRules.FromBytes(bytes, "rules.rwz");
    Assert.Equal(3, rules.Count);
    Assert.Equal("temuemail.com", rules[0].Name);
    Assert.Equal("Temu", rules[0].Folder);
    Assert.Equal("temuemail.com", rules[0].Conditions[0].Value);
    Assert.Equal("iliad", rules[1].Name);
    Assert.Equal("iliad.it", rules[1].Conditions[0].Value);
    Assert.Equal("Iliad", rules[1].Folder);
    Assert.Equal("or", rules[2].Logic);
    Assert.Contains(rules[2].Conditions, c => c.Value == "amazon.it");
    Assert.Contains(rules[2].Conditions, c => c.Value == "amazon.com");
    Assert.Equal("Amazon", rules[2].Folder);
    Assert.True(MailRuleEngine.Matches(rules[2], "order@amazon.com", "", "Order", "", false));
  }

  [Fact]
  public void FromBytes_ReadsOutlookExportIfPresent() {
    var path = SampleRwz();
    Assert.SkipWhen(path is null, "Untitled.rwz is a local Outlook export, not in CI.");
    var rules = OutlookRules.FromFile(path!);
    Assert.True(rules.Count >= 20, "expected the exported Outlook rules, got " + rules.Count);
    Assert.Contains(rules, r => r.Folder == "Temu" && r.Conditions.Any(c => c.Value.Contains("temuemail.com")));
    Assert.Contains(rules, r => r.Folder == "Iliad" && r.Conditions.Any(c => c.Value.Contains("iliad.it")));
    Assert.Contains(rules, r => r.Folder == "Amazon" && r.Logic == "or");
    Assert.Contains(rules, r => r.Folder == "Studio Giovannini");
    Assert.DoesNotContain(rules, r => r.Name.Contains('@'));
  }

  [Fact]
  public void ToJson_RoundTrips() {
    var json = OutlookRules.ToJson([
      new MailRule {
        Name = "Comune",
        Action = MailRuleAction.Move,
        Folder = "Archive",
        MailboxId = "box-1",
        Conditions = [new() { Field = "from", Value = "comune.it" }]
      }
    ]);
    var rules = OutlookRules.FromBytes(json, "rules.json");
    Assert.Single(rules);
    Assert.Equal("Comune", rules[0].Name);
    Assert.Equal("box-1", rules[0].MailboxId);
  }

  private static byte[] ModernRwz(params (string Name, string From, string Folder)[] rules) {
    using var buffer = new MemoryStream();
    var header = new byte[32];
    header[2] = 0x14;
    header[6] = 0x14;
    header[7] = 0x06;
    buffer.Write(header);
    WriteInt(buffer, 1);
    WriteInt(buffer, 1);
    WriteInt(buffer, 0);
    WriteInt(buffer, rules.Length);
    WriteName(buffer, "user@example.com");
    WritePath(buffer, @"C:\mail.ost");
    WriteFolder(buffer, "Inbox");
    foreach (var rule in rules) {
      WriteName(buffer, rule.Name);
      WriteFrom(buffer, rule.From);
      WritePath(buffer, @"C:\mail.ost");
      WriteFolder(buffer, rule.Folder);
    }

    return buffer.ToArray();
  }

  private static void WriteName(Stream stream, string text) {
    stream.WriteByte(0x14);
    stream.WriteByte(0);
    WriteCounted(stream, text);
  }

  private static void WriteFrom(Stream stream, string text) {
    stream.Write([0x01, 0x80, 0xE6, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);
    WriteCounted(stream, text);
  }

  private static void WritePath(Stream stream, string path) {
    stream.Write(Encoding.Unicode.GetBytes(path));
    stream.WriteByte(0);
    stream.WriteByte(0);
  }

  private static void WriteFolder(Stream stream, string text) =>
    WriteCounted(stream, text);

  private static void WriteCounted(Stream stream, string text) {
    var chars = Encoding.Unicode.GetBytes(text);
    stream.WriteByte((byte)(chars.Length / 2));
    stream.Write(chars);
  }

  private static void WriteInt(Stream stream, int value) =>
    stream.Write(BitConverter.GetBytes(value));

  private static string? SampleRwz() {
    var dir = AppContext.BaseDirectory;
    for (var i = 0; i < 10 && !string.IsNullOrEmpty(dir); i++) {
      var path = Path.Combine(dir, "Untitled.rwz");
      if (File.Exists(path))
        return path;
      dir = Path.GetDirectoryName(dir) ?? "";
    }

    return null;
  }
}
