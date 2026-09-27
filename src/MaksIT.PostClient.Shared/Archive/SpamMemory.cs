using Microsoft.Data.Sqlite;


namespace MaksIT.PostClient.Shared.Archive;


public sealed class SpamMemory : IDisposable {
  private readonly SqliteConnection _db;
  private readonly Lock _gate = new();

  public SpamMemory(string path) {
    ArgumentException.ThrowIfNullOrWhiteSpace(path);
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);
    _db = new SqliteConnection("Data Source=" + path + ";Pooling=False");
    _db.Open();
    using var pragma = _db.CreateCommand();
    pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
    pragma.ExecuteNonQuery();
    EnsureSchema();
  }

  public void Absorb(IReadOnlyList<SpamLesson> lessons, IReadOnlyList<string> dismissals) {
    ArgumentNullException.ThrowIfNull(lessons);
    ArgumentNullException.ThrowIfNull(dismissals);
    lock (_gate) {
      foreach (var lesson in lessons) {
        if (Dismissed(lesson.Fingerprint))
          continue;
        RememberUnlocked(lesson);
      }

      foreach (var fingerprint in dismissals) {
        if (IsExplicit(fingerprint))
          continue;
        DismissUnlocked(fingerprint);
      }
    }
  }

  public void Remember(SpamLesson lesson) {
    lock (_gate)
      RememberUnlocked(lesson);
  }

  public void ClearDismissal(string fingerprint) {
    if (string.IsNullOrWhiteSpace(fingerprint))
      return;
    lock (_gate) {
      using var clear = _db.CreateCommand();
      clear.CommandText = "DELETE FROM spam_dismissals WHERE fingerprint = $f;";
      clear.Parameters.AddWithValue("$f", fingerprint);
      clear.ExecuteNonQuery();
    }
  }

  public void Dismiss(string fingerprint) {
    if (string.IsNullOrWhiteSpace(fingerprint))
      return;
    lock (_gate)
      DismissUnlocked(fingerprint);
  }

  public IReadOnlyList<float[]> Vectors(string modelId, string exceptFingerprint) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = """
        SELECT vector FROM spam_examples
        WHERE model_id = $model AND fingerprint <> $f AND vector IS NOT NULL;
        """;
      cmd.Parameters.AddWithValue("$model", modelId);
      cmd.Parameters.AddWithValue("$f", exceptFingerprint ?? "");
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
  }

  public IReadOnlyDictionary<string, bool> ExplicitMap(string modelId) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = "SELECT fingerprint, explicit FROM spam_examples WHERE model_id = $model;";
      cmd.Parameters.AddWithValue("$model", modelId);
      var rows = new Dictionary<string, bool>(StringComparer.Ordinal);
      using var reader = cmd.ExecuteReader();
      while (reader.Read())
        rows[reader.GetString(0)] = reader.GetInt64(1) != 0;
      return rows;
    }
  }

  public IReadOnlyList<SpamExampleInfo> List(string modelId) {
    lock (_gate) {
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
        rows.Add(new SpamExampleInfo {
          Fingerprint = reader.GetString(0),
          From = reader.IsDBNull(1) ? "" : reader.GetString(1),
          Subject = reader.IsDBNull(2) ? "" : reader.GetString(2),
          DateUtc = reader.IsDBNull(3) ? "" : reader.GetString(3),
          Explicit = reader.GetInt64(4) != 0
        });
      }

      return rows;
    }
  }

  public int Count(string modelId) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = "SELECT COUNT(*) FROM spam_examples WHERE model_id = $model AND vector IS NOT NULL;";
      cmd.Parameters.AddWithValue("$model", modelId);
      return (int)(long)(cmd.ExecuteScalar() ?? 0L);
    }
  }

  public void Dispose() => _db.Dispose();

  private void RememberUnlocked(SpamLesson lesson) {
    if (string.IsNullOrWhiteSpace(lesson.Fingerprint) || string.IsNullOrWhiteSpace(lesson.ModelId))
      return;
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
    cmd.Parameters.AddWithValue("$f", lesson.Fingerprint);
    cmd.Parameters.AddWithValue("$model", lesson.ModelId);
    cmd.Parameters.AddWithValue("$from", lesson.From ?? "");
    cmd.Parameters.AddWithValue("$sub", lesson.Subject ?? "");
    cmd.Parameters.AddWithValue("$date", lesson.DateUtc ?? "");
    cmd.Parameters.AddWithValue("$utc", string.IsNullOrWhiteSpace(lesson.MarkedUtc)
      ? DateTimeOffset.UtcNow.ToString("o")
      : lesson.MarkedUtc);
    cmd.Parameters.AddWithValue("$explicit", lesson.Explicit ? 1 : 0);
    if (lesson.Vector is { Length: > 0 }) {
      cmd.Parameters.AddWithValue("$vec", EmbeddingVector.ToBlob(lesson.Vector));
      cmd.Parameters.AddWithValue("$dims", lesson.Vector.Length);
    }
    else {
      cmd.Parameters.AddWithValue("$vec", DBNull.Value);
      cmd.Parameters.AddWithValue("$dims", 0);
    }

    cmd.ExecuteNonQuery();
  }

  private void DismissUnlocked(string fingerprint) {
    using (var del = _db.CreateCommand()) {
      del.CommandText = "DELETE FROM spam_examples WHERE fingerprint = $f;";
      del.Parameters.AddWithValue("$f", fingerprint);
      del.ExecuteNonQuery();
    }

    using var mark = _db.CreateCommand();
    mark.CommandText = """
      INSERT INTO spam_dismissals (fingerprint, dismissed_utc)
      VALUES ($f, $utc)
      ON CONFLICT(fingerprint) DO UPDATE SET dismissed_utc = excluded.dismissed_utc;
      """;
    mark.Parameters.AddWithValue("$f", fingerprint);
    mark.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("o"));
    mark.ExecuteNonQuery();
  }

  private bool Dismissed(string fingerprint) {
    using var cmd = _db.CreateCommand();
    cmd.CommandText = "SELECT 1 FROM spam_dismissals WHERE fingerprint = $f;";
    cmd.Parameters.AddWithValue("$f", fingerprint);
    return cmd.ExecuteScalar() is not null;
  }

  private bool IsExplicit(string fingerprint) {
    using var cmd = _db.CreateCommand();
    cmd.CommandText = "SELECT 1 FROM spam_examples WHERE fingerprint = $f AND explicit = 1 LIMIT 1;";
    cmd.Parameters.AddWithValue("$f", fingerprint);
    return cmd.ExecuteScalar() is not null;
  }

  private void EnsureSchema() {
    using var cmd = _db.CreateCommand();
    cmd.CommandText = """
      CREATE TABLE IF NOT EXISTS spam_examples (
        fingerprint TEXT NOT NULL,
        model_id TEXT NOT NULL,
        from_addr TEXT NOT NULL DEFAULT '',
        subject TEXT NOT NULL DEFAULT '',
        date_utc TEXT NOT NULL DEFAULT '',
        marked_utc TEXT NOT NULL DEFAULT '',
        vector BLOB,
        dims INTEGER NOT NULL DEFAULT 0,
        explicit INTEGER NOT NULL DEFAULT 0,
        PRIMARY KEY (fingerprint, model_id)
      );
      CREATE TABLE IF NOT EXISTS spam_dismissals (
        fingerprint TEXT PRIMARY KEY,
        dismissed_utc TEXT NOT NULL DEFAULT ''
      );
      """;
    cmd.ExecuteNonQuery();
  }
}
