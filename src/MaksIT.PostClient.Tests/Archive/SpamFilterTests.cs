namespace MaksIT.PostClient.Tests.Archive;


public class SpamFilterTests {
  [Fact]
  public void MarkSpam_IsOneRow_AndNotSpamRemovesIt() {
    using var store = Open(out var path);
    try {
      Seed(store, 1, "INBOX", "<a@mail>", "Spam offer");
      store.MarkSpam("box", "INBOX", 1, EmbeddingModelSpec.Id);
      store.MarkSpam("box", "INBOX", 1, EmbeddingModelSpec.Id);
      var marked = Assert.Single(store.ListSpam(EmbeddingModelSpec.Id));
      Assert.True(marked.Explicit);
      Assert.False(marked.Gone);
      store.DismissMessage("box", "INBOX", 1);
      Assert.Empty(store.ListSpam(EmbeddingModelSpec.Id));
    }
    finally {
      Delete(path);
    }
  }

  [Fact]
  public void JunkFolderAlone_DoesNotCreateExample() {
    using var store = Open(out var path);
    try {
      Seed(store, 3, "Junk", "<j@mail>", "Already junk");
      Assert.Empty(store.ListSpam(EmbeddingModelSpec.Id));
      var flags = store.SpamFlags("box", "Junk", EmbeddingModelSpec.Id);
      Assert.False(flags.ContainsKey(3));
    }
    finally {
      Delete(path);
    }
  }

  [Fact]
  public void RemoveUids_KeepsExample_DismissDropsItFromLaterScores() {
    using var store = Open(out var path);
    try {
      Seed(store, 1, "INBOX", "<a@mail>", "Spam offer");
      var spam = EmbeddingVector.Normalize([1f, 0f, 0f]);
      var near = EmbeddingVector.Normalize([1f, 0.05f, 0f]);
      var far = EmbeddingVector.Normalize([0f, 1f, 0f]);
      var spamId = StoreId(store);
      store.UpsertEmbedding(spamId, EmbeddingModelSpec.Id, spam);
      store.MarkSpam("box", "INBOX", 1, EmbeddingModelSpec.Id);
      store.RemoveUids("box", "INBOX", [1]);
      var kept = Assert.Single(store.ListSpam(EmbeddingModelSpec.Id));
      Assert.True(kept.Gone);

      Seed(store, 2, "INBOX", "<b@mail>", "Another offer");
      var nearId = StoreId(store);
      store.UpsertEmbedding(nearId, EmbeddingModelSpec.Id, near);
      store.ApplySpamEmbedding(nearId, EmbeddingModelSpec.Id, near, filterEnabled: true);
      Assert.Equal(2, store.ListSpam(EmbeddingModelSpec.Id).Count);
      var nearFlags = store.SpamFlags("box", "INBOX", EmbeddingModelSpec.Id);
      Assert.True(nearFlags[2].Score >= SpamScore.ShowAt);

      store.DismissMessage("box", "INBOX", 2);
      store.ApplySpamEmbedding(nearId, EmbeddingModelSpec.Id, near, filterEnabled: true);
      Assert.DoesNotContain(store.ListSpam(EmbeddingModelSpec.Id), row => row.Subject == "Another offer");
      Assert.False(store.SpamFlags("box", "INBOX", EmbeddingModelSpec.Id).TryGetValue(2, out var cleared) && cleared.Score > 0f);

      Seed(store, 4, "INBOX", "<d@mail>", "Far note");
      var farId = StoreId(store);
      store.UpsertEmbedding(farId, EmbeddingModelSpec.Id, far);
      store.ApplySpamEmbedding(farId, EmbeddingModelSpec.Id, far, filterEnabled: true);
      Assert.False(store.SpamFlags("box", "INBOX", EmbeddingModelSpec.Id).ContainsKey(4));
    }
    finally {
      Delete(path);
    }
  }

  [Fact]
  public void OtherModel_DoesNotScore_AndPendingVectorIsFilled() {
    using var store = Open(out var path);
    try {
      Seed(store, 1, "INBOX", "<a@mail>", "Wait for vector");
      store.MarkSpam("box", "INBOX", 1, EmbeddingModelSpec.Id);
      Assert.Equal(0, store.SpamExampleCount(EmbeddingModelSpec.Id));
      var vector = EmbeddingVector.Normalize([1f, 0f, 0f]);
      var id = StoreId(store);
      store.UpsertEmbedding(id, EmbeddingModelSpec.Id, vector);
      store.ApplySpamEmbedding(id, EmbeddingModelSpec.Id, vector, filterEnabled: false);
      Assert.Equal(1, store.SpamExampleCount(EmbeddingModelSpec.Id));

      Seed(store, 5, "INBOX", "<e@mail>", "Other model");
      store.UpsertEmbedding(StoreId(store), "other-model", vector);
      store.MarkSpam("box", "INBOX", 5, "other-model");
      Seed(store, 6, "INBOX", "<f@mail>", "Should stay");
      var candidate = StoreId(store);
      store.UpsertEmbedding(candidate, EmbeddingModelSpec.Id, vector);
      store.DismissSpam(store.ListSpam(EmbeddingModelSpec.Id).Single(row => row.Subject == "Wait for vector").Fingerprint);
      store.ApplySpamEmbedding(candidate, EmbeddingModelSpec.Id, vector, filterEnabled: true);
      Assert.False(store.SpamFlags("box", "INBOX", EmbeddingModelSpec.Id).ContainsKey(6));
    }
    finally {
      Delete(path);
    }
  }

  [Fact]
  public void MarkInOneMailbox_TeachesTheOther() {
    var root = Path.Combine(Path.GetTempPath(), "postclient-spam-all-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try {
      using var catalog = new MailArchiveCatalog(Path.Combine(root, "spam.db"));
      catalog.Open("a", Path.Combine(root, "a.db"));
      catalog.Open("b", Path.Combine(root, "b.db"));
      var vector = EmbeddingVector.Normalize([1f, 0f, 0f]);
      var near = EmbeddingVector.Normalize([1f, 0.05f, 0f]);
      catalog.UpsertBody(
        "a",
        new MailArchiveHeader {
          Uid = 1,
          Folder = "INBOX",
          Subject = "Offer",
          From = "spammer@example.test",
          Date = DateTimeOffset.Parse("2026-01-02T03:04:05Z"),
          MessageId = "<a@mail>"
        },
        Path.Combine(root, "a.eml"),
        "buy now",
        "");
      var marked = Assert.Single(catalog.PendingEmbeddings(EmbeddingModelSpec.Id, 4));
      catalog.UpsertEmbedding("a", marked.MessageId, EmbeddingModelSpec.Id, vector);
      catalog.MarkSpam("a", "INBOX", 1, EmbeddingModelSpec.Id);

      catalog.UpsertBody(
        "b",
        new MailArchiveHeader {
          Uid = 2,
          Folder = "INBOX",
          Subject = "Same offer",
          From = "spammer@example.test",
          Date = DateTimeOffset.Parse("2026-02-02T03:04:05Z"),
          MessageId = "<b@mail>"
        },
        Path.Combine(root, "b.eml"),
        "buy now too",
        "");
      var other = catalog.PendingEmbeddings(EmbeddingModelSpec.Id, 4).Single(row => row.MailboxId == "b");
      catalog.UpsertEmbedding("b", other.MessageId, EmbeddingModelSpec.Id, near);
      catalog.ApplySpamEmbedding("b", other.MessageId, EmbeddingModelSpec.Id, near, filterEnabled: true);
      var flags = catalog.SpamFlags("b", "INBOX", EmbeddingModelSpec.Id);
      Assert.True(flags[2].Score >= SpamScore.ShowAt);

      catalog.DismissSpam("a", catalog.ListSpam(EmbeddingModelSpec.Id).Single(row => row.MailboxId == "a").Example.Fingerprint);
      Assert.DoesNotContain(
        catalog.ListSpam(EmbeddingModelSpec.Id),
        row => row.MailboxId == "a");
    }
    finally {
      Directory.Delete(root, recursive: true);
    }
  }

  [Fact]
  public void Marks_StayAfterTheMailboxIsClosed() {
    var root = Path.Combine(Path.GetTempPath(), "postclient-spam-keep-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var spam = Path.Combine(root, "spam.db");
    try {
      string fingerprint;
      using (var catalog = new MailArchiveCatalog(spam)) {
        catalog.Open("a", Path.Combine(root, "a.db"));
        catalog.Open("b", Path.Combine(root, "b.db"));
        var vector = EmbeddingVector.Normalize([1f, 0f, 0f]);
        var near = EmbeddingVector.Normalize([1f, 0.05f, 0f]);
        catalog.UpsertBody(
          "a",
          new MailArchiveHeader {
            Uid = 1,
            Folder = "INBOX",
            Subject = "Offer",
            From = "spammer@example.test",
            Date = DateTimeOffset.Parse("2026-01-02T03:04:05Z"),
            MessageId = "<a@mail>"
          },
          Path.Combine(root, "a.eml"),
          "buy now",
          "");
        var marked = catalog.PendingEmbeddings(EmbeddingModelSpec.Id, 4).Single(row => row.MailboxId == "a");
        catalog.UpsertEmbedding("a", marked.MessageId, EmbeddingModelSpec.Id, vector);
        catalog.MarkSpam("a", "INBOX", 1, EmbeddingModelSpec.Id);
        catalog.UpsertBody(
          "b",
          new MailArchiveHeader {
            Uid = 2,
            Folder = "INBOX",
            Subject = "Same offer",
            From = "spammer@example.test",
            Date = DateTimeOffset.Parse("2026-02-02T03:04:05Z"),
            MessageId = "<b@mail>"
          },
          Path.Combine(root, "b.eml"),
          "buy now too",
          "");
        var other = catalog.PendingEmbeddings(EmbeddingModelSpec.Id, 4).Single(row => row.MailboxId == "b");
        catalog.UpsertEmbedding("b", other.MessageId, EmbeddingModelSpec.Id, near);
        catalog.Close("a");
        catalog.ApplySpamEmbedding("b", other.MessageId, EmbeddingModelSpec.Id, near, filterEnabled: true);
        var flags = catalog.SpamFlags("b", "INBOX", EmbeddingModelSpec.Id);
        Assert.True(flags[2].Score >= SpamScore.ShowAt);
        var lesson = Assert.Single(catalog.ListSpam(EmbeddingModelSpec.Id), row => row.Example.Subject == "Offer");
        Assert.True(lesson.Example.Gone);
        fingerprint = lesson.Example.Fingerprint;
      }

      using (var again = new MailArchiveCatalog(spam)) {
        again.Open("b", Path.Combine(root, "b.db"));
        var lesson = Assert.Single(again.ListSpam(EmbeddingModelSpec.Id), row => row.Example.Subject == "Offer");
        Assert.True(lesson.Example.Gone);
        again.DismissSpam("", fingerprint);
        Assert.DoesNotContain(
          again.ListSpam(EmbeddingModelSpec.Id),
          row => row.Example.Subject == "Offer");
      }
    }
    finally {
      try {
        Directory.Delete(root, recursive: true);
      }
      catch {
      }
    }
  }

  private static MailArchiveStore Open(out string path) {
    path = Path.Combine(Path.GetTempPath(), "postclient-spam-" + Guid.NewGuid().ToString("N") + ".db");
    return new MailArchiveStore(path);
  }

  private static void Seed(MailArchiveStore store, uint uid, string folder, string messageId, string subject) =>
    store.UpsertBody(
      "box",
      new MailArchiveHeader {
        Uid = uid,
        Folder = folder,
        Subject = subject,
        From = "spammer@example.test",
        Date = DateTimeOffset.Parse("2026-01-02T03:04:05Z"),
        MessageId = messageId
      },
      uid + ".eml",
      "body " + subject,
      "");

  private static long StoreId(MailArchiveStore store) =>
    store.PendingEmbeddings(EmbeddingModelSpec.Id, 1).FirstOrDefault().MessageId is var id && id > 0
      ? id
      : store.PendingEmbeddings("missing-model", 8).Max(row => row.MessageId);

  private static void Delete(string path) {
    try {
      File.Delete(path);
    }
    catch {
    }
  }
}
