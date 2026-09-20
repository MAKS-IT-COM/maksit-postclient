using MaksIT.PostClient.Client;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Tests;


public class FatturaPaTests {
  [Fact]
  public void TryParse_ReadsHeaderAndLines() {
    var xml = """
      <?xml version="1.0"?>
      <FatturaElettronica>
        <FatturaElettronicaHeader>
          <CedentePrestatore>
            <DatiAnagrafici>
              <Anagrafica><Denominazione>Studio Rossi</Denominazione></Anagrafica>
            </DatiAnagrafici>
          </CedentePrestatore>
          <CessionarioCommittente>
            <DatiAnagrafici>
              <Anagrafica><Denominazione>Comune di Pisa</Denominazione></Anagrafica>
            </DatiAnagrafici>
          </CessionarioCommittente>
        </FatturaElettronicaHeader>
        <FatturaElettronicaBody>
          <DatiGenerali>
            <DatiGeneraliDocumento>
              <Numero>42</Numero>
              <Data>2026-03-01</Data>
              <Divisa>EUR</Divisa>
              <ImportoTotaleDocumento>120.00</ImportoTotaleDocumento>
            </DatiGeneraliDocumento>
          </DatiGenerali>
          <DatiBeniServizi>
            <DettaglioLinee>
              <Descrizione>IMU 2025</Descrizione>
              <Quantita>1.00</Quantita>
              <PrezzoTotale>120.00</PrezzoTotale>
            </DettaglioLinee>
          </DatiBeniServizi>
        </FatturaElettronicaBody>
      </FatturaElettronica>
      """;
    var parsed = FatturaPaDocument.TryParse(System.Text.Encoding.UTF8.GetBytes(xml));
    Assert.NotNull(parsed);
    Assert.Equal("Studio Rossi", parsed.Supplier);
    Assert.Equal("Comune di Pisa", parsed.Customer);
    Assert.Equal("42", parsed.Number);
    Assert.Contains("IMU 2025", parsed.Text);
    Assert.Contains("120.00", parsed.Text);
  }

  [Fact]
  public void LooksLike_FatturaFileName() {
    Assert.True(FatturaPaDocument.LooksLike("IT012345_fattura.xml", "text/xml"));
  }
}


public class MailboxQuotaTests {
  [Fact]
  public void Line_ShowsPercentAndHost() {
    var quota = new MailboxQuota {
      Label = "Aruba",
      Host = "imaps.pec.aruba.it",
      UsedKb = 1900 * 1024,
      LimitKb = 2000 * 1024
    };
    var line = quota.Line();
    Assert.Contains("Aruba 95%", line);
    Assert.Contains("imaps.pec.aruba.it", line);
  }

  [Fact]
  public void Line_WithoutLimit_ShowsUsedAndHidesPercent() {
    var quota = new MailboxQuota {
      Label = "POP",
      Host = "pop.example.com",
      UsedKb = 2048
    };
    Assert.Null(quota.Percent);
    var line = quota.Line();
    Assert.Contains("used", line);
    Assert.DoesNotContain("%", line);
  }
}


public class MboxReaderTests {
  [Fact]
  public void Messages_SplitsOnFromSeparator() {
    var text = "From a@b Mon Sep 1 00:00:00 2026\nSubject: One\n\nHi\nFrom c@d Mon Sep 1 00:00:00 2026\nSubject: Two\n\nBye\n";
    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));
    var rows = MboxReader.Messages(stream);
    Assert.Equal(2, rows.Count);
    Assert.Contains("Subject: One", System.Text.Encoding.UTF8.GetString(rows[0]));
    Assert.Contains("Subject: Two", System.Text.Encoding.UTF8.GetString(rows[1]));
  }
}


public class MailArchiveStoreTests {
  [Fact]
  public void Search_FindsBodyAndLabel() {
    var path = Path.Combine(Path.GetTempPath(), "postclient-archive-" + Guid.NewGuid().ToString("N") + ".db");
    try {
      using var store = new MailArchiveStore(path);
      var header = new MailArchiveHeader {
        Uid = 7,
        Folder = "INBOX",
        Subject = "Avviso",
        From = "comune@pec.it",
        Date = DateTimeOffset.Parse("2025-06-01T10:00:00Z"),
        EnvelopeKind = EnvelopeKind.PecTransport,
        EnvelopeBadge = "PEC"
      };
      store.UpsertBody("box", header, "a.eml", "pagamento IMU 2025", "fattura.xml\ncodice tributo");
      store.AddLabel("box", "INBOX", 7, "Condominio X");
      var bodyHits = store.Search("box", "INBOX", "IMU");
      Assert.Single(bodyHits);
      var labelHits = store.Search("box", "INBOX", "Condominio");
      Assert.Single(labelHits);
    }
    finally {
      DeleteArchive(path);
    }
  }

  [Fact]
  public void UpsertBody_DoesNotMarkExistingUnreadAsSeen() {
    var path = Path.Combine(Path.GetTempPath(), "postclient-archive-" + Guid.NewGuid().ToString("N") + ".db");
    try {
      using var store = new MailArchiveStore(path);
      store.UpsertHeaders("box", [
        new MailArchiveHeader {
          Uid = 3,
          Folder = "INBOX",
          Subject = "Unread",
          From = "a@b.c",
          Date = DateTimeOffset.Parse("2026-01-01T10:00:00Z"),
          IsSeen = false
        }
      ]);
      store.UpsertBody(
        "box",
        new MailArchiveHeader {
          Uid = 3,
          Folder = "INBOX",
          Subject = "Unread",
          From = "a@b.c",
          Date = DateTimeOffset.Parse("2026-01-01T10:00:00Z"),
          IsSeen = true
        },
        "a.eml",
        "hello",
        "");
      var row = Assert.Single(store.ListFolder("box", "INBOX"));
      Assert.False(row.IsSeen);
    }
    finally {
      DeleteArchive(path);
    }
  }

  [Fact]
  public void UpdateFlags_SetsSeenWithoutTouchingSubject() {
    var path = Path.Combine(Path.GetTempPath(), "postclient-archive-" + Guid.NewGuid().ToString("N") + ".db");
    try {
      using var store = new MailArchiveStore(path);
      store.UpsertHeaders("box", [
        new MailArchiveHeader {
          Uid = 4,
          Folder = "INBOX",
          Subject = "Keep me",
          From = "a@b.c",
          Date = DateTimeOffset.Parse("2026-01-01T10:00:00Z"),
          IsSeen = false
        }
      ]);
      store.UpdateFlags("box", "INBOX", [(4u, true, true)]);
      var row = Assert.Single(store.ListFolder("box", "INBOX"));
      Assert.Equal("Keep me", row.Subject);
      Assert.True(row.IsSeen);
      Assert.True(row.IsFlagged);
      var counts = store.FolderCounts("box", "INBOX");
      Assert.Equal(1, counts.Total);
      Assert.Equal(0, counts.Unread);
      Assert.Equal((0, 0), store.FolderCounts("box", "Missing"));
    }
    finally {
      DeleteArchive(path);
    }
  }

  [Fact]
  public void RemoveUids_LeavesOtherMessages() {
    var path = Path.Combine(Path.GetTempPath(), "postclient-archive-" + Guid.NewGuid().ToString("N") + ".db");
    try {
      using var store = new MailArchiveStore(path);
      store.UpsertHeaders("box", [
        new MailArchiveHeader { Uid = 1, Folder = "INBOX", Subject = "Keep" },
        new MailArchiveHeader { Uid = 2, Folder = "INBOX", Subject = "Drop" }
      ]);
      store.RemoveUids("box", "INBOX", [2u]);
      var row = Assert.Single(store.ListFolder("box", "INBOX"));
      Assert.Equal(1u, row.Uid);
      Assert.Equal("Keep", row.Subject);
    }
    finally {
      DeleteArchive(path);
    }
  }

  [Fact]
  public void RemoveUids_DropsKeywordAndMeaning() {
    var path = Path.Combine(Path.GetTempPath(), "postclient-archive-" + Guid.NewGuid().ToString("N") + ".db");
    try {
      using var store = new MailArchiveStore(path);
      store.UpsertBody(
        "box",
        new MailArchiveHeader { Uid = 1, Folder = "INBOX", Subject = "IMU avviso", From = "comune@pec.it" },
        "a.eml",
        "pagamento tributo",
        "");
      var item = Assert.Single(store.PendingEmbeddings(EmbeddingModelSpec.Id, 8));
      store.UpsertEmbedding(item.MessageId, EmbeddingModelSpec.Id, Towards(1));
      Assert.Equal(1, store.EmbeddingCount(EmbeddingModelSpec.Id));
      Assert.Single(store.Search("box", "INBOX", "IMU"));
      store.RemoveUids("box", "INBOX", [1u]);
      Assert.Empty(store.Search("box", "INBOX", "IMU"));
      Assert.Equal(0, store.EmbeddingCount(EmbeddingModelSpec.Id));
      var stats = store.IndexStats(EmbeddingModelSpec.Id);
      Assert.Equal(0, stats.Messages);
      Assert.Equal(0, stats.KeywordRows);
      Assert.Equal(0, stats.MeaningRows);
      Assert.Equal(0, stats.Orphans);
    }
    finally {
      DeleteArchive(path);
    }
  }

  [Fact]
  public void RebuildKeywordIndex_RestoresSearch() {
    var path = Path.Combine(Path.GetTempPath(), "postclient-archive-" + Guid.NewGuid().ToString("N") + ".db");
    try {
      using var store = new MailArchiveStore(path);
      store.UpsertBody(
        "box",
        new MailArchiveHeader { Uid = 1, Folder = "INBOX", Subject = "IMU avviso", From = "comune@pec.it" },
        "a.eml",
        "pagamento tributo",
        "");
      Assert.Equal(1, store.RebuildKeywordIndex());
      Assert.Single(store.Search("box", "INBOX", "IMU"));
    }
    finally {
      DeleteArchive(path);
    }
  }

  [Fact]
  public void KeywordIndexDisabled_SkipsFtsUntilRebuild() {
    var path = Path.Combine(Path.GetTempPath(), "postclient-archive-" + Guid.NewGuid().ToString("N") + ".db");
    try {
      using var store = new MailArchiveStore(path);
      store.KeywordIndexEnabled = false;
      store.UpsertHeaders(
        "box",
        [new MailArchiveHeader { Uid = 1, Folder = "INBOX", Subject = "IMU avviso", From = "comune@pec.it" }]);
      Assert.Equal(0, store.IndexStats(EmbeddingModelSpec.Id).KeywordRows);
      Assert.Equal(1, store.RebuildKeywordIndex());
      Assert.Equal(1, store.IndexStats(EmbeddingModelSpec.Id).KeywordRows);
    }
    finally {
      DeleteArchive(path);
    }
  }

  [Fact]
  public void TrashRetention_UsesTrashedUtcNotReceivedDate() {
    var path = Path.Combine(Path.GetTempPath(), "postclient-archive-" + Guid.NewGuid().ToString("N") + ".db");
    try {
      using var store = new MailArchiveStore(path);
      store.UpsertHeaders(
        "box",
        [
          new MailArchiveHeader {
            Uid = 1,
            Folder = MailRetention.TrashFolder,
            Subject = "Old",
            From = "a@b.c",
            Date = DateTimeOffset.UtcNow.AddDays(-40)
          }
        ]);
      var yesterday = DateTimeOffset.UtcNow.AddDays(-1);
      Assert.Empty(store.UidsOlderThan("box", MailRetention.TrashFolder, yesterday));
      var tomorrow = DateTimeOffset.UtcNow.AddDays(1);
      Assert.Equal([1u], store.UidsOlderThan("box", MailRetention.TrashFolder, tomorrow));
    }
    finally {
      DeleteArchive(path);
    }
  }

  [Fact]
  public void ClearEmbeddings_LeavesKeywordAndPending() {
    var path = Path.Combine(Path.GetTempPath(), "postclient-archive-" + Guid.NewGuid().ToString("N") + ".db");
    try {
      using var store = new MailArchiveStore(path);
      store.UpsertBody(
        "box",
        new MailArchiveHeader { Uid = 1, Folder = "INBOX", Subject = "IMU avviso", From = "comune@pec.it" },
        "a.eml",
        "pagamento tributo",
        "");
      var item = Assert.Single(store.PendingEmbeddings(EmbeddingModelSpec.Id, 8));
      store.UpsertEmbedding(item.MessageId, EmbeddingModelSpec.Id, Towards(1));
      Assert.Equal(1, store.ClearEmbeddings());
      Assert.Equal(0, store.EmbeddingCount(EmbeddingModelSpec.Id));
      Assert.Equal(1, store.EmbeddingPendingCount(EmbeddingModelSpec.Id));
      Assert.Single(store.Search("box", "INBOX", "IMU"));
      var stats = store.SanitizeIndices(EmbeddingModelSpec.Id);
      Assert.Equal(1, stats.Messages);
      Assert.Equal(0, stats.Orphans);
    }
    finally {
      DeleteArchive(path);
    }
  }

  [Fact]
  public void MissingBodies_FindsAllFolders() {
    var path = Path.Combine(Path.GetTempPath(), "postclient-archive-" + Guid.NewGuid().ToString("N") + ".db");
    try {
      using var store = new MailArchiveStore(path);
      store.UpsertHeaders("box", [
        new MailArchiveHeader {
          Uid = 1,
          Folder = "INBOX",
          Subject = "One",
          Date = DateTimeOffset.Parse("2026-02-01T10:00:00Z")
        },
        new MailArchiveHeader {
          Uid = 2,
          Folder = "Clients",
          Subject = "Two",
          Date = DateTimeOffset.Parse("2026-03-01T10:00:00Z")
        }
      ]);
      store.UpsertBody(
        "box",
        new MailArchiveHeader { Uid = 1, Folder = "INBOX", Subject = "One" },
        "one.eml",
        "body",
        "");
      var missing = store.MissingBodies("box");
      var row = Assert.Single(missing);
      Assert.Equal("box", row.MailboxId);
      Assert.Equal("Clients", row.Folder);
      Assert.Equal(2u, row.Uid);
      Assert.Equal(1, store.MissingBodyCount(["box"]));
      Assert.Equal(1, store.MissingBodyCount());
    }
    finally {
      DeleteArchive(path);
    }
  }

  [Fact]
  public void SetSeenAndRemoveFolder_UpdateArchive() {
    var path = Path.Combine(Path.GetTempPath(), "postclient-archive-" + Guid.NewGuid().ToString("N") + ".db");
    var eml = Path.Combine(Path.GetTempPath(), "postclient-eml-" + Guid.NewGuid().ToString("N") + ".eml");
    try {
      File.WriteAllText(eml, "From: a@b.c\n\nHi");
      using var store = new MailArchiveStore(path);
      store.UpsertBody(
        "box",
        new MailArchiveHeader {
          Uid = 9,
          Folder = "Clients",
          Subject = "Bye",
          From = "a@b.c",
          Date = DateTimeOffset.Parse("2026-01-01T10:00:00Z"),
          IsSeen = false
        },
        eml,
        "hi",
        "");
      store.SetSeen("box", "Clients", true);
      Assert.True(Assert.Single(store.ListFolder("box", "Clients")).IsSeen);
      var paths = store.RemoveFolder("box", "Clients");
      Assert.Contains(eml, paths);
      Assert.Empty(store.ListFolder("box", "Clients"));
      Assert.Empty(store.MissingBodies("box"));
    }
    finally {
      if (File.Exists(eml))
        File.Delete(eml);
      DeleteArchive(path);
    }
  }

  [Fact]
  public void RewriteFolderPrefix_MovesFolderAndChild() {
    var path = Path.Combine(Path.GetTempPath(), "postclient-archive-" + Guid.NewGuid().ToString("N") + ".db");
    try {
      using var store = new MailArchiveStore(path);
      store.UpsertHeaders("box", [
        new MailArchiveHeader {
          Uid = 1,
          Folder = "Project",
          Subject = "Root",
          From = "a@b.c",
          Date = DateTimeOffset.Parse("2026-01-01T10:00:00Z")
        },
        new MailArchiveHeader {
          Uid = 2,
          Folder = "Project/2024",
          Subject = "Child",
          From = "a@b.c",
          Date = DateTimeOffset.Parse("2026-01-02T10:00:00Z")
        }
      ]);
      Assert.Equal(2, store.RewriteFolderPrefix("box", "Project", "Inbox/Project"));
      Assert.Equal("Root", Assert.Single(store.ListFolder("box", "Inbox/Project")).Subject);
      Assert.Equal("Child", Assert.Single(store.ListFolder("box", "Inbox/Project/2024")).Subject);
      Assert.Empty(store.ListFolder("box", "Project"));
    }
    finally {
      DeleteArchive(path);
    }
  }

  [Fact]
  public void Search_MergesSemanticHits() {
    var path = Path.Combine(Path.GetTempPath(), "postclient-archive-" + Guid.NewGuid().ToString("N") + ".db");
    try {
      using var store = new MailArchiveStore(path);
      store.UpsertBody(
        "box",
        new MailArchiveHeader { Uid = 1, Folder = "INBOX", Subject = "IMU avviso", From = "comune@pec.it" },
        "a.eml",
        "pagamento tributo comunale",
        "");
      store.UpsertBody(
        "box",
        new MailArchiveHeader { Uid = 2, Folder = "INBOX", Subject = "Vacanze", From = "amico@mail.it" },
        "b.eml",
        "spiaggia e mare",
        "");
      var pending = store.PendingEmbeddings(EmbeddingModelSpec.Id, 8);
      Assert.Equal(2, pending.Count);
      var imu = Assert.Single(pending, p => p.Subject.Contains("IMU", StringComparison.Ordinal));
      var other = Assert.Single(pending, p => p.Subject.Contains("Vacanze", StringComparison.Ordinal));
      store.UpsertEmbedding(imu.MessageId, EmbeddingModelSpec.Id, Towards(1));
      store.UpsertEmbedding(other.MessageId, EmbeddingModelSpec.Id, Towards(-1));
      var hits = store.Search("box", "INBOX", "tassa sulla casa", Towards(1));
      var row = Assert.Single(hits);
      Assert.Equal(1u, row.Uid);
    }
    finally {
      DeleteArchive(path);
    }
  }

  private static float[] Towards(float sign) {
    var values = new float[EmbeddingModelSpec.StoredDimensions];
    values[0] = sign;
    return EmbeddingVector.Normalize(values);
  }

  private static void DeleteArchive(string path) {
    if (File.Exists(path))
      File.Delete(path);
    foreach (var extra in new[] { path + "-wal", path + "-shm" }) {
      if (File.Exists(extra))
        File.Delete(extra);
    }
  }
}


public class AttachmentTextTests {
  [Fact]
  public void Extract_ReadsXml() {
    var xml = "<FatturaElettronica><Numero>1</Numero></FatturaElettronica>"u8.ToArray();
    var text = AttachmentText.Extract([
      new MailFileAttachment {
        Name = "IT012_fattura.xml",
        Bytes = xml,
        ContentType = "application/xml"
      }
    ]);
    Assert.Contains("Numero", text);
  }
}


public class MailFileAttachmentTests {
  [Fact]
  public void Glyph_MatchesType() {
    Assert.Equal("📄", File("avviso.pdf", "application/pdf").Glyph);
    Assert.Equal("🖼", File("scan.png", "image/png").Glyph);
    Assert.Equal("📋", File("IT012.xml", "application/xml").Glyph);
    Assert.Equal("📦", File("fascicolo.zip", "application/zip").Glyph);
    Assert.Equal("✉", File("postacert.eml", "message/rfc822").Glyph);
    Assert.Equal("📎", File("notes.bin", "application/octet-stream").Glyph);
    Assert.Equal("📫", File("casella.pst", "application/octet-stream").Glyph);
    Assert.True(File("backup.ost", "application/vnd.ms-outlook").IsPst);
    Assert.False(File("notes.bin", "application/octet-stream").IsPst);
    Assert.False(File("notes.bin", "application/octet-stream").IsFatturaPa);
    var xml = """
      <?xml version="1.0"?><FatturaElettronica><FatturaElettronicaHeader /></FatturaElettronica>
      """u8.ToArray();
    Assert.True(new MailFileAttachment { Name = "IT012_fattura.xml", Bytes = xml, ContentType = "text/xml" }.IsFatturaPa);
  }

  private static MailFileAttachment File(string name, string type) =>
    new() { Name = name, Bytes = [1], ContentType = type };
}


public class AttachmentZipTests {
  [Fact]
  public void FromFiles_WritesEntriesAndDedupsNames() {
    var zip = AttachmentZip.FromFiles([
      new MailFileAttachment { Name = "../secret/fattura.xml", Bytes = "one"u8.ToArray() },
      new MailFileAttachment { Name = "fattura.xml", Bytes = "two"u8.ToArray() },
      new MailFileAttachment { Name = "", Bytes = "empty-name"u8.ToArray() }
    ]);
    using var stream = new MemoryStream(zip);
    using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
    Assert.Equal(3, archive.Entries.Count);
    Assert.Contains(archive.Entries, e => e.Name == "fattura.xml");
    Assert.Contains(archive.Entries, e => e.Name == "fattura (2).xml");
    Assert.Contains(archive.Entries, e => e.Name == "attachment");
    Assert.DoesNotContain(archive.Entries, e => e.FullName.Contains("..", StringComparison.Ordinal));
  }

  [Fact]
  public void SuggestedName_UsesSubject() {
    Assert.Equal("Avviso IMU-attachments.zip", AttachmentZip.SuggestedName("Avviso IMU"));
    Assert.Equal("message-attachments.zip", AttachmentZip.SuggestedName("   "));
  }

  [Fact]
  public void FileName_AddsZipExtension() {
    Assert.Equal("fatture.zip", AttachmentZip.FileName("fatture"));
    Assert.Equal("a.zip", AttachmentZip.FileName("a.zip"));
  }

  [Fact]
  public void FromFiles_PasswordRoundTrip() {
    var zip = AttachmentZip.FromFiles(
      [new MailFileAttachment { Name = "note.txt", Bytes = "hello"u8.ToArray() }],
      "secret");
    using var file = new ICSharpCode.SharpZipLib.Zip.ZipFile(new MemoryStream(zip), false);
    file.Password = "secret";
    var entry = file.GetEntry("note.txt");
    Assert.NotNull(entry);
    using var stream = file.GetInputStream(entry);
    using var reader = new StreamReader(stream);
    Assert.Equal("hello", reader.ReadToEnd());
  }
}


public class MailArchiveMapTests {
  [Fact]
  public void ToHeader_RestoresPecDeliveryMarkFromTipo() {
    var header = MailArchiveMap.ToHeader(new MailArchiveHeader {
      Uid = 9,
      Folder = "INBOX",
      EnvelopeKind = EnvelopeKind.PecReceipt,
      EnvelopeTipo = "avvenuta-consegna"
    });
    Assert.Equal(ReceiptStatus.Delivered, header.DeliveryStatus);
  }
}


public class ArchiveFilesTests {
  [Fact]
  public void Hint_SaysArchiveIsOnThisPc() {
    Assert.Contains("only on this PC", ArchiveFiles.Hint(), StringComparison.OrdinalIgnoreCase);
  }
}
