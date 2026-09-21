using System.Text;


namespace MaksIT.PostClient.Tests.Auth;


public class MailAuthKindTests {
  [Fact]
  public void Normalize_MapsAliases() {
    Assert.Equal(MailAuthKind.Password, MailAuthKind.Normalize(null));
    Assert.Equal(MailAuthKind.Google, MailAuthKind.Normalize("gmail"));
    Assert.Equal(MailAuthKind.Microsoft, MailAuthKind.Normalize("outlook"));
    Assert.True(MailAuthKind.IsOAuth("google"));
    Assert.False(MailAuthKind.IsOAuth("password"));
  }
}


public class MailProviderTests {
  [Fact]
  public void Gmail_FillsImapAndSmtp() {
    var box = new MailboxAccount { Provider = "gmail" };
    MailProvider.Apply(box);
    Assert.Equal("imap.gmail.com", box.ImapHost);
    Assert.Equal(993, box.ImapPort);
    Assert.Equal("smtp.gmail.com", box.SmtpHost);
    Assert.Equal(465, box.SmtpPort);
    Assert.Equal(MailSecurity.Ssl, box.SmtpSecurity);
  }

  [Fact]
  public void Gmail_Pop3_UsesPopHost() {
    var box = new MailboxAccount { Provider = "gmail", IncomingProtocol = MailProtocol.Pop3 };
    MailProvider.Apply(box);
    Assert.Equal("pop.gmail.com", box.ImapHost);
    Assert.Equal(995, box.ImapPort);
  }

  [Fact]
  public void Outlook_UsesOffice365AndStartTlsSmtp() {
    var box = new MailboxAccount { Provider = "microsoft" };
    MailProvider.Apply(box);
    Assert.Equal(MailProvider.Outlook, box.Provider);
    Assert.Equal("outlook.office365.com", box.ImapHost);
    Assert.Equal("smtp.office365.com", box.SmtpHost);
    Assert.Equal(587, box.SmtpPort);
    Assert.Equal(MailSecurity.StartTls, box.SmtpSecurity);
  }

  [Fact]
  public void Aruba_FillsOfficialPecHosts() {
    var box = new MailboxAccount { Provider = "arubapec" };
    MailProvider.Apply(box);
    Assert.Equal(MailProvider.Aruba, box.Provider);
    Assert.Equal("imaps.pec.aruba.it", box.ImapHost);
    Assert.Equal(993, box.ImapPort);
    Assert.Equal("smtps.pec.aruba.it", box.SmtpHost);
    Assert.Equal(465, box.SmtpPort);
    Assert.Equal(MailSecurity.Ssl, box.SmtpSecurity);
    Assert.True(MailProvider.IsPec(box.Provider));
    Assert.False(MailProvider.UsesOAuth(box.Provider));
  }

  [Fact]
  public void Aruba_Pop3_UsesPopHost() {
    var box = new MailboxAccount { Provider = MailProvider.Aruba, IncomingProtocol = MailProtocol.Pop3 };
    MailProvider.Apply(box);
    Assert.Equal("pop3s.pec.aruba.it", box.ImapHost);
    Assert.Equal(995, box.ImapPort);
  }

  [Fact]
  public void LegalmailNamirialPosteRegister_FillOfficialHosts() {
    var legal = new MailboxAccount { Provider = "infocert" };
    MailProvider.Apply(legal);
    Assert.Equal("mbox.cert.legalmail.it", legal.ImapHost);
    Assert.Equal("sendm.cert.legalmail.it", legal.SmtpHost);

    var namirial = new MailboxAccount { Provider = "sicurezzapostale" };
    MailProvider.Apply(namirial);
    Assert.Equal("imaps.sicurezzapostale.it", namirial.ImapHost);
    Assert.Equal("smtps.sicurezzapostale.it", namirial.SmtpHost);

    var poste = new MailboxAccount { Provider = "posteitaliane" };
    MailProvider.Apply(poste);
    Assert.Equal("mail.postecert.it", poste.ImapHost);
    Assert.Equal("mail.postecert.it", poste.SmtpHost);

    var register = new MailboxAccount { Provider = "registerpec" };
    MailProvider.Apply(register);
    Assert.Equal("imap.pec-email.com", register.ImapHost);
    Assert.Equal("smtp.pec-email.com", register.SmtpHost);
  }

  [Fact]
  public void LiberoAndIntesi_FillOfficialHosts() {
    var libero = new MailboxAccount { Provider = "liberopec" };
    MailProvider.Apply(libero);
    Assert.Equal(MailProvider.Libero, libero.Provider);
    Assert.Equal("mail.postacert.it.net", libero.ImapHost);
    Assert.Equal("mail.postacert.it.net", libero.SmtpHost);
    Assert.True(MailProvider.IsPec(libero.Provider));

    var intesi = new MailboxAccount { Provider = "intesigroup" };
    MailProvider.Apply(intesi);
    Assert.Equal(MailProvider.Intesi, intesi.Provider);
    Assert.Equal("imap.ig-trustmail.com", intesi.ImapHost);
    Assert.Equal("smtp.ig-trustmail.com", intesi.SmtpHost);

    var pop = new MailboxAccount { Provider = MailProvider.Intesi, IncomingProtocol = MailProtocol.Pop3 };
    MailProvider.Apply(pop);
    Assert.Equal("pop.ig-trustmail.com", pop.ImapHost);
    Assert.Equal(995, pop.ImapPort);
  }

  [Fact]
  public void Imap_LeavesManualHosts() {
    var box = new MailboxAccount { Provider = "imap", ImapHost = "mail.studio.it", SmtpHost = "smtp.studio.it" };
    MailProvider.Apply(box);
    Assert.Equal(MailProvider.Imap, box.Provider);
    Assert.Equal("mail.studio.it", box.ImapHost);
    Assert.Equal("smtp.studio.it", box.SmtpHost);
    Assert.False(MailProvider.HasHostPreset(box.Provider));
  }
}


public class OAuthClientIdTests {
  [Fact]
  public void IsGoogle_RequiresAppsGoogleusercontent() {
    Assert.True(OAuthClientId.IsGoogle("123-abc.apps.googleusercontent.com"));
    Assert.False(OAuthClientId.IsGoogle("user@gmail.com"));
    Assert.False(OAuthClientId.IsGoogle(""));
  }

  [Fact]
  public void Normalize_ReadsInstalledClientIdJson() {
    var id = OAuthClientId.Normalize(
      "{\"installed\":{\"client_id\":\"123-abc.apps.googleusercontent.com\"}}");
    Assert.Equal("123-abc.apps.googleusercontent.com", id);
  }

  [Fact]
  public void Preview_KeepsProjectPrefix() {
    var preview = OAuthClientId.Preview("123456789-abcde.apps.googleusercontent.com");
    Assert.StartsWith("123456789-abcd", preview, StringComparison.Ordinal);
    Assert.EndsWith(OAuthClientId.GoogleSuffix, preview, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void IsMicrosoft_RequiresGuid() {
    Assert.True(OAuthClientId.IsMicrosoft("11111111-1111-1111-1111-111111111111"));
    Assert.False(OAuthClientId.IsMicrosoft("user@outlook.com"));
  }
}


public class OAuthClientSecretTests {
  [Fact]
  public void Normalize_ReadsWebClientSecretJson() {
    var secret = OAuthClientSecret.Normalize(
      "{\"web\":{\"client_secret\":\"GOCSPX-secret\"}}");
    Assert.Equal("GOCSPX-secret", secret);
  }
}


public class OAuthLoginHintTests {
  [Fact]
  public void Google_SkipsNonGmailAddress() {
    Assert.Null(OAuthLoginHint.For("google", "maksym.sadovnychyy@pecsicura.com"));
    Assert.Equal(
      "a@gmail.com",
      OAuthLoginHint.For("google", "a@gmail.com"));
  }
}


public class OAuthMailScopeTests {
  [Fact]
  public void Google_RequiresMailGoogleCom() {
    Assert.True(OAuthMailScope.GrantsMail("google", "openid email https://mail.google.com/"));
    Assert.False(OAuthMailScope.GrantsMail("google", "openid email"));
    Assert.False(OAuthMailScope.GrantsMail("google", ""));
  }

  [Fact]
  public void Microsoft_RequiresImapOrSmtp() {
    Assert.True(OAuthMailScope.GrantsMail("microsoft", "offline_access IMAP.AccessAsUser.All"));
    Assert.False(OAuthMailScope.GrantsMail("microsoft", "openid email"));
  }
}


public class OAuthPkceTests {
  [Fact]
  public void ChallengeS256_Rfc7636Vector() {
    var challenge = OAuthPkce.ChallengeS256("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk");
    Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", challenge);
  }

  [Fact]
  public void EmailFromIdToken_ReadsEmailClaim() {
    var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"email\":\"a@gmail.com\"}"))
      .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    var email = OAuthPkce.EmailFromIdToken("hdr." + payload + ".sig");
    Assert.Equal("a@gmail.com", email);
  }
}
