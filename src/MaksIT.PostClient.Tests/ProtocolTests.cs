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
      new MailFolderInfo { FullName = "Clients", Name = "Clients" }
    };
    var rows = MailFolderCatalog.Normalize(folders);
    var names = rows.Select(f => f.Name).ToList();
    Assert.DoesNotContain("[Gmail]", names);
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
}
