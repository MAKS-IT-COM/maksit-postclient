namespace MaksIT.PostClient.Shared.Archive;


public sealed partial class MailArchiveStore {
  public void MarkSpam(string mailboxId, string folder, uint uid, string modelId) {
    lock (_gate)
      MarkSpamUnlocked(mailboxId, folder, uid, modelId);
  }

  public void DismissMessage(string mailboxId, string folder, uint uid) {
    lock (_gate) {
      var item = Identity(mailboxId, folder, uid);
      if (item is null)
        return;
      DismissUnlocked(item.Fingerprint);
    }
  }

  public void DismissSpam(string fingerprint) {
    if (string.IsNullOrWhiteSpace(fingerprint))
      return;
    lock (_gate)
      DismissUnlocked(fingerprint);
  }

  public SpamExampleInfo? LocateSpam(string fingerprint) {
    if (string.IsNullOrWhiteSpace(fingerprint))
      return null;
    lock (_gate) {
      var hit = FindUnlocked(fingerprint);
      return hit is null
        ? null
        : new SpamExampleInfo {
          Fingerprint = fingerprint,
          Found = true,
          Folder = hit.Folder,
          Uid = hit.Uid
        };
    }
  }

  public IReadOnlyList<SpamExampleInfo> ListSpam(string modelId) {
    lock (_gate) {
      var live = LiveFingerprints();
      using var cmd = _db.CreateCommand();
      cmd.CommandText = """
        SELECT fingerprint, from_addr, subject, date_utc, explicit
        FROM spam_examples
        WHERE model_id = $model
        ORDER BY marked_utc DESC;
        """;
      cmd.Parameters.AddWithValue("$model", modelId);
      var rows = new List<SpamExampleInfo>();
      using var reader = cmd.ExecuteReader();
      while (reader.Read()) {
        var fingerprint = reader.GetString(0);
        var hit = live.TryGetValue(fingerprint, out var found) ? found : null;
        rows.Add(new SpamExampleInfo {
          Fingerprint = fingerprint,
          From = reader.IsDBNull(1) ? "" : reader.GetString(1),
          Subject = reader.IsDBNull(2) ? "" : reader.GetString(2),
          DateUtc = reader.IsDBNull(3) ? "" : reader.GetString(3),
          Explicit = reader.GetInt64(4) != 0,
          Gone = hit is null,
          Found = hit is not null,
          Folder = hit?.Folder ?? "",
          Uid = hit?.Uid ?? 0
        });
      }

      return rows;
    }
  }

  public IReadOnlyDictionary<uint, SpamFlag> SpamFlags(
    string mailboxId,
    string folder,
    string modelId,
    IReadOnlyDictionary<string, bool>? shared = null) {
    lock (_gate) {
      var examples = ExampleSet(modelId);
      var rows = new Dictionary<uint, SpamFlag>();
      foreach (var item in Identities(mailboxId, folder)) {
        var localExplicit = false;
        var local = examples.TryGetValue(item.Fingerprint, out var example);
        if (local)
          localExplicit = example.Explicit;
        var sharedExplicit = false;
        var fromShared = shared?.TryGetValue(item.Fingerprint, out sharedExplicit) == true;
        if (!local && !fromShared)
          continue;
        rows[item.Uid] = new SpamFlag(localExplicit || sharedExplicit, ScoreOf(item.Id));
      }

      return rows;
    }
  }

  public int SpamExampleCount(string modelId) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = "SELECT COUNT(*) FROM spam_examples WHERE model_id = $model AND vector IS NOT NULL;";
      cmd.Parameters.AddWithValue("$model", modelId);
      return (int)(long)(cmd.ExecuteScalar() ?? 0L);
    }
  }

  public string? SpamFingerprintOf(string mailboxId, string folder, uint uid) {
    lock (_gate)
      return Identity(mailboxId, folder, uid)?.Fingerprint;
  }

  public string? SpamFingerprintOf(long messageId) {
    lock (_gate)
      return IdentityById(messageId)?.Fingerprint;
  }

  public IReadOnlyList<SpamLesson> ExportSpam() {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = """
        SELECT fingerprint, model_id, from_addr, subject, date_utc, marked_utc, vector, explicit
        FROM spam_examples;
        """;
      var rows = new List<SpamLesson>();
      using var reader = cmd.ExecuteReader();
      while (reader.Read()) {
        float[]? vector = null;
        if (!reader.IsDBNull(6)) {
          var values = EmbeddingVector.FromBlob((byte[])reader.GetValue(6));
          if (values.Length > 0)
            vector = values;
        }

        rows.Add(new SpamLesson(
          reader.GetString(0),
          reader.GetString(1),
          reader.IsDBNull(2) ? "" : reader.GetString(2),
          reader.IsDBNull(3) ? "" : reader.GetString(3),
          reader.IsDBNull(4) ? "" : reader.GetString(4),
          reader.IsDBNull(5) ? "" : reader.GetString(5),
          reader.GetInt64(7) != 0,
          vector));
      }

      return rows;
    }
  }

  public IReadOnlyList<string> ExportSpamDismissals() {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = "SELECT fingerprint FROM spam_dismissals;";
      var rows = new List<string>();
      using var reader = cmd.ExecuteReader();
      while (reader.Read())
        rows.Add(reader.GetString(0));
      return rows;
    }
  }

  public IReadOnlyList<float[]> SpamVectors(string modelId) {
    lock (_gate)
      return Vectors(modelId, "");
  }

  public void ClearSpamDismissal(string fingerprint) {
    if (string.IsNullOrWhiteSpace(fingerprint))
      return;
    lock (_gate) {
      using var clear = _db.CreateCommand();
      clear.CommandText = "DELETE FROM spam_dismissals WHERE fingerprint = $f;";
      clear.Parameters.AddWithValue("$f", fingerprint);
      clear.ExecuteNonQuery();
    }
  }

  public void ApplySpamEmbedding(
    long messageId,
    string modelId,
    float[] vector,
    bool filterEnabled,
    IReadOnlyList<float[]>? shared = null) {
    ArgumentNullException.ThrowIfNull(vector);
    var stored = EmbeddingVector.Truncate(vector, EmbeddingModelSpec.StoredDimensions);
    lock (_gate) {
      var item = IdentityById(messageId);
      if (item is null)
        return;
      if (Dismissed(item.Fingerprint)) {
        WriteScore(messageId, null);
        return;
      }

      FillVector(item.Fingerprint, modelId, stored);
      if (!filterEnabled)
        return;
      var known = Vectors(modelId, item.Fingerprint);
      if (shared is { Count: > 0 })
        known.AddRange(shared);
      var score = SpamScore.Max(stored, known);
      var show = score >= SpamScore.ShowAt;
      WriteScore(messageId, show ? score : null);
      if (!show || MailFolderRole.Kind(null, item.Folder) == "junk")
        return;
      if (ExampleSet(modelId).ContainsKey(item.Fingerprint))
        return;
      InsertExample(item, modelId, stored, userMarked: false);
      TrimExamples(modelId);
    }
  }

  private void MarkSpamUnlocked(string mailboxId, string folder, uint uid, string modelId) {
    var item = Identity(mailboxId, folder, uid);
    if (item is null)
      return;
    using (var clear = _db.CreateCommand()) {
      clear.CommandText = "DELETE FROM spam_dismissals WHERE fingerprint = $f;";
      clear.Parameters.AddWithValue("$f", item.Fingerprint);
      clear.ExecuteNonQuery();
    }

    float[]? vector = null;
    using (var read = _db.CreateCommand()) {
      read.CommandText = """
        SELECT vector FROM message_embeddings
        WHERE message_id = $id AND model_id = $model;
        """;
      read.Parameters.AddWithValue("$id", item.Id);
      read.Parameters.AddWithValue("$model", modelId);
      if (read.ExecuteScalar() is byte[] blob && blob.Length > 0)
        vector = EmbeddingVector.FromBlob(blob);
    }

    InsertExample(item, modelId, vector, userMarked: true);
    TrimExamples(modelId);
  }

  private void DismissUnlocked(string fingerprint) {
    using (var del = _db.CreateCommand()) {
      del.CommandText = "DELETE FROM spam_examples WHERE fingerprint = $f;";
      del.Parameters.AddWithValue("$f", fingerprint);
      del.ExecuteNonQuery();
    }

    using (var mark = _db.CreateCommand()) {
      mark.CommandText = """
        INSERT INTO spam_dismissals (fingerprint, dismissed_utc)
        VALUES ($f, $utc)
        ON CONFLICT(fingerprint) DO UPDATE SET dismissed_utc = excluded.dismissed_utc;
        """;
      mark.Parameters.AddWithValue("$f", fingerprint);
      mark.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("o"));
      mark.ExecuteNonQuery();
    }

    foreach (var item in Identities(null, null)) {
      if (!item.Fingerprint.Equals(fingerprint, StringComparison.Ordinal))
        continue;
      WriteScore(item.Id, null);
    }
  }

  private void InsertExample(SpamIdentity item, string modelId, float[]? vector, bool userMarked) {
    using var cmd = _db.CreateCommand();
    cmd.CommandText = """
      INSERT INTO spam_examples (
        fingerprint, model_id, from_addr, subject, date_utc, marked_utc, vector, dims, explicit)
      VALUES ($f, $model, $from, $sub, $date, $utc, $vec, $dims, $explicit)
      ON CONFLICT(fingerprint, model_id) DO UPDATE SET
        from_addr = excluded.from_addr,
        subject = excluded.subject,
        date_utc = excluded.date_utc,
        marked_utc = excluded.marked_utc,
        explicit = CASE WHEN excluded.explicit = 1 THEN 1 ELSE spam_examples.explicit END,
        vector = COALESCE(excluded.vector, spam_examples.vector),
        dims = CASE WHEN excluded.vector IS NULL THEN spam_examples.dims ELSE excluded.dims END;
      """;
    cmd.Parameters.AddWithValue("$f", item.Fingerprint);
    cmd.Parameters.AddWithValue("$model", modelId);
    cmd.Parameters.AddWithValue("$from", item.From);
    cmd.Parameters.AddWithValue("$sub", item.Subject);
    cmd.Parameters.AddWithValue("$date", item.DateUtc);
    cmd.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("o"));
    cmd.Parameters.AddWithValue("$explicit", userMarked ? 1 : 0);
    if (vector is { Length: > 0 }) {
      cmd.Parameters.AddWithValue("$vec", EmbeddingVector.ToBlob(vector));
      cmd.Parameters.AddWithValue("$dims", vector.Length);
    }
    else {
      cmd.Parameters.AddWithValue("$vec", DBNull.Value);
      cmd.Parameters.AddWithValue("$dims", 0);
    }

    cmd.ExecuteNonQuery();
  }

  private void FillVector(string fingerprint, string modelId, float[] vector) {
    using var cmd = _db.CreateCommand();
    cmd.CommandText = """
      UPDATE spam_examples
      SET vector = $vec, dims = $dims
      WHERE fingerprint = $f AND model_id = $model AND vector IS NULL;
      """;
    cmd.Parameters.AddWithValue("$vec", EmbeddingVector.ToBlob(vector));
    cmd.Parameters.AddWithValue("$dims", vector.Length);
    cmd.Parameters.AddWithValue("$f", fingerprint);
    cmd.Parameters.AddWithValue("$model", modelId);
    cmd.ExecuteNonQuery();
  }

  private void TrimExamples(string modelId) {
    using var count = _db.CreateCommand();
    count.CommandText = "SELECT COUNT(*) FROM spam_examples WHERE model_id = $model;";
    count.Parameters.AddWithValue("$model", modelId);
    var extra = (int)(long)(count.ExecuteScalar() ?? 0L) - SpamScore.Cap;
    if (extra <= 0)
      return;
    using var del = _db.CreateCommand();
    del.CommandText = """
      DELETE FROM spam_examples
      WHERE rowid IN (
        SELECT rowid FROM spam_examples
        WHERE model_id = $model
        ORDER BY explicit ASC, marked_utc ASC
        LIMIT $n
      );
      """;
    del.Parameters.AddWithValue("$model", modelId);
    del.Parameters.AddWithValue("$n", extra);
    del.ExecuteNonQuery();
  }

  private List<float[]> Vectors(string modelId, string exceptFingerprint) {
    using var cmd = _db.CreateCommand();
    cmd.CommandText = """
      SELECT vector FROM spam_examples
      WHERE model_id = $model AND fingerprint <> $f AND vector IS NOT NULL;
      """;
    cmd.Parameters.AddWithValue("$model", modelId);
    cmd.Parameters.AddWithValue("$f", exceptFingerprint);
    var rows = new List<float[]>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read()) {
      if (reader.IsDBNull(0))
        continue;
      var values = EmbeddingVector.FromBlob((byte[])reader.GetValue(0));
      if (values.Length > 0)
        rows.Add(values);
    }

    return rows;
  }

  private Dictionary<string, (bool Explicit, string Folder, uint Uid)> ExampleSet(string modelId) {
    using var cmd = _db.CreateCommand();
    cmd.CommandText = "SELECT fingerprint, explicit FROM spam_examples WHERE model_id = $model;";
    cmd.Parameters.AddWithValue("$model", modelId);
    var rows = new Dictionary<string, (bool, string, uint)>(StringComparer.Ordinal);
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
      rows[reader.GetString(0)] = (reader.GetInt64(1) != 0, "", 0);
    return rows;
  }

  private Dictionary<string, SpamIdentity> LiveFingerprints() {
    var rows = new Dictionary<string, SpamIdentity>(StringComparer.Ordinal);
    foreach (var item in Identities(null, null))
      rows[item.Fingerprint] = item;
    return rows;
  }

  private SpamIdentity? FindUnlocked(string fingerprint) {
    foreach (var item in Identities(null, null)) {
      if (item.Fingerprint.Equals(fingerprint, StringComparison.Ordinal))
        return item;
    }

    return null;
  }

  private bool Dismissed(string fingerprint) {
    using var cmd = _db.CreateCommand();
    cmd.CommandText = "SELECT 1 FROM spam_dismissals WHERE fingerprint = $f;";
    cmd.Parameters.AddWithValue("$f", fingerprint);
    return cmd.ExecuteScalar() is not null;
  }

  private float ScoreOf(long id) {
    using var cmd = _db.CreateCommand();
    cmd.CommandText = "SELECT spam_score FROM messages WHERE id = $id;";
    cmd.Parameters.AddWithValue("$id", id);
    var value = cmd.ExecuteScalar();
    return value is null or DBNull ? 0f : Convert.ToSingle(value);
  }

  private void WriteScore(long id, float? score) {
    using var cmd = _db.CreateCommand();
    cmd.CommandText = "UPDATE messages SET spam_score = $score WHERE id = $id;";
    cmd.Parameters.AddWithValue("$id", id);
    cmd.Parameters.AddWithValue("$score", score.HasValue ? score.Value : DBNull.Value);
    cmd.ExecuteNonQuery();
  }

  private SpamIdentity? Identity(string mailboxId, string folder, uint uid) {
    foreach (var item in Identities(mailboxId, folder)) {
      if (item.Uid == uid)
        return item;
    }

    return null;
  }

  private SpamIdentity? IdentityById(long id) {
    foreach (var item in Identities(null, null)) {
      if (item.Id == id)
        return item;
    }

    return null;
  }

  private List<SpamIdentity> Identities(string? mailboxId, string? folder) {
    using var cmd = _db.CreateCommand();
    cmd.CommandText = """
      SELECT id, mailbox_id, folder, uid, message_id, from_addr, subject, date_utc, substr(body_text, 1, 160)
      FROM messages
      WHERE ($m = '' OR mailbox_id = $m)
        AND ($f = '' OR folder = $f);
      """;
    cmd.Parameters.AddWithValue("$m", mailboxId ?? "");
    cmd.Parameters.AddWithValue("$f", folder ?? "");
    var rows = new List<SpamIdentity>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read()) {
      var messageId = reader.IsDBNull(4) ? "" : reader.GetString(4);
      var from = reader.IsDBNull(5) ? "" : reader.GetString(5);
      var subject = reader.IsDBNull(6) ? "" : reader.GetString(6);
      var date = reader.IsDBNull(7) ? "" : reader.GetString(7);
      var body = reader.IsDBNull(8) ? "" : reader.GetString(8);
      rows.Add(new SpamIdentity(
        reader.GetInt64(0),
        reader.GetString(2),
        (uint)reader.GetInt64(3),
        from,
        subject,
        date,
        SpamFingerprint.Of(messageId, from, subject, date, body)));
    }

    return rows;
  }

  private sealed record SpamIdentity(
    long Id,
    string Folder,
    uint Uid,
    string From,
    string Subject,
    string DateUtc,
    string Fingerprint);
}
