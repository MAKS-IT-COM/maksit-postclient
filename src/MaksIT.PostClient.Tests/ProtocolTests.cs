using System.Threading;
using MaksIT.Results;
using MaksIT.PostClient.Client;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Tests;


public class MailProtocolTests {
  [Fact]
  public void NormalizeIncoming_BlankIsImap() =>
    Assert.Equal(MailProtocol.Imap, MailProtocol.NormalizeIncoming(""));

  [Fact]
  public void NormalizeIncoming_PopVariants() {
    Assert.True(MailProtocol.IsPop3("POP3"));
    Assert.True(MailProtocol.IsPop3("pop"));
    Assert.False(MailProtocol.IsPop3("imap"));
  }

  [Fact]
  public void NormalizeIncoming_Pst() {
    Assert.Equal(MailProtocol.Pst, MailProtocol.NormalizeIncoming("OST"));
    Assert.True(MailProtocol.IsPst("pst"));
    Assert.False(MailProtocol.IsPst("imap"));
  }
}


public class MailSecurityTests {
  [Fact]
  public void DefaultIncomingPorts() {
    Assert.Equal(993, MailSecurity.DefaultIncomingPort(MailProtocol.Imap, MailSecurity.Ssl));
    Assert.Equal(143, MailSecurity.DefaultIncomingPort(MailProtocol.Imap, MailSecurity.StartTls));
    Assert.Equal(995, MailSecurity.DefaultIncomingPort(MailProtocol.Pop3, MailSecurity.Ssl));
    Assert.Equal(110, MailSecurity.DefaultIncomingPort(MailProtocol.Pop3, MailSecurity.None));
  }

  [Fact]
  public void DefaultSmtpPorts() {
    Assert.Equal(465, MailSecurity.DefaultSmtpPort(MailSecurity.Ssl));
    Assert.Equal(587, MailSecurity.DefaultSmtpPort(MailSecurity.StartTls));
    Assert.Equal(25, MailSecurity.DefaultSmtpPort(MailSecurity.None));
  }

  [Fact]
  public void Normalize_LegacySslFalse() =>
    Assert.Equal(MailSecurity.StartTlsWhenAvailable, MailSecurity.Normalize(null, false));
}


public class MailSessionFactoryTests {
  [Fact]
  public void Create_ImapByDefault() {
    var session = new MailSessionFactory(new FakeMailAuth()).Create(new MailboxAccount());
    Assert.IsType<ImapMailSession>(session);
  }

  [Fact]
  public void Create_Pop3WhenAsked() {
    var session = new MailSessionFactory(new FakeMailAuth()).Create(
      new MailboxAccount { IncomingProtocol = MailProtocol.Pop3 });
    Assert.IsType<Pop3MailSession>(session);
  }

  [Fact]
  public void Create_PstStore() {
    var session = new MailSessionFactory(new FakeMailAuth()).Create(
      new MailboxAccount { IncomingProtocol = MailProtocol.Pst, Provider = MailProvider.Pst });
    Assert.IsType<PstMailSession>(session);
  }

  [Fact]
  public void Create_LocalStore() {
    var session = new MailSessionFactory(new FakeMailAuth()).Create(
      new MailboxAccount { IncomingProtocol = MailProtocol.Store, Provider = MailProvider.Store });
    Assert.IsType<LocalStoreSession>(session);
  }
}


file sealed class FakeMailAuth : IMailAuthService {
  public Task<Result<MailAuthMaterial>> ResolveAsync(
    MailboxAccount account,
    string? password,
    CancellationToken cancellationToken = default) {
    _ = account;
    _ = cancellationToken;
    return Task.FromResult(Result<MailAuthMaterial>.Ok(new MailAuthMaterial { Password = password ?? "" }));
  }

  public Task<Result<OAuthTokenSet>> SignInAsync(
    string authKind,
    string? loginHint = null,
    CancellationToken cancellationToken = default) {
    _ = authKind;
    _ = loginHint;
    _ = cancellationToken;
    return Task.FromResult(Result<OAuthTokenSet>.BadRequest(null, "not used"));
  }

  public Result SaveTokens(string mailboxId, OAuthTokenSet tokens) {
    _ = mailboxId;
    _ = tokens;
    return Result.Ok();
  }

  public Result DeleteTokens(string mailboxId) {
    _ = mailboxId;
    return Result.Ok();
  }

  public bool HasTokens(string mailboxId) {
    _ = mailboxId;
    return false;
  }

  public string DesktopLoginUrl(string authKind) =>
    IdentityHubAddress.DesktopLoginUrl(authKind);

  public Result<OAuthTokenSet> CompleteHubSignIn(string json, string authKind) {
    _ = json;
    _ = authKind;
    return Result<OAuthTokenSet>.BadRequest(null, "not used");
  }

  public Task<Result<OAuthTokenSet>> CompleteHubSignInAsync(
    string json,
    string authKind,
    CancellationToken cancellationToken = default) {
    _ = cancellationToken;
    return Task.FromResult(CompleteHubSignIn(json, authKind));
  }
}


public class MailFolderCatalogTests {
  [Fact]
  public void Normalize_HidesGmailVirtualAndDedupsSent() {
    var folders = new[] {
      new MailFolderInfo { FullName = "INBOX", Name = "INBOX", Kind = "inbox" },
      new MailFolderInfo { FullName = "[Gmail]", Name = "[Gmail]" },
      new MailFolderInfo { FullName = "[Gmail]/All Mail", Name = "All Mail" },
      new MailFolderInfo { FullName = "[Gmail]/Sent Mail", Name = "Sent Mail" },
      new MailFolderInfo { FullName = "Sent", Name = "Sent" },
      new MailFolderInfo { FullName = "[Gmail]/Starred", Name = "Starred" },
      new MailFolderInfo { FullName = "[Gmail]/Important", Name = "Important" },
      new MailFolderInfo { FullName = "[Gmail]/Drafts", Name = "Drafts" },
      new MailFolderInfo { FullName = "[Gmail]/Trash", Name = "Trash" },
      new MailFolderInfo { FullName = "[Gmail]/Spam", Name = "Spam" },
      new MailFolderInfo { FullName = "[Gmail]/Allianz", Name = "[Gmail]/Allianz" },
      new MailFolderInfo { FullName = "Clients", Name = "Clients" }
    };
    var rows = MailFolderCatalog.Normalize(folders, MailFolderLayout.Gmail);
    var names = rows.Select(f => f.Name).ToList();
    Assert.Contains("[Gmail]", names);
    Assert.DoesNotContain("All Mail", names);
    Assert.DoesNotContain("Starred", names);
    Assert.DoesNotContain("Important", names);
    Assert.DoesNotContain("Sent Mail", names);
    Assert.Single(rows, f => f.Name == "Sent");
    Assert.Equal("[Gmail]/Sent Mail", rows.Single(f => f.Name == "Sent").FullName);
    Assert.Contains("Inbox", names);
    Assert.Contains("Drafts", names);
    Assert.Contains("Trash", names);
    Assert.Contains("Junk", names);
    Assert.Contains("Clients", names);
    Assert.Equal("Allianz", rows.Single(f => f.FullName == "[Gmail]/Allianz").Name);
  }

  [Fact]
  public void Normalize_KeepsGmailLabelDots() {
    var folders = new[] {
      new MailFolderInfo { FullName = "INBOX", Name = "INBOX", Kind = "inbox", Delimiter = '/' },
      new MailFolderInfo { FullName = "[Gmail]/Sent Mail", Name = "Sent Mail", Delimiter = '/' },
      new MailFolderInfo { FullName = "P.IVA", Name = "P.IVA", Delimiter = '/' },
      new MailFolderInfo { FullName = "P.IVA/2024", Name = "2024", Delimiter = '/' }
    };
    var rows = MailFolderCatalog.Normalize(folders, MailFolderLayout.Gmail);
    Assert.DoesNotContain(rows, f => f.FullName == "P");
    Assert.Equal("P.IVA", rows.Single(f => f.FullName == "P.IVA").Name);
    Assert.Equal("P.IVA", MailFolderPath.TreeParent(
      "P.IVA/2024",
      rows.Select(f => f.FullName)));
    Assert.Null(MailFolderPath.TreeParent("P.IVA", rows.Select(f => f.FullName)));
  }

  [Fact]
  public void Normalize_InsertsMissingImapParents() {
    var folders = new[] {
      new MailFolderInfo { FullName = "INBOX", Name = "INBOX", Kind = "inbox" },
      new MailFolderInfo { FullName = "[Gmail]/PayPal", Name = "PayPal" },
      new MailFolderInfo { FullName = "[Gmail]/Sent Mail", Name = "Sent Mail" },
      new MailFolderInfo { FullName = "Auto-doc", Name = "Auto-doc" }
    };
    var rows = MailFolderCatalog.Normalize(folders, MailFolderLayout.Gmail);
    Assert.Contains(rows, f => f.FullName == "[Gmail]" && f.Name == "[Gmail]");
    Assert.Equal("[Gmail]", MailFolderPath.TreeParent(
      "[Gmail]/PayPal",
      rows.Select(f => f.FullName)));
    Assert.Null(MailFolderPath.TreeParent("Auto-doc", rows.Select(f => f.FullName)));
  }

  [Fact]
  public void Normalize_KeepsUnreadAndTotal() {
    var folders = new[] {
      new MailFolderInfo { FullName = "INBOX", Name = "INBOX", Kind = "inbox", Unread = 4, Total = 20 }
    };
    var row = Assert.Single(MailFolderCatalog.Normalize(folders));
    Assert.Equal("Inbox", row.Name);
    Assert.Equal(4, row.Unread);
    Assert.Equal(20, row.Total);
  }

  [Fact]
  public void Normalize_PecKeepsInboxDotFoldersAndNamedStarred() {
    var folders = new[] {
      new MailFolderInfo { FullName = "INBOX", Name = "INBOX", Kind = "inbox", Delimiter = '.' },
      new MailFolderInfo { FullName = "INBOX.Drafts", Name = "Drafts", Delimiter = '.' },
      new MailFolderInfo { FullName = "INBOX.Sent", Name = "Sent", Delimiter = '.' },
      new MailFolderInfo { FullName = "INBOX.Trash", Name = "Trash", Delimiter = '.' },
      new MailFolderInfo { FullName = "INBOX.Starred", Name = "Starred", Delimiter = '.' },
      new MailFolderInfo { FullName = "INBOX.Clients", Name = "Clients", Delimiter = '.' }
    };
    var rows = MailFolderCatalog.Normalize(folders, MailFolderLayout.Pec);
    Assert.DoesNotContain(rows, f => MailFolderPath.IsGmailMailbox(f.FullName));
    Assert.Contains(rows, f => f.FullName == "INBOX.Drafts");
    Assert.Contains(rows, f => f.FullName == "INBOX.Starred");
    Assert.Contains(rows, f => f.FullName == "INBOX.Clients");
    var names = rows.Select(f => f.FullName).ToList();
    Assert.Null(MailFolderRole.DisplayParent("Drafts", "INBOX.Drafts", names, MailFolderLayout.Pec, '.'));
    Assert.Equal("INBOX", MailFolderRole.DisplayParent("Clients", "INBOX.Clients", names, MailFolderLayout.Pec, '.'));
  }

  [Fact]
  public void Normalize_ImapDoesNotHideAllMailByGmailRules() {
    var folders = new[] {
      new MailFolderInfo { FullName = "INBOX", Name = "INBOX", Kind = "inbox" },
      new MailFolderInfo { FullName = "All Mail", Name = "All Mail" }
    };
    var rows = MailFolderCatalog.Normalize(folders, MailFolderLayout.Imap);
    Assert.Contains(rows, f => f.FullName == "All Mail");
  }
}


public class PstMailSessionTests {
  [Fact]
  public async Task UnicodePst_RoundTripWrites() {
    var dir = Path.Combine(Path.GetTempPath(), "postclient-pst-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    var path = Path.Combine(dir, "mail.pst");
    try {
      OutlookPst.CreateEmpty(path);
      await using var session = new PstMailSession();
      var token = TestContext.Current.CancellationToken;
      var connected = await session.ConnectAsync(
        new MailboxAccount {
          IncomingProtocol = MailProtocol.Pst,
          Provider = MailProvider.Pst,
          StorePath = path
        },
        "",
        token);
      Assert.True(connected.IsSuccess, string.Join(" ", connected.Messages));

      var folders = await session.ListFoldersAsync(token);
      Assert.True(folders.IsSuccess, string.Join(" ", folders.Messages));
      Assert.Contains(
        folders.Value!,
        folder => folder.FullName.Equals("Inbox", StringComparison.OrdinalIgnoreCase));

      var appended = await session.AppendAsync("Inbox", Eml("one"), token);
      Assert.True(appended.IsSuccess, string.Join(" ", appended.Messages));

      var listed = await session.ListMessagesAsync("Inbox", null, token);
      Assert.True(listed.IsSuccess, string.Join(" ", listed.Messages));
      var id = Assert.Single(listed.Value!.Present!);
      Assert.Contains(
        "test@example.com",
        Assert.Single(listed.Value.Headers).From,
        StringComparison.OrdinalIgnoreCase);

      var body = await session.GetMessageAsync("Inbox", id, token);
      Assert.True(body.IsSuccess, string.Join(" ", body.Messages));
      Assert.Contains("one", body.Value!.Header.Subject, StringComparison.OrdinalIgnoreCase);
      Assert.Contains("test@example.com", body.Value.Header.From, StringComparison.OrdinalIgnoreCase);

      var created = await session.CreateFolderAsync("Project", null, token);
      Assert.True(created.IsSuccess, string.Join(" ", created.Messages));

      var moved = await session.MoveMessagesAsync("Inbox", [id], "Project", token);
      Assert.True(moved.IsSuccess, string.Join(" ", moved.Messages));

      var project = await session.ListMessagesAsync("Project", null, token);
      Assert.True(project.IsSuccess, string.Join(" ", project.Messages));
      var movedId = Assert.Single(project.Value!.Present!);
      Assert.Contains(
        "test@example.com",
        Assert.Single(project.Value.Headers).From,
        StringComparison.OrdinalIgnoreCase);

      var flagged = await session.SetMessageFlagsAsync(
        "Project",
        [movedId],
        new MailFlagUpdate { Flagged = true, Seen = true },
        token);
      Assert.True(flagged.IsSuccess, string.Join(" ", flagged.Messages));

      var emptied = await session.EmptyFolderAsync("Project", "Deleted Items", token);
      Assert.True(emptied.IsSuccess, string.Join(" ", emptied.Messages));
      var trash = await session.ListMessagesAsync("Deleted Items", null, token);
      Assert.True(trash.IsSuccess, string.Join(" ", trash.Messages));
      Assert.Single(trash.Value!.Present!);
    }
    finally {
      try {
        Directory.Delete(dir, true);
      }
      catch {
      }
    }
  }

  [Fact]
  public async Task ListMessages_ItemBudgetLeavesFolderIncomplete() {
    var dir = Path.Combine(Path.GetTempPath(), "postclient-pst-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    var path = Path.Combine(dir, "mail.pst");
    try {
      OutlookPst.CreateEmpty(path);
      await using var session = new PstMailSession();
      var token = TestContext.Current.CancellationToken;
      var connected = await session.ConnectAsync(
        new MailboxAccount {
          IncomingProtocol = MailProtocol.Pst,
          Provider = MailProvider.Pst,
          StorePath = path
        },
        "",
        token);
      Assert.True(connected.IsSuccess, string.Join(" ", connected.Messages));
      Assert.True((await session.AppendAsync("Inbox", Eml("one"), token)).IsSuccess);
      Assert.True((await session.AppendAsync("Inbox", Eml("two"), token)).IsSuccess);

      var first = await session.ListMessagesAsync("Inbox", null, token, 1);
      Assert.True(first.IsSuccess, string.Join(" ", first.Messages));
      Assert.True(first.Value!.Incomplete);
      Assert.Null(first.Value.Present);
      Assert.Single(first.Value.Headers);

      var second = await session.ListMessagesAsync("Inbox", null, token, 1);
      Assert.True(second.IsSuccess, string.Join(" ", second.Messages));
      Assert.False(second.Value!.Incomplete);
      Assert.Equal(2, second.Value.Present!.Count);
    }
    finally {
      try {
        Directory.Delete(dir, true);
      }
      catch {
      }
    }
  }

  [Fact]
  public async Task GetMessage_OpensByItemIdWithoutListingFirst() {
    var dir = Path.Combine(Path.GetTempPath(), "postclient-pst-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    var path = Path.Combine(dir, "mail.pst");
    try {
      OutlookPst.CreateEmpty(path);
      uint firstId;
      uint secondId;
      var token = TestContext.Current.CancellationToken;
      await using (var writer = new PstMailSession()) {
        var connected = await writer.ConnectAsync(
          new MailboxAccount {
            IncomingProtocol = MailProtocol.Pst,
            Provider = MailProvider.Pst,
            StorePath = path
          },
          "",
          token);
        Assert.True(connected.IsSuccess, string.Join(" ", connected.Messages));
        Assert.True((await writer.AppendAsync("Inbox", Eml("same"), token)).IsSuccess);
        Assert.True((await writer.AppendAsync("Inbox", Eml("same"), token)).IsSuccess);
        var listed = await writer.ListMessagesAsync("Inbox", null, token);
        Assert.True(listed.IsSuccess, string.Join(" ", listed.Messages));
        Assert.Equal(2, listed.Value!.Present!.Count);
        Assert.NotEqual(listed.Value.Present[0], listed.Value.Present[1]);
        firstId = listed.Value.Present[0];
        secondId = listed.Value.Present[1];
      }

      await using var reader = new PstMailSession();
      var reopened = await reader.ConnectAsync(
        new MailboxAccount {
          IncomingProtocol = MailProtocol.Pst,
          Provider = MailProvider.Pst,
          StorePath = path
        },
        "",
        token);
      Assert.True(reopened.IsSuccess, string.Join(" ", reopened.Messages));
      var first = await reader.GetMessageAsync("Inbox", firstId, token);
      Assert.True(first.IsSuccess, string.Join(" ", first.Messages));
      Assert.Contains("same", first.Value!.Header.Subject, StringComparison.OrdinalIgnoreCase);
      var second = await reader.GetMessageAsync("Inbox", secondId, token);
      Assert.True(second.IsSuccess, string.Join(" ", second.Messages));
      Assert.Contains("same", second.Value!.Header.Subject, StringComparison.OrdinalIgnoreCase);
    }
    finally {
      try {
        Directory.Delete(dir, true);
      }
      catch {
      }
    }
  }

  private static byte[] Eml(string subject) =>
    System.Text.Encoding.ASCII.GetBytes(
      "From: test@example.com\r\nTo: you@example.com\r\nSubject: "
      + subject
      + "\r\nDate: Mon, 14 Sep 2026 08:00:00 +0000\r\nMessage-ID: <"
      + subject
      + "@example.com>\r\nMIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nbody\r\n");
}


public class MailSessionGateTests {
  [Fact]
  public async Task RunAsync_DoesNotUseCallerSynchronizationContext() {
    using var gate = new MailSessionGate();
    var context = new SynchronizationContext();
    var previous = SynchronizationContext.Current;
    SynchronizationContext.SetSynchronizationContext(context);
    try {
      SynchronizationContext? seen = context;
      await gate.RunAsync(() => {
        seen = SynchronizationContext.Current;
        return Task.FromResult(0);
      }, TestContext.Current.CancellationToken);
      Assert.NotSame(context, seen);
    }
    finally {
      SynchronizationContext.SetSynchronizationContext(previous);
    }
  }
}


public class PstSubjectTests {
  [Fact]
  public void Display_StripsPstMetadataPrefix() {
    var raw = "\u0001\u0005RE: Fattura IMU";
    Assert.Equal("RE: Fattura IMU", PstSubject.Display(raw));
  }

  [Fact]
  public void Display_UsesNormalizedWhenRawIsControlBytes() {
    Assert.Equal(
      "RE: Fattura IMU",
      PstSubject.Display("\u0001\u0002", "Fattura IMU", "RE:"));
  }

  [Fact]
  public void Display_LeavesPlainSubject() =>
    Assert.Equal("Ciao", PstSubject.Display("Ciao"));
}


public class ImapFetchPolicyTests {
  [Fact]
  public void ExtraHeaders_SkippedForGmailAndOutlook() {
    Assert.Null(ImapFetchPolicy.ExtraHeaders(MailProvider.Gmail));
    Assert.Null(ImapFetchPolicy.ExtraHeaders("hotmail"));
    Assert.NotNull(ImapFetchPolicy.ExtraHeaders(MailProvider.Aruba));
    Assert.NotNull(ImapFetchPolicy.ExtraHeaders(MailProvider.Imap));
  }

  [Fact]
  public void PreferComplete_KeepsEnvelopeOverFlagsOnly() {
    Assert.False(ImapFetchPolicy.PreferComplete(false, true, false, true));
    Assert.True(ImapFetchPolicy.PreferComplete(true, false, true, false));
    Assert.True(ImapFetchPolicy.PreferComplete(true, true, true, true));
  }

  [Fact]
  public void IsInboxPath_MatchesHotmailInboxNames() {
    Assert.True(ImapFetchPolicy.IsInboxPath("INBOX"));
    Assert.True(ImapFetchPolicy.IsInboxPath("Inbox"));
    Assert.False(ImapFetchPolicy.IsInboxPath("Sent"));
    Assert.False(ImapFetchPolicy.IsInboxPath("Deleted Items"));
  }
}


public class OutlookPstHeaderTests {
  [Fact]
  public void IsAnsi_UnicodeEmptyPstIsFalse() {
    var dir = Path.Combine(Path.GetTempPath(), "postclient-pst-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    var path = Path.Combine(dir, "mail.pst");
    try {
      OutlookPst.CreateEmpty(path);
      Assert.False(OutlookPst.IsAnsi(path));
      Assert.EndsWith(".postclient.pst", OutlookPst.UnicodeCopyPath(path), StringComparison.OrdinalIgnoreCase);
    }
    finally {
      try {
        Directory.Delete(dir, true);
      }
      catch {
      }
    }
  }
}
