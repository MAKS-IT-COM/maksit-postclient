using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Tests;


public class AppInfoTests {
  [Fact]
  public void Credits_and_email_are_maksit() {
    Assert.Equal("MaksIT", AppInfo.Brand);
    Assert.Equal("Postclient", AppInfo.ProductName);
    Assert.Equal("Maksym Sadovnychyy", AppInfo.Credits);
    Assert.Equal("maksym.sadovnychyy@gmail.com", AppInfo.Email);
    Assert.Equal("https://maks-it.com", AppInfo.SiteUri);
    Assert.Equal("Desktop mail client for PEC, REM, and IMAP. Mail stays on this PC.", AppInfo.Summary);
    Assert.Contains(DateTime.UtcNow.Year.ToString(), AppInfo.Copyright, StringComparison.Ordinal);
  }
}


public class AppPathsTests {
  [Fact]
  public void ProductFolderIsPlaceholderBrandNotMaksIt() {
    Assert.Equal("Postclient", AppPaths.ProductName);
    Assert.Equal("postclient", AppPaths.ProductId);
    Assert.DoesNotContain("MaksIT", AppPaths.ConfigDirectory(), StringComparison.OrdinalIgnoreCase);
    Assert.EndsWith("webview", AppPaths.WebViewDirectory(), StringComparison.OrdinalIgnoreCase);
    Assert.EndsWith("models", AppPaths.ModelsDirectory(), StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void ConfigEnvOverridesDefault() {
    var previous = Environment.GetEnvironmentVariable(AppPaths.ConfigEnv);
    var dir = Path.Combine(Path.GetTempPath(), "postclient-config-" + Guid.NewGuid().ToString("N"));
    try {
      Environment.SetEnvironmentVariable(AppPaths.ConfigEnv, dir);
      Assert.Equal(Path.GetFullPath(dir), AppPaths.ConfigDirectory());
    }
    finally {
      Environment.SetEnvironmentVariable(AppPaths.ConfigEnv, previous);
    }
  }
}


public class MessageTextTests {
  [Fact]
  public void StripHtml_DecodesAndDropsTags() {
    var text = MessageText.StripHtml("<p>Fattura &amp; ricevuta</p>");
    Assert.Equal("Fattura & ricevuta", text);
  }

  [Fact]
  public void StripHtml_Empty() {
    Assert.Equal("", MessageText.StripHtml("  "));
  }
}


public class MessageHtmlTests {
  [Fact]
  public void FromPlain_EscapesAndBreaks() {
    var html = MessageHtml.FromPlain("A & B\nC");
    Assert.Contains("A &amp; B", html);
    Assert.Contains("<br>", html);
  }

  [Fact]
  public void Document_WrapsFragment() {
    var html = MessageHtml.Document("<p>Hello</p>");
    Assert.Contains("<p>Hello</p>", html);
    Assert.Contains("Content-Security-Policy", html);
    Assert.Contains("<html>", html);
  }

  [Fact]
  public void Document_KeepsFullHtml() {
    var html = MessageHtml.Document("<html><head></head><body>Hi</body></html>");
    Assert.Contains("Content-Security-Policy", html);
    Assert.Contains("<body>Hi</body>", html);
    Assert.Contains("color-scheme:light", html);
    Assert.Contains("html{color-scheme:light;background:#fff;}", html);
    Assert.True(MessageHtml.LooksLikeDocument(html));
  }

  [Fact]
  public void Document_KeepsBodyBgcolor() {
    var html = MessageHtml.Document("""<html><head></head><body bgcolor="#ffcc00">Hi</body></html>""");
    Assert.Contains("bgcolor=\"#ffcc00\"", html);
    Assert.DoesNotContain("body{background", html);
  }

  [Fact]
  public void InlineCid_RewritesToDataUri() {
    var html = MessageHtml.InlineCid(
      "<img src=\"cid:pic@mail\">",
      new Dictionary<string, string> { ["pic@mail"] = "data:image/png;base64,xx" });
    Assert.Equal("<img src=\"data:image/png;base64,xx\">", html);
  }

  [Fact]
  public void HasMarkup_DetectsHtmlNotPlain() {
    Assert.True(MessageHtml.HasMarkup("<p>Hello</p>"));
    Assert.True(MessageHtml.HasMarkup("<html><body>Hi</body></html>"));
    Assert.False(MessageHtml.HasMarkup("Hello\nWorld"));
    Assert.False(MessageHtml.HasMarkup(""));
    Assert.False(MessageHtml.HasMarkup("2 < 3 and 4 > 1"));
  }
}


public class MailBodyKindTests {
  [Fact]
  public void DefaultView_UsesDetectedPart() {
    Assert.Equal(MailBodyKind.Html, MailBodyKind.DefaultView("<p>Hi</p>", "Hi", MailBodyKind.Html));
    Assert.Equal(MailBodyKind.Text, MailBodyKind.DefaultView("", "Hello", MailBodyKind.Html));
    Assert.Equal(MailBodyKind.Html, MailBodyKind.DefaultView("<p>Hi</p>", "", MailBodyKind.Text));
    Assert.Equal(MailBodyKind.Text, MailBodyKind.DefaultView("<p>Hi</p>", "Hello", MailBodyKind.Text));
  }
}


public class MailLayoutTests {
  [Fact]
  public void Normalize_WideAliases() {
    Assert.Equal(MailLayout.Wide, MailLayout.Normalize("outlook"));
    Assert.Equal(MailLayout.Wide, MailLayout.Normalize("columns"));
    Assert.Equal(MailLayout.Stacked, MailLayout.Normalize(""));
    Assert.True(MailLayout.IsWide("wide"));
    Assert.True(MailLayout.IsStacked("stacked"));
  }
}


public class MailWhenTests {
  [Fact]
  public void Line_IncludesLocalDateAndTime() {
    var date = new DateTimeOffset(2026, 9, 13, 10, 42, 0, TimeSpan.Zero);
    Assert.Equal(date.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture), MailWhen.Line(date));
    Assert.Equal("", MailWhen.Line(DateTimeOffset.MinValue));
  }
}


public class MailIdTests {
  [Fact]
  public void Parent_PrefersInReplyToThenLastReferences() {
    Assert.Equal("a@b", MailId.Parent("<a@b>", "<root@x> <a@b>"));
    Assert.Equal("later@x", MailId.Parent(null, "<root@x> <later@x>"));
    Assert.Equal("orig@studio.it", MailId.Parent("orig@studio.it", ""));
  }

  [Fact]
  public void ThreadLine_AppendsMessageId() {
    Assert.Equal("<one@x> <two@x>", MailId.ThreadLine("<one@x>", "two@x"));
  }
}


public class MailChainTests {
  [Fact]
  public void Order_IndentsRepliesAndSortsByLatest() {
    var root = Item(1, "root@x", "", new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero));
    var reply = Item(2, "reply@x", "root@x", new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero));
    var older = Item(3, "old@x", "", new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero));
    var links = MailChain.Order([older, root, reply], true);
    Assert.Equal(new uint[] { 1, 2, 3 }, links.Select(l => l.Uid).ToArray());
    Assert.Equal(0, links[0].Depth);
    Assert.Equal(2, links[0].Size);
    Assert.Equal(1, links[1].Depth);
    Assert.Equal(0, links[2].Depth);
  }

  [Fact]
  public void Order_GroupsSiblingsWhenParentIsMissing() {
    var acc = Item(10, "acc@gestore", "orig@studio.it", new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
    var del = Item(11, "del@gestore", "orig@studio.it", new DateTimeOffset(2026, 3, 1, 9, 5, 0, TimeSpan.Zero));
    var other = Item(12, "other@x", "", new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero));
    var links = MailChain.Order([other, acc, del], true);
    Assert.Equal(new uint[] { 10, 11, 12 }, links.Select(l => l.Uid).ToArray());
    Assert.Equal(2, links[0].Size);
    Assert.Equal(1, links[1].Size);
  }

  [Fact]
  public void Order_FlatKeepsDateDescending() {
    var a = Item(1, "a", "", new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));
    var b = Item(2, "b", "", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    var links = MailChain.Order([b, a], false);
    Assert.Equal(new uint[] { 1, 2 }, links.Select(l => l.Uid).ToArray());
    Assert.All(links, l => Assert.Equal(0, l.Depth));
  }

  [Fact]
  public void ReverseThreads_KeepsIndentInsideThread() {
    var root = Item(1, "root@x", "", new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero));
    var reply = Item(2, "reply@x", "root@x", new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero));
    var older = Item(3, "old@x", "", new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero));
    var links = MailChain.Order([older, root, reply], true);
    var reversed = MailListOrder.ReverseThreads(links);
    Assert.Equal(new uint[] { 3, 1, 2 }, reversed.Select(l => l.Uid).ToArray());
    Assert.Equal(0, reversed[1].Depth);
    Assert.Equal(1, reversed[2].Depth);
  }

  [Fact]
  public void ReorderThreads_SortsRootsKeepsChildren() {
    var root = Item(1, "root@x", "", new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero));
    var reply = Item(2, "reply@x", "root@x", new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero));
    var older = Item(3, "old@x", "", new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero));
    var links = MailChain.Order([older, root, reply], true);
    var ordered = MailListOrder.ReorderThreads(links, (a, b) => a.CompareTo(b));
    Assert.Equal(new uint[] { 1, 2, 3 }, ordered.Select(l => l.Uid).ToArray());
  }

  private static MailChainItem Item(uint uid, string id, string parent, DateTimeOffset date) =>
    new() { Uid = uid, MessageId = id, InReplyTo = parent, Date = date };
}


public class MailFolderStatsTests {
  [Fact]
  public void Line_ShowsItemsAndUnread() {
    Assert.Equal("Inbox  ·  0 items", MailFolderStats.Line("Inbox", 0, 0));
    Assert.Equal("Inbox  ·  1 item", MailFolderStats.Line("Inbox", 1, 0));
    Assert.Equal("Inbox  ·  42 items, 7 unread", MailFolderStats.Line("Inbox", 42, 7));
    Assert.Equal("Sent  ·  8 shown of 100 items, 3 unread", MailFolderStats.Line("Sent", 100, 3, 8));
  }
}


public class MailIndexProgressTests {
  [Fact]
  public void Line_EmptyWhenNothingToIndex() {
    Assert.Equal("", MailIndexProgress.Line(0, 0));
  }

  [Fact]
  public void Line_ShowsDoneOfTotal() {
    Assert.Equal("Indexing 12 / 400", MailIndexProgress.Line(12, 400));
    Assert.Equal("Indexing 400 / 400", MailIndexProgress.Line(500, 400));
  }

  [Fact]
  public void Line_IncludesFolderName() {
    Assert.Equal("Indexing Inbox · 12 / 400", MailIndexProgress.Line(12, 400, "Inbox"));
    Assert.Equal("Indexing Sent…", MailIndexProgress.Line(0, 0, "Sent"));
  }

  [Fact]
  public void Left_MatchesMeaningStyle() {
    Assert.Equal("Indexing…", MailIndexProgress.Left(0));
    Assert.Equal("Indexing 12 left", MailIndexProgress.Left(12));
    Assert.Equal("Indexing meaning 12 left", MailIndexProgress.MeaningLeft(12));
    Assert.Equal("Meaning index ready (CPU)", MailIndexProgress.MeaningReady("CPU", 0));
    Assert.Equal("Meaning index ready (GPU, 40)", MailIndexProgress.MeaningReady("GPU", 40));
  }
}


public class MailboxAccountTests {
  [Fact]
  public void Label_PrefersDisplayName() {
    var box = new MailboxAccount { DisplayName = "Studio", Address = "a@b.it" };
    Assert.Equal("Studio", box.Label);
  }
}


public class MailCertifiedKindTests {
  [Fact]
  public void ForProvider_PresetsForceKind_ImapKeepsChoice() {
    Assert.Equal(MailCertifiedKind.Pec, MailCertifiedKind.ForProvider(MailProvider.Aruba));
    Assert.Equal(MailCertifiedKind.Rem, MailCertifiedKind.ForProvider(MailProvider.Intesi));
    Assert.Equal(MailCertifiedKind.Ordinary, MailCertifiedKind.ForProvider(MailProvider.Gmail, MailCertifiedKind.Pec));
    Assert.Equal(MailCertifiedKind.Pec, MailCertifiedKind.ForProvider(MailProvider.Imap, MailCertifiedKind.Pec));
    Assert.Equal(MailCertifiedKind.Ordinary, MailCertifiedKind.ForProvider(MailProvider.Imap, ""));
    Assert.True(MailCertifiedKind.TracksReceipts(MailCertifiedKind.Pec));
    Assert.False(MailCertifiedKind.TracksReceipts(MailCertifiedKind.Ordinary));
  }

  [Fact]
  public void Apply_WritesCertifiedKind() {
    var aruba = new MailboxAccount { Provider = MailProvider.Aruba };
    MailProvider.Apply(aruba);
    Assert.Equal(MailCertifiedKind.Pec, aruba.CertifiedKind);
    Assert.True(aruba.TracksCertifiedReceipts);

    var custom = new MailboxAccount {
      Provider = MailProvider.Imap,
      CertifiedKind = MailCertifiedKind.Pec,
      ImapHost = "mail.studio.it"
    };
    MailProvider.Apply(custom);
    Assert.Equal(MailCertifiedKind.Pec, custom.CertifiedKind);
    Assert.Equal("mail.studio.it", custom.ImapHost);

    var gmail = new MailboxAccount { Provider = MailProvider.Gmail, CertifiedKind = MailCertifiedKind.Pec };
    MailProvider.Apply(gmail);
    Assert.Equal(MailCertifiedKind.Ordinary, gmail.CertifiedKind);
    Assert.False(gmail.TracksCertifiedReceipts);
  }
}


public class MailFolderRoleTests {
  [Fact]
  public void InboxAndItalianAliases_SortFirst() {
    Assert.Equal("inbox", MailFolderRole.Kind("INBOX", "INBOX"));
    Assert.Equal("inbox", MailFolderRole.Kind("Posta in arrivo", "INBOX"));
    Assert.Equal("inbox", MailFolderRole.Kind("Boîte de réception", "INBOX"));
    Assert.Equal("inbox", MailFolderRole.Kind("Posteingang", "INBOX"));
    Assert.Equal("inbox", MailFolderRole.Kind("Bandeja de entrada", "INBOX"));
    Assert.Equal(0, MailFolderRole.SortKey("Inbox", "INBOX"));
  }

  [Fact]
  public void SentAndTrash_UseLeafName() {
    Assert.Equal("sent", MailFolderRole.Kind("Sent", "INBOX.Sent"));
    Assert.Equal("trash", MailFolderRole.Kind("Cestino", "INBOX.Trash"));
    Assert.Equal("receipts", MailFolderRole.Kind("Ricevute", "INBOX.Ricevute"));
    Assert.True(MailFolderRole.SortKey("Sent", "INBOX.Sent") < MailFolderRole.SortKey("Trash", "INBOX.Trash"));
  }

  [Fact]
  public void OrdinaryFolder_HasNoSpecialKind() {
    Assert.Equal("", MailFolderRole.Kind("Clients", "INBOX.Clients"));
    Assert.Equal(100, MailFolderRole.SortKey("Clients", "INBOX.Clients"));
    Assert.True(MailFolderRole.IsCustom("Clients", "INBOX.Clients"));
    Assert.False(MailFolderRole.IsCustom("INBOX", "INBOX"));
    Assert.False(MailFolderRole.IsCustom("Cestino", "INBOX.Trash"));
    Assert.False(MailFolderRole.IsCustom("All Mail", "[Gmail]/All Mail"));
  }

  [Fact]
  public void GmailTrash_IsTrash() {
    Assert.Equal("trash", MailFolderRole.Kind("Trash", "[Gmail]/Trash"));
    Assert.Equal("trash", MailFolderRole.Kind("Cestino", "[Gmail]/Cestino"));
    Assert.Equal("trash", MailFolderRole.Kind("Bin", "[Gmail]/Bin"));
  }

  [Fact]
  public void GmailSentMail_IsSent() {
    Assert.Equal("sent", MailFolderRole.Kind("Sent Mail", "[Gmail]/Sent Mail"));
    Assert.Equal("Sent", MailFolderRole.DisplayName("Sent Mail", "[Gmail]/Sent Mail"));
  }

  [Fact]
  public void GmailVirtualFolders_AreHidden() {
    Assert.True(MailFolderRole.IsHidden("[Gmail]", "[Gmail]"));
    Assert.True(MailFolderRole.IsHidden("All Mail", "[Gmail]/All Mail"));
    Assert.True(MailFolderRole.IsHidden("Starred", "[Gmail]/Starred"));
    Assert.True(MailFolderRole.IsHidden("Important", "[Gmail]/Important"));
    Assert.False(MailFolderRole.IsHidden("Sent Mail", "[Gmail]/Sent Mail"));
  }

  [Fact]
  public void GmailSpecial_PrefersCanonicalPath() {
    Assert.True(MailFolderRole.PreferOver("[Gmail]/Sent Mail", "Sent"));
    Assert.False(MailFolderRole.PreferOver("Sent", "[Gmail]/Sent Mail"));
  }
}


public class UiLanguageTests {
  [Fact]
  public void Normalize_MapsPrefixesAndUnknownToEnglish() {
    Assert.Equal(UiLanguage.It, UiLanguage.Normalize("it-IT"));
    Assert.Equal(UiLanguage.En, UiLanguage.Normalize("en-US"));
    Assert.Equal(UiLanguage.Fr, UiLanguage.Normalize("fr-FR"));
    Assert.Equal(UiLanguage.De, UiLanguage.Normalize("de"));
    Assert.Equal(UiLanguage.Es, UiLanguage.Normalize("es-ES"));
    Assert.Equal(UiLanguage.En, UiLanguage.Normalize(""));
    Assert.Equal(UiLanguage.En, UiLanguage.Normalize("pl"));
    Assert.Equal("Italiano", UiLanguage.Title("it"));
    Assert.Equal("English", UiLanguage.Title("en"));
    Assert.Equal("Français", UiLanguage.Title("fr"));
    Assert.Equal("Deutsch", UiLanguage.Title("de"));
    Assert.Equal("Español", UiLanguage.Title("es"));
    Assert.Equal(5, UiLanguage.All.Count);
  }
}


public class UiCopyTests {
  [Fact]
  public void For_Italian_UsesItalianFolderNames() {
    var copy = UiCopy.For("it");
    Assert.Equal("Posta in arrivo", copy.Inbox);
    Assert.Equal("Inviata", copy.Sent);
    Assert.Equal("Bozze", copy.Drafts);
    Assert.Equal("Cestino", copy.Trash);
    Assert.Equal("Scarica messaggi", copy.GetMessages);
  }

  [Fact]
  public void For_FrenchGermanSpanish_UsesLocalizedFolderNames() {
    Assert.Equal("Boîte de réception", UiCopy.For("fr").Inbox);
    Assert.Equal("Récupérer les messages", UiCopy.For("fr").GetMessages);
    Assert.Equal("Posteingang", UiCopy.For("de").Inbox);
    Assert.Equal("Nachrichten abrufen", UiCopy.For("de").GetMessages);
    Assert.Equal("Bandeja de entrada", UiCopy.For("es").Inbox);
    Assert.Equal("Obtener mensajes", UiCopy.For("es").GetMessages);
  }

  [Fact]
  public void For_Unknown_FallsBackToEnglish() {
    Assert.Equal("Inbox", UiCopy.For("en").Inbox);
    Assert.Equal("Inbox", UiCopy.For("pl").Inbox);
    Assert.False(string.IsNullOrWhiteSpace(UiCopy.For("en").About));
    Assert.Contains("Informazioni", UiCopy.For("it").About, StringComparison.Ordinal);
    Assert.Equal("Credits", UiCopy.For("en").AboutCredits);
    Assert.Equal("Crediti", UiCopy.For("it").AboutCredits);
    Assert.Equal("Crédits", UiCopy.For("fr").AboutCredits);
    Assert.Equal("Mitwirkende", UiCopy.For("de").AboutCredits);
    Assert.Equal("Créditos", UiCopy.For("es").AboutCredits);
  }
}


public class FeatureCatalogTests {
  [Fact]
  public void Titles_CoverAllUiLanguages() {
    foreach (var feature in FeatureCatalog.All) {
      foreach (var language in UiLanguage.All)
        Assert.False(string.IsNullOrWhiteSpace(feature.TitleFor(language)));
    }

    Assert.Equal("Italie", FeatureCatalog.RegionTitle(AppRegion.Italy, UiLanguage.Fr));
    Assert.Equal("Deutschland", FeatureCatalog.RegionTitle(AppRegion.Germany, UiLanguage.De));
    Assert.Equal("España", FeatureCatalog.RegionTitle(AppRegion.Spain, UiLanguage.Es));
  }
}


public class MailPriorityTests {
  [Fact]
  public void XPriority_MapsRanks() {
    Assert.Equal(MailPriority.High, MailPriority.FromHeaders("1 (Highest)", null, null));
    Assert.Equal(MailPriority.High, MailPriority.FromHeaders("2", null, null));
    Assert.Equal(MailPriority.Normal, MailPriority.FromHeaders("3", null, null));
    Assert.Equal(MailPriority.Low, MailPriority.FromHeaders("5 (Lowest)", null, null));
  }

  [Fact]
  public void ImportanceAndKeywords_WinInOrder() {
    Assert.Equal(MailPriority.High, MailPriority.FromHeaders(null, "high", null));
    Assert.Equal(MailPriority.Low, MailPriority.FromHeaders(null, null, "non-urgent"));
    Assert.Equal(
      MailPriority.High,
      MailPriority.Resolve([MailPriority.KeywordHigh], "5", "low", null));
    Assert.Equal("!", MailPriority.Mark(MailPriority.High));
    Assert.Equal("↓", MailPriority.Mark(MailPriority.Low));
    Assert.Equal("", MailPriority.Mark(MailPriority.Normal));
    Assert.Equal("High priority", MailPriority.Label(MailPriority.High));
    Assert.Equal("Low priority", MailPriority.Label(MailPriority.Low));
    Assert.Equal("Normal priority", MailPriority.Label(MailPriority.Normal));
  }
}


public class FileSecretStoreTests {
  [Fact]
  public void PutGetRoundTrip() {
    var path = Path.Combine(Path.GetTempPath(), "postclient-secrets-" + Guid.NewGuid().ToString("N") + ".bin");
    try {
      var store = new FileSecretStore(path);
      Assert.True(store.Put("mailbox:1", "secret").IsSuccess);
      var got = store.Get("mailbox:1");
      Assert.True(got.IsSuccess);
      Assert.Equal("secret", got.Value);
    }
    finally {
      if (File.Exists(path))
        File.Delete(path);
    }
  }
}


public class ConfigurationTests {
  [Fact]
  public void EnsureDefaults_FillsPortsAndIds() {
    var configuration = new Configuration {
      Mailboxes = [new MailboxAccount { Id = "", ImapPort = 0, SmtpPort = 0 }]
    };
    configuration.EnsureDefaults();
    Assert.False(string.IsNullOrWhiteSpace(configuration.Mailboxes[0].Id));
    Assert.Equal(993, configuration.Mailboxes[0].ImapPort);
    Assert.Equal(465, configuration.Mailboxes[0].SmtpPort);
    Assert.Equal(MailProtocol.Imap, configuration.Mailboxes[0].IncomingProtocol);
    Assert.Equal(MailSecurity.Ssl, configuration.Mailboxes[0].IncomingSecurity);
    Assert.Equal(MailCertifiedKind.Ordinary, configuration.Mailboxes[0].CertifiedKind);
  }

  [Fact]
  public void EnsureDefaults_MigratesPecPresetKind() {
    var configuration = new Configuration {
      Mailboxes = [
        new MailboxAccount { Provider = MailProvider.Aruba, CertifiedKind = "" },
        new MailboxAccount { Provider = MailProvider.Intesi, CertifiedKind = "" },
        new MailboxAccount {
          Provider = MailProvider.Imap,
          CertifiedKind = MailCertifiedKind.Pec
        }
      ]
    };
    configuration.EnsureDefaults();
    Assert.Equal(MailCertifiedKind.Pec, configuration.Mailboxes[0].CertifiedKind);
    Assert.Equal(MailCertifiedKind.Rem, configuration.Mailboxes[1].CertifiedKind);
    Assert.Equal(MailCertifiedKind.Pec, configuration.Mailboxes[2].CertifiedKind);
  }

  [Fact]
  public void EnsureDefaults_Pop3StartTlsPorts() {
    var configuration = new Configuration {
      Mailboxes = [
        new MailboxAccount {
          IncomingProtocol = "pop3",
          IncomingSecurity = "starttls",
          SmtpSecurity = "starttls",
          ImapPort = 0,
          SmtpPort = 0
        }
      ]
    };
    configuration.EnsureDefaults();
    Assert.Equal(MailProtocol.Pop3, configuration.Mailboxes[0].IncomingProtocol);
    Assert.Equal(110, configuration.Mailboxes[0].ImapPort);
    Assert.Equal(587, configuration.Mailboxes[0].SmtpPort);
    Assert.False(configuration.Mailboxes[0].ImapSsl);
  }

  [Fact]
  public void EnsureDefaults_NormalizesLanguage() {
    var configuration = new Configuration { Language = "it-IT" };
    configuration.EnsureDefaults();
    Assert.Equal(UiLanguage.It, configuration.Language);

    var detected = new Configuration { Language = "" };
    detected.EnsureDefaults();
    Assert.Contains(detected.Language, UiLanguage.All);
  }
}


public class ConfigurationFileServiceTests {
  [Fact]
  public void SaveReload_KeepsMailbox() {
    var dir = Path.Combine(Path.GetTempPath(), "postclient-cfg-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    var path = Path.Combine(dir, "settings.json");
    try {
      var files = new ConfigurationFileService(path);
      var configuration = files.Current;
      configuration.Mailboxes.Add(new MailboxAccount {
        DisplayName = "Studio",
        Address = "a@b.it",
        ImapHost = "imap.example.it",
        CertifiedKind = MailCertifiedKind.Pec
      });
      files.Save(configuration);
      var reloaded = new ConfigurationFileService(path).Current;
      Assert.Single(reloaded.Mailboxes);
      Assert.Equal("a@b.it", reloaded.Mailboxes[0].Address);
      Assert.Equal("imap.example.it", reloaded.Mailboxes[0].ImapHost);
      Assert.Equal(MailCertifiedKind.Pec, reloaded.Mailboxes[0].CertifiedKind);
    }
    finally {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void SaveReload_KeepsLayout() {
    var dir = Path.Combine(Path.GetTempPath(), "postclient-cfg-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    var path = Path.Combine(dir, "settings.json");
    try {
      var files = new ConfigurationFileService(path);
      var configuration = files.Current;
      configuration.ReadingLayout = MailLayout.Wide;
      configuration.Layout.WindowWidth = 1600;
      configuration.Layout.WindowHeight = 900;
      configuration.Layout.WindowX = 40;
      configuration.Layout.WindowY = 80;
      configuration.Layout.WindowState = "Maximized";
      configuration.Layout.FolderWidth = 260;
      configuration.Layout.ListWidth = 400;
      configuration.Layout.ListHeight = 280;
      configuration.Layout.MessageFilter = "invoice";
      configuration.Layout.ColumnWidths["Subject"] = 320;
      configuration.Layout.ColumnWidths["From"] = 180;
      configuration.Layout.ColumnSort = new SavedColumnSort {
        Header = "Date",
        Direction = "Descending"
      };
      configuration.Layout.ColumnOrder = ["From", "Subject", "Date"];
      files.Save(configuration);

      var reloaded = new ConfigurationFileService(path).Current;
      Assert.Equal(MailLayout.Wide, reloaded.ReadingLayout);
      Assert.Equal(1600, reloaded.Layout.WindowWidth);
      Assert.Equal(900, reloaded.Layout.WindowHeight);
      Assert.Equal(40, reloaded.Layout.WindowX);
      Assert.Equal(80, reloaded.Layout.WindowY);
      Assert.Equal("Maximized", reloaded.Layout.WindowState);
      Assert.Equal(260, reloaded.Layout.FolderWidth);
      Assert.Equal(400, reloaded.Layout.ListWidth);
      Assert.Equal(280, reloaded.Layout.ListHeight);
      Assert.Equal("invoice", reloaded.Layout.MessageFilter);
      Assert.Equal(320, reloaded.Layout.ColumnWidths["Subject"]);
      Assert.Equal(180, reloaded.Layout.ColumnWidths["From"]);
      Assert.Equal("Date", reloaded.Layout.ColumnSort?.Header);
      Assert.Equal("Descending", reloaded.Layout.ColumnSort?.Direction);
      Assert.Equal(["From", "Subject", "Date"], reloaded.Layout.ColumnOrder);
    }
    finally {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void SaveReload_KeepsFeatures() {
    var dir = Path.Combine(Path.GetTempPath(), "postclient-cfg-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    var path = Path.Combine(dir, "settings.json");
    try {
      var files = new ConfigurationFileService(path);
      var configuration = files.Current;
      configuration.Features.Set(AppFeature.FrEvidence, true);
      configuration.Features.Set(AppFeature.FatturaPa, false);
      files.Save(configuration);

      var reloaded = new ConfigurationFileService(path).Current;
      Assert.True(reloaded.Features.IsEnabled(AppFeature.FrEvidence));
      Assert.False(reloaded.Features.IsEnabled(AppFeature.FatturaPa));
      Assert.True(reloaded.Features.IsEnabled(AppFeature.PecEnvelope));
    }
    finally {
      Directory.Delete(dir, true);
    }
  }
}


public class FeatureSettingsTests {
  [Fact]
  public void EmptySettings_UsesCatalogDefaults() {
    var features = new FeatureSettings();
    Assert.True(features.IsEnabled(AppFeature.PecEnvelope));
    Assert.True(features.IsEnabled(AppFeature.FatturaPa));
    Assert.True(features.IsEnabled(AppFeature.RemEvidence));
    Assert.False(features.IsEnabled(AppFeature.FrEvidence));
    Assert.False(features.IsEnabled(AppFeature.DeEvidence));
    Assert.False(features.IsEnabled(AppFeature.EsEvidence));
    Assert.False(features.IsEnabled(AppFeature.ChEvidence));
  }

  [Fact]
  public void RegionOff_DisablesAllInRegion() {
    var features = new FeatureSettings();
    features.SetRegion(AppRegion.Italy, false);
    Assert.False(features.IsEnabled(AppFeature.PecEnvelope));
    Assert.False(features.IsEnabled(AppFeature.PecPresets));
    Assert.False(features.IsEnabled(AppFeature.FatturaPa));
    Assert.False(features.IsEnabled(AppFeature.Fascicolo));
    Assert.False(features.RegionEnabled(AppRegion.Italy));
    Assert.True(features.IsEnabled(AppFeature.RemEvidence));
  }

  [Fact]
  public void EnsureDefaults_WiresFeatureGateFromCatalog() {
    var previous = FeatureGate.Current;
    try {
      var configuration = new Configuration();
      configuration.EnsureDefaults();
      Assert.True(configuration.Features.IsEnabled(AppFeature.PecEnvelope));
      Assert.False(configuration.Features.IsEnabled(AppFeature.FrEvidence));
    }
    finally {
      FeatureGate.Use(previous);
    }
  }
}
