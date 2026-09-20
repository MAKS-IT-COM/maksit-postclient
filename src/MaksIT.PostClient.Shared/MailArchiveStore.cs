using Microsoft.Data.Sqlite;


namespace MaksIT.PostClient.Shared;


public sealed class MailArchiveStore : IDisposable {
  private readonly SqliteConnection _db;
  private readonly Lock _gate = new();

  public bool KeywordIndexEnabled { get; set; } = true;

  public MailArchiveStore(string? path = null) {
    var file = string.IsNullOrWhiteSpace(path) ? AppPaths.ArchiveDatabase() : path;
    var dir = Path.GetDirectoryName(file);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);
    _db = new SqliteConnection("Data Source=" + file + ";Pooling=False");
    _db.Open();
    using var pragma = _db.CreateCommand();
    pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
    pragma.ExecuteNonQuery();
    EnsureSchema();
  }

  public void UpsertHeaders(string mailboxId, IEnumerable<MailArchiveHeader> headers) {
    ArgumentNullException.ThrowIfNull(headers);
    lock (_gate) {
      using var tx = _db.BeginTransaction();
      foreach (var header in headers)
        UpsertHeader(tx, mailboxId, header, updateFlags: true);
      tx.Commit();
    }
  }

  public void UpdateFlags(
    string mailboxId,
    string folder,
    IEnumerable<(uint Uid, bool IsSeen, bool IsFlagged)> flags) {
    ArgumentNullException.ThrowIfNull(flags);
    lock (_gate) {
      using var tx = _db.BeginTransaction();
      foreach (var row in flags) {
        using var cmd = _db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
          UPDATE messages
          SET is_seen = $seen, is_flagged = $flag
          WHERE mailbox_id = $m AND folder = $f AND uid = $u;
          """;
        cmd.Parameters.AddWithValue("$seen", row.IsSeen ? 1 : 0);
        cmd.Parameters.AddWithValue("$flag", row.IsFlagged ? 1 : 0);
        cmd.Parameters.AddWithValue("$m", mailboxId);
        cmd.Parameters.AddWithValue("$f", folder);
        cmd.Parameters.AddWithValue("$u", row.Uid);
        cmd.ExecuteNonQuery();
      }

      tx.Commit();
    }
  }

  public void UpsertBody(
    string mailboxId,
    MailArchiveHeader header,
    string emlPath,
    string bodyText,
    string attachmentText) {
    lock (_gate) {
      using var tx = _db.BeginTransaction();
      var id = UpsertHeader(tx, mailboxId, header, updateFlags: false);
      using (var cmd = _db.CreateCommand()) {
        cmd.Transaction = tx;
        cmd.CommandText = """
          UPDATE messages
          SET eml_path = $eml, body_text = $body, attachment_text = $att
          WHERE id = $id;
          """;
        cmd.Parameters.AddWithValue("$eml", emlPath);
        cmd.Parameters.AddWithValue("$body", bodyText);
        cmd.Parameters.AddWithValue("$att", attachmentText);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
      }

      IndexFts(tx, id, header, bodyText, attachmentText);
      tx.Commit();
    }
  }

  public IReadOnlyList<EmbeddingWorkItem> PendingEmbeddings(string modelId, int limit) {
    var take = Math.Clamp(limit, 1, 64);
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = """
        SELECT m.mailbox_id, m.id, m.subject, m.from_addr, m.body_text, m.attachment_text
        FROM messages m
        LEFT JOIN message_embeddings e ON e.message_id = m.id
        WHERE (e.message_id IS NULL OR e.model_id <> $model)
          AND (m.body_text <> '' OR m.subject <> '')
        ORDER BY m.date_utc DESC, m.id DESC
        LIMIT $n;
        """;
      cmd.Parameters.AddWithValue("$model", modelId);
      cmd.Parameters.AddWithValue("$n", take);
      var rows = new List<EmbeddingWorkItem>();
      using var reader = cmd.ExecuteReader();
      while (reader.Read()) {
        rows.Add(new EmbeddingWorkItem(
          reader.GetString(0),
          reader.GetInt64(1),
          reader.IsDBNull(2) ? "" : reader.GetString(2),
          reader.IsDBNull(3) ? "" : reader.GetString(3),
          reader.IsDBNull(4) ? "" : reader.GetString(4),
          reader.IsDBNull(5) ? "" : reader.GetString(5)));
      }

      return rows;
    }
  }

  public int EmbeddingCount(string modelId) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = "SELECT COUNT(*) FROM message_embeddings WHERE model_id = $model;";
      cmd.Parameters.AddWithValue("$model", modelId);
      return (int)(long)(cmd.ExecuteScalar() ?? 0L);
    }
  }

  public int EmbeddingPendingCount(string modelId) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = """
        SELECT COUNT(*)
        FROM messages m
        LEFT JOIN message_embeddings e ON e.message_id = m.id
        WHERE (e.message_id IS NULL OR e.model_id <> $model)
          AND (m.body_text <> '' OR m.subject <> '');
        """;
      cmd.Parameters.AddWithValue("$model", modelId);
      return (int)(long)(cmd.ExecuteScalar() ?? 0L);
    }
  }

  public void UpsertEmbedding(long messageId, string modelId, float[] vector) {
    ArgumentNullException.ThrowIfNull(vector);
    var stored = EmbeddingVector.Truncate(vector, EmbeddingModelSpec.StoredDimensions);
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = """
        INSERT INTO message_embeddings (message_id, model_id, dims, vector)
        VALUES ($id, $model, $dims, $vec)
        ON CONFLICT(message_id) DO UPDATE SET
          model_id = excluded.model_id,
          dims = excluded.dims,
          vector = excluded.vector;
        """;
      cmd.Parameters.AddWithValue("$id", messageId);
      cmd.Parameters.AddWithValue("$model", modelId);
      cmd.Parameters.AddWithValue("$dims", stored.Length);
      cmd.Parameters.AddWithValue("$vec", EmbeddingVector.ToBlob(stored));
      cmd.ExecuteNonQuery();
    }
  }

  public void DropForeignEmbeddings(string modelId) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = "DELETE FROM message_embeddings WHERE model_id <> $model;";
      cmd.Parameters.AddWithValue("$model", modelId);
      cmd.ExecuteNonQuery();
    }
  }

  public int ClearEmbeddings() {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = "DELETE FROM message_embeddings;";
      return cmd.ExecuteNonQuery();
    }
  }

  public ArchiveIndexStats IndexStats(string modelId) {
    lock (_gate) {
      return ReadIndexStats(modelId);
    }
  }

  public int RebuildKeywordIndex() {
    KeywordIndexEnabled = true;
    lock (_gate) {
      using var tx = _db.BeginTransaction();
      using (var del = _db.CreateCommand()) {
        del.Transaction = tx;
        del.CommandText = "DELETE FROM messages_fts;";
        del.ExecuteNonQuery();
      }

      using var list = _db.CreateCommand();
      list.Transaction = tx;
      list.CommandText = """
        SELECT id, subject, from_addr, body_text, attachment_text, envelope_badge
        FROM messages;
        """;
      var rows = new List<(long Id, MailArchiveHeader Header, string Body, string Att)>();
      using (var reader = list.ExecuteReader()) {
        while (reader.Read()) {
          rows.Add((
            reader.GetInt64(0),
            new MailArchiveHeader {
              Subject = reader.IsDBNull(1) ? "" : reader.GetString(1),
              From = reader.IsDBNull(2) ? "" : reader.GetString(2),
              EnvelopeBadge = reader.IsDBNull(5) ? "" : reader.GetString(5)
            },
            reader.IsDBNull(3) ? "" : reader.GetString(3),
            reader.IsDBNull(4) ? "" : reader.GetString(4)));
        }
      }

      foreach (var row in rows)
        IndexFts(tx, row.Id, row.Header, row.Body, row.Att);
      tx.Commit();
      return rows.Count;
    }
  }

  public ArchiveIndexStats SanitizeIndices(string modelId) {
    lock (_gate) {
      using var tx = _db.BeginTransaction();
      using (var fts = _db.CreateCommand()) {
        fts.Transaction = tx;
        fts.CommandText = "DELETE FROM messages_fts WHERE rowid NOT IN (SELECT id FROM messages);";
        fts.ExecuteNonQuery();
      }

      using (var orphans = _db.CreateCommand()) {
        orphans.Transaction = tx;
        orphans.CommandText = """
          DELETE FROM message_embeddings
          WHERE message_id NOT IN (SELECT id FROM messages);
          """;
        orphans.ExecuteNonQuery();
      }

      using (var foreign = _db.CreateCommand()) {
        foreign.Transaction = tx;
        foreign.CommandText = "DELETE FROM message_embeddings WHERE model_id <> $model;";
        foreign.Parameters.AddWithValue("$model", modelId);
        foreign.ExecuteNonQuery();
      }

      tx.Commit();
      return ReadIndexStats(modelId);
    }
  }

  public IReadOnlyList<string> ListFolders(string mailboxId) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = "SELECT DISTINCT folder FROM messages WHERE mailbox_id = $m ORDER BY folder COLLATE NOCASE;";
      cmd.Parameters.AddWithValue("$m", mailboxId);
      var rows = new List<string>();
      using var reader = cmd.ExecuteReader();
      while (reader.Read()) {
        var folder = reader.GetString(0);
        if (!string.IsNullOrWhiteSpace(folder))
          rows.Add(folder);
      }

      return rows;
    }
  }

  public IReadOnlyList<MailArchiveHeader> ListFolder(string mailboxId, string folder) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = """
        SELECT uid, folder, subject, from_addr, date_utc, is_seen, is_flagged, has_attachments,
               priority, envelope_kind, envelope_badge, envelope_tipo, message_id, in_reply_to, eml_path,
               (SELECT GROUP_CONCAT(name, ', ') FROM message_labels l
                WHERE l.mailbox_id = messages.mailbox_id AND l.folder = messages.folder AND l.uid = messages.uid)
        FROM messages
        WHERE mailbox_id = $m AND folder = $f
        ORDER BY date_utc DESC, uid DESC;
        """;
      cmd.Parameters.AddWithValue("$m", mailboxId);
      cmd.Parameters.AddWithValue("$f", folder);
      return ReadHeaders(cmd);
    }
  }

  public (int Total, int Unread) FolderCounts(string mailboxId, string folder) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = """
        SELECT COUNT(*), COALESCE(SUM(CASE WHEN is_seen = 0 THEN 1 ELSE 0 END), 0)
        FROM messages
        WHERE mailbox_id = $m AND folder = $f;
        """;
      cmd.Parameters.AddWithValue("$m", mailboxId);
      cmd.Parameters.AddWithValue("$f", folder);
      using var reader = cmd.ExecuteReader();
      if (!reader.Read())
        return (0, 0);
      return ((int)reader.GetInt64(0), (int)reader.GetInt64(1));
    }
  }

  public IReadOnlySet<uint> Uids(string mailboxId, string folder) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = "SELECT uid FROM messages WHERE mailbox_id = $m AND folder = $f;";
      cmd.Parameters.AddWithValue("$m", mailboxId);
      cmd.Parameters.AddWithValue("$f", folder);
      var set = new HashSet<uint>();
      using var reader = cmd.ExecuteReader();
      while (reader.Read())
        set.Add((uint)reader.GetInt64(0));
      return set;
    }
  }

  public IReadOnlyList<uint> MissingBodies(string mailboxId, string folder) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = """
        SELECT uid FROM messages
        WHERE mailbox_id = $m AND folder = $f AND (eml_path IS NULL OR eml_path = '')
        ORDER BY date_utc DESC;
        """;
      cmd.Parameters.AddWithValue("$m", mailboxId);
      cmd.Parameters.AddWithValue("$f", folder);
      var ids = new List<uint>();
      using var reader = cmd.ExecuteReader();
      while (reader.Read())
        ids.Add((uint)reader.GetInt64(0));
      return ids;
    }
  }

  public IReadOnlyList<MailArchiveUid> MissingBodies(string mailboxId) =>
    MissingBodies([mailboxId]);

  public IReadOnlyList<MailArchiveUid> MissingBodies(
    IReadOnlyCollection<string>? mailboxIds = null,
    int limit = 0) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      var sql = """
        SELECT mailbox_id, folder, uid FROM messages
        WHERE (eml_path IS NULL OR eml_path = '')
        """;
      sql += MailboxInSql(cmd, mailboxIds);
      sql += " ORDER BY date_utc DESC, uid DESC";
      if (limit > 0) {
        sql += " LIMIT $n";
        cmd.Parameters.AddWithValue("$n", Math.Clamp(limit, 1, 256));
      }

      cmd.CommandText = sql;
      var rows = new List<MailArchiveUid>();
      using var reader = cmd.ExecuteReader();
      while (reader.Read()) {
        rows.Add(new MailArchiveUid(
          reader.GetString(0),
          reader.GetString(1),
          (uint)reader.GetInt64(2)));
      }

      return rows;
    }
  }

  public int MissingBodyCount(IReadOnlyCollection<string>? mailboxIds = null) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      var sql = """
        SELECT COUNT(*) FROM messages
        WHERE (eml_path IS NULL OR eml_path = '')
        """;
      sql += MailboxInSql(cmd, mailboxIds);
      cmd.CommandText = sql;
      return (int)(long)(cmd.ExecuteScalar() ?? 0L);
    }
  }

  private static string MailboxInSql(SqliteCommand cmd, IReadOnlyCollection<string>? mailboxIds) {
    if (mailboxIds is not { Count: > 0 })
      return "";
    var names = new List<string>(mailboxIds.Count);
    var i = 0;
    foreach (var id in mailboxIds) {
      var name = "$mb" + i;
      names.Add(name);
      cmd.Parameters.AddWithValue(name, id);
      i++;
    }

    return " AND mailbox_id IN (" + string.Join(",", names) + ")";
  }

  public void SetSeen(string mailboxId, string folder, bool seen) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = """
        UPDATE messages
        SET is_seen = $seen
        WHERE mailbox_id = $m AND folder = $f;
        """;
      cmd.Parameters.AddWithValue("$seen", seen ? 1 : 0);
      cmd.Parameters.AddWithValue("$m", mailboxId);
      cmd.Parameters.AddWithValue("$f", folder);
      cmd.ExecuteNonQuery();
    }
  }

  public IReadOnlyList<string> RemoveFolder(string mailboxId, string folder) =>
    RemoveUids(mailboxId, folder, uids: null);

  public int RewriteFolderPrefix(string mailboxId, string from, string to) {
    if (string.IsNullOrWhiteSpace(mailboxId) || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
      return 0;
    var folders = ListFolders(mailboxId);
    var count = 0;
    lock (_gate) {
      using var tx = _db.BeginTransaction();
      foreach (var folder in folders) {
        var next = MailFolderPath.Rewrite(folder, from, to);
        if (string.IsNullOrWhiteSpace(next) || next.Equals(folder, StringComparison.Ordinal))
          continue;
        using (var labels = _db.CreateCommand()) {
          labels.Transaction = tx;
          labels.CommandText = """
            UPDATE message_labels
            SET folder = $to
            WHERE mailbox_id = $m AND folder = $from;
            """;
          labels.Parameters.AddWithValue("$to", next);
          labels.Parameters.AddWithValue("$m", mailboxId);
          labels.Parameters.AddWithValue("$from", folder);
          labels.ExecuteNonQuery();
        }

        using (var messages = _db.CreateCommand()) {
          messages.Transaction = tx;
          messages.CommandText = """
            UPDATE messages
            SET folder = $to
            WHERE mailbox_id = $m AND folder = $from;
            """;
          messages.Parameters.AddWithValue("$to", next);
          messages.Parameters.AddWithValue("$m", mailboxId);
          messages.Parameters.AddWithValue("$from", folder);
          messages.ExecuteNonQuery();
        }

        count++;
      }

      tx.Commit();
    }

    return count;
  }

  public IReadOnlyList<string> RemoveUids(
    string mailboxId,
    string folder,
    IReadOnlyCollection<uint>? uids) {
    if (uids is { Count: 0 })
      return [];
    lock (_gate) {
      var paths = EmlPaths(mailboxId, folder, uids);
      using var tx = _db.BeginTransaction();
      DeleteFts(tx, mailboxId, folder, uids);
      DeleteEmbeddings(tx, mailboxId, folder, uids);
      using (var labels = _db.CreateCommand()) {
        labels.Transaction = tx;
        labels.CommandText = "DELETE FROM message_labels WHERE mailbox_id = $m AND folder = $f"
          + UidFilter(labels, uids)
          + ";";
        labels.Parameters.AddWithValue("$m", mailboxId);
        labels.Parameters.AddWithValue("$f", folder);
        labels.ExecuteNonQuery();
      }

      using (var messages = _db.CreateCommand()) {
        messages.Transaction = tx;
        messages.CommandText = "DELETE FROM messages WHERE mailbox_id = $m AND folder = $f"
          + UidFilter(messages, uids)
          + ";";
        messages.Parameters.AddWithValue("$m", mailboxId);
        messages.Parameters.AddWithValue("$f", folder);
        messages.ExecuteNonQuery();
      }

      tx.Commit();
      return paths;
    }
  }

  public string? EmlPath(string mailboxId, string folder, uint uid) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = """
        SELECT eml_path FROM messages
        WHERE mailbox_id = $m AND folder = $f AND uid = $u;
        """;
      cmd.Parameters.AddWithValue("$m", mailboxId);
      cmd.Parameters.AddWithValue("$f", folder);
      cmd.Parameters.AddWithValue("$u", uid);
      var value = cmd.ExecuteScalar() as string;
      return string.IsNullOrWhiteSpace(value) ? null : value;
    }
  }

  public IReadOnlyList<MailArchiveHeader> Search(string mailboxId, string folder, string query) =>
    Search(mailboxId, folder, query, queryVector: null);

  public IReadOnlyList<MailArchiveHeader> Search(
    string mailboxId,
    string folder,
    string query,
    float[]? queryVector) {
    var text = query.Trim();
    if (text.Length == 0)
      return ListFolder(mailboxId, folder);

    IReadOnlyList<MailArchiveHeader> lexical;
    lock (_gate) {
      try {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
          SELECT m.uid, m.folder, m.subject, m.from_addr, m.date_utc, m.is_seen, m.is_flagged, m.has_attachments,
                 m.priority, m.envelope_kind, m.envelope_badge, m.envelope_tipo, m.message_id, m.in_reply_to, m.eml_path,
                 (SELECT GROUP_CONCAT(name, ', ') FROM message_labels l
                  WHERE l.mailbox_id = m.mailbox_id AND l.folder = m.folder AND l.uid = m.uid)
          FROM messages_fts
          JOIN messages m ON m.id = messages_fts.rowid
          WHERE messages_fts MATCH $q AND m.mailbox_id = $m AND m.folder = $f
          UNION
          SELECT m.uid, m.folder, m.subject, m.from_addr, m.date_utc, m.is_seen, m.is_flagged, m.has_attachments,
                 m.priority, m.envelope_kind, m.envelope_badge, m.envelope_tipo, m.message_id, m.in_reply_to, m.eml_path,
                 (SELECT GROUP_CONCAT(name, ', ') FROM message_labels l
                  WHERE l.mailbox_id = m.mailbox_id AND l.folder = m.folder AND l.uid = m.uid)
          FROM messages m
          WHERE m.mailbox_id = $m AND m.folder = $f
            AND EXISTS (
              SELECT 1 FROM message_labels l
              WHERE l.mailbox_id = m.mailbox_id AND l.folder = m.folder
                AND l.uid = m.uid AND l.name LIKE $like)
          ORDER BY 5 DESC, 1 DESC;
          """;
        cmd.Parameters.AddWithValue("$q", FtsQuery(text));
        cmd.Parameters.AddWithValue("$like", "%" + text.Replace("%", "\\%").Replace("_", "\\_") + "%");
        cmd.Parameters.AddWithValue("$m", mailboxId);
        cmd.Parameters.AddWithValue("$f", folder);
        lexical = ReadHeaders(cmd);
      }
      catch (SqliteException) {
        lexical = SearchLike(mailboxId, folder, text);
      }
    }

    if (queryVector is null || queryVector.Length == 0)
      return lexical;
    return MergeSemantic(mailboxId, folder, lexical, queryVector);
  }

  public uint NextUid(string mailboxId, string folder) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = "SELECT COALESCE(MAX(uid), 0) FROM messages WHERE mailbox_id = $m AND folder = $f;";
      cmd.Parameters.AddWithValue("$m", mailboxId);
      cmd.Parameters.AddWithValue("$f", folder);
      var max = (long)(cmd.ExecuteScalar() ?? 0L);
      return (uint)max + 1;
    }
  }

  public IReadOnlyList<string> LabelNames(string mailboxId) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = """
        SELECT DISTINCT name FROM message_labels
        WHERE mailbox_id = $m
        ORDER BY name COLLATE NOCASE;
        """;
      cmd.Parameters.AddWithValue("$m", mailboxId);
      var names = new List<string>();
      using var reader = cmd.ExecuteReader();
      while (reader.Read())
        names.Add(reader.GetString(0));
      return names;
    }
  }

  public void AddLabel(string mailboxId, string folder, uint uid, string name) {
    var label = name.Trim();
    if (label.Length == 0)
      return;
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = """
        INSERT OR IGNORE INTO message_labels (mailbox_id, folder, uid, name)
        VALUES ($m, $f, $u, $n);
        """;
      cmd.Parameters.AddWithValue("$m", mailboxId);
      cmd.Parameters.AddWithValue("$f", folder);
      cmd.Parameters.AddWithValue("$u", uid);
      cmd.Parameters.AddWithValue("$n", label);
      cmd.ExecuteNonQuery();
    }
  }

  public void Checkpoint() {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
      cmd.ExecuteNonQuery();
    }
  }

  public bool HasMessageId(string mailboxId, string messageId) {
    if (string.IsNullOrWhiteSpace(messageId))
      return false;
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = """
        SELECT 1 FROM messages
        WHERE mailbox_id = $m AND message_id = $mid
        LIMIT 1;
        """;
      cmd.Parameters.AddWithValue("$m", mailboxId);
      cmd.Parameters.AddWithValue("$mid", messageId.Trim());
      return cmd.ExecuteScalar() is not null;
    }
  }

  public MailArchiveCopy? ReadCopy(string mailboxId, string folder, uint uid) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = """
        SELECT m.uid, m.folder, m.subject, m.from_addr, m.date_utc, m.is_seen, m.is_flagged, m.has_attachments,
               m.priority, m.envelope_kind, m.envelope_badge, m.envelope_tipo, m.message_id, m.in_reply_to, m.eml_path,
               (SELECT GROUP_CONCAT(name, ', ') FROM message_labels l
                WHERE l.mailbox_id = m.mailbox_id AND l.folder = m.folder AND l.uid = m.uid),
               m.body_text, m.attachment_text, e.model_id, e.vector
        FROM messages m
        LEFT JOIN message_embeddings e ON e.message_id = m.id
        WHERE m.mailbox_id = $m AND m.folder = $f AND m.uid = $u;
        """;
      cmd.Parameters.AddWithValue("$m", mailboxId);
      cmd.Parameters.AddWithValue("$f", folder);
      cmd.Parameters.AddWithValue("$u", uid);
      using var reader = cmd.ExecuteReader();
      if (!reader.Read())
        return null;
      var header = ReadHeader(reader);
      var body = reader.IsDBNull(16) ? "" : reader.GetString(16);
      var att = reader.IsDBNull(17) ? "" : reader.GetString(17);
      var model = reader.IsDBNull(18) ? "" : reader.GetString(18);
      float[]? vector = null;
      if (!reader.IsDBNull(19))
        vector = EmbeddingVector.FromBlob(reader.GetFieldValue<byte[]>(19));
      var labels = string.IsNullOrWhiteSpace(header.Labels)
        ? Array.Empty<string>()
        : header.Labels.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
      return new MailArchiveCopy {
        Header = header,
        BodyText = body,
        AttachmentText = att,
        Labels = labels,
        ModelId = model,
        Vector = vector
      };
    }
  }

  public uint InsertCopy(string mailboxId, string folder, MailArchiveCopy copy) {
    ArgumentNullException.ThrowIfNull(copy);
    lock (_gate) {
      using var tx = _db.BeginTransaction();
      var uid = copy.Header.Uid;
      if (uid == 0)
        uid = NextUidUnlocked(mailboxId, folder);
      var header = new MailArchiveHeader {
        Uid = uid,
        Folder = folder,
        Subject = copy.Header.Subject,
        From = copy.Header.From,
        Date = copy.Header.Date,
        IsSeen = copy.Header.IsSeen,
        IsFlagged = copy.Header.IsFlagged,
        HasAttachments = copy.Header.HasAttachments,
        Priority = copy.Header.Priority,
        EnvelopeKind = copy.Header.EnvelopeKind,
        EnvelopeBadge = copy.Header.EnvelopeBadge,
        EnvelopeTipo = copy.Header.EnvelopeTipo,
        MessageId = copy.Header.MessageId,
        InReplyTo = copy.Header.InReplyTo,
        EmlPath = copy.Header.EmlPath,
        Labels = copy.Header.Labels
      };
      var id = UpsertHeader(tx, mailboxId, header, updateFlags: true);
      using (var body = _db.CreateCommand()) {
        body.Transaction = tx;
        body.CommandText = """
          UPDATE messages
          SET eml_path = $eml, body_text = $body, attachment_text = $att
          WHERE id = $id;
          """;
        body.Parameters.AddWithValue("$eml", header.EmlPath ?? "");
        body.Parameters.AddWithValue("$body", copy.BodyText ?? "");
        body.Parameters.AddWithValue("$att", copy.AttachmentText ?? "");
        body.Parameters.AddWithValue("$id", id);
        body.ExecuteNonQuery();
      }

      IndexFts(tx, id, header, copy.BodyText, copy.AttachmentText);
      if (copy.Vector is { Length: > 0 }) {
        using var emb = _db.CreateCommand();
        emb.Transaction = tx;
        emb.CommandText = """
          INSERT INTO message_embeddings (message_id, model_id, dims, vector)
          VALUES ($id, $model, $dims, $vec)
          ON CONFLICT(message_id) DO UPDATE SET
            model_id = excluded.model_id,
            dims = excluded.dims,
            vector = excluded.vector;
          """;
        var stored = EmbeddingVector.Truncate(copy.Vector, EmbeddingModelSpec.StoredDimensions);
        emb.Parameters.AddWithValue("$id", id);
        emb.Parameters.AddWithValue("$model", string.IsNullOrWhiteSpace(copy.ModelId) ? EmbeddingModelSpec.Id : copy.ModelId);
        emb.Parameters.AddWithValue("$dims", stored.Length);
        emb.Parameters.AddWithValue("$vec", EmbeddingVector.ToBlob(stored));
        emb.ExecuteNonQuery();
      }

      foreach (var label in copy.Labels) {
        if (string.IsNullOrWhiteSpace(label))
          continue;
        using var lab = _db.CreateCommand();
        lab.Transaction = tx;
        lab.CommandText = """
          INSERT OR IGNORE INTO message_labels (mailbox_id, folder, uid, name)
          VALUES ($m, $f, $u, $n);
          """;
        lab.Parameters.AddWithValue("$m", mailboxId);
        lab.Parameters.AddWithValue("$f", folder);
        lab.Parameters.AddWithValue("$u", uid);
        lab.Parameters.AddWithValue("$n", label.Trim());
        lab.ExecuteNonQuery();
      }

      tx.Commit();
      return uid;
    }
  }

  public IReadOnlyList<uint> UidsOlderThan(string mailboxId, string folder, DateTimeOffset cutoff) {
    var trash = MailRetention.IsTrash(folder);
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = trash
        ? """
          SELECT uid FROM messages
          WHERE mailbox_id = $m AND folder = $f
            AND COALESCE(NULLIF(trashed_utc, ''), date_utc) <> ''
            AND COALESCE(NULLIF(trashed_utc, ''), date_utc) < $cut
          ORDER BY COALESCE(NULLIF(trashed_utc, ''), date_utc);
          """
        : """
          SELECT uid FROM messages
          WHERE mailbox_id = $m AND folder = $f AND date_utc <> '' AND date_utc < $cut
          ORDER BY date_utc;
          """;
      cmd.Parameters.AddWithValue("$m", mailboxId);
      cmd.Parameters.AddWithValue("$f", folder);
      cmd.Parameters.AddWithValue("$cut", cutoff.ToString("O"));
      var ids = new List<uint>();
      using var reader = cmd.ExecuteReader();
      while (reader.Read())
        ids.Add((uint)reader.GetInt64(0));
      return ids;
    }
  }

  public int MessageCount(string mailboxId) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = "SELECT COUNT(*) FROM messages WHERE mailbox_id = $m;";
      cmd.Parameters.AddWithValue("$m", mailboxId);
      return (int)(long)(cmd.ExecuteScalar() ?? 0L);
    }
  }

  public IReadOnlyList<string> DistinctMailboxIds() {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = "SELECT DISTINCT mailbox_id FROM messages ORDER BY mailbox_id;";
      var ids = new List<string>();
      using var reader = cmd.ExecuteReader();
      while (reader.Read())
        ids.Add(reader.GetString(0));
      return ids;
    }
  }

  public void CopyMailboxInto(string mailboxId, MailArchiveStore dest) {
    ArgumentNullException.ThrowIfNull(dest);
    foreach (var folder in ListFolders(mailboxId)) {
      foreach (var header in ListFolder(mailboxId, folder)) {
        var copy = ReadCopy(mailboxId, folder, header.Uid);
        if (copy is null)
          continue;
        dest.InsertCopy(mailboxId, folder, copy);
      }
    }
  }

  public void Dispose() {
    lock (_gate) {
      try {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        cmd.ExecuteNonQuery();
      }
      catch {
      }

      _db.Dispose();
    }
  }

  private uint NextUidUnlocked(string mailboxId, string folder) {
    using var cmd = _db.CreateCommand();
    cmd.CommandText = "SELECT COALESCE(MAX(uid), 0) FROM messages WHERE mailbox_id = $m AND folder = $f;";
    cmd.Parameters.AddWithValue("$m", mailboxId);
    cmd.Parameters.AddWithValue("$f", folder);
    var max = (long)(cmd.ExecuteScalar() ?? 0L);
    return (uint)max + 1;
  }

  private IReadOnlyList<MailArchiveHeader> SearchLike(string mailboxId, string folder, string text) {
    using var cmd = _db.CreateCommand();
    cmd.CommandText = """
      SELECT uid, folder, subject, from_addr, date_utc, is_seen, is_flagged, has_attachments,
             priority, envelope_kind, envelope_badge, envelope_tipo, message_id, in_reply_to, eml_path,
             (SELECT GROUP_CONCAT(name, ', ') FROM message_labels l
              WHERE l.mailbox_id = messages.mailbox_id AND l.folder = messages.folder AND l.uid = messages.uid)
      FROM messages
      WHERE mailbox_id = $m AND folder = $f
        AND (subject LIKE $q OR from_addr LIKE $q OR body_text LIKE $q OR attachment_text LIKE $q
             OR envelope_badge LIKE $q
             OR EXISTS (SELECT 1 FROM message_labels l
                        WHERE l.mailbox_id = messages.mailbox_id AND l.folder = messages.folder
                          AND l.uid = messages.uid AND l.name LIKE $q))
      ORDER BY date_utc DESC, uid DESC;
      """;
    cmd.Parameters.AddWithValue("$m", mailboxId);
    cmd.Parameters.AddWithValue("$f", folder);
    cmd.Parameters.AddWithValue("$q", "%" + text.Replace("%", "\\%").Replace("_", "\\_") + "%");
    return ReadHeaders(cmd);
  }

  private long UpsertHeader(
    SqliteTransaction tx,
    string mailboxId,
    MailArchiveHeader header,
    bool updateFlags) {
    using var cmd = _db.CreateCommand();
    cmd.Transaction = tx;
    var flagAssign = updateFlags
      ? """
        is_seen = excluded.is_seen,
        is_flagged = excluded.is_flagged,
        """
      : "";
    cmd.CommandText = $"""
      INSERT INTO messages (
        mailbox_id, folder, uid, message_id, subject, from_addr, date_utc, envelope_kind, envelope_badge,
        envelope_tipo, in_reply_to, is_seen, is_flagged, has_attachments, priority, trashed_utc)
      VALUES ($m, $f, $u, $mid, $sub, $from, $date, $kind, $badge, $tipo, $reply, $seen, $flag, $att, $pri, $trashed)
      ON CONFLICT(mailbox_id, folder, uid) DO UPDATE SET
        message_id = excluded.message_id,
        subject = excluded.subject,
        from_addr = excluded.from_addr,
        date_utc = excluded.date_utc,
        envelope_kind = excluded.envelope_kind,
        envelope_badge = excluded.envelope_badge,
        envelope_tipo = excluded.envelope_tipo,
        in_reply_to = excluded.in_reply_to,
        {flagAssign}
        has_attachments = excluded.has_attachments,
        priority = excluded.priority,
        trashed_utc = CASE
          WHEN messages.trashed_utc <> '' THEN messages.trashed_utc
          WHEN $trash = 1 THEN excluded.trashed_utc
          ELSE messages.trashed_utc
        END;
      """;
    var trash = MailRetention.IsTrash(header.Folder);
    var trashed = trash ? DateTimeOffset.UtcNow.ToString("O") : "";
    cmd.Parameters.AddWithValue("$m", mailboxId);
    cmd.Parameters.AddWithValue("$f", header.Folder);
    cmd.Parameters.AddWithValue("$u", header.Uid);
    cmd.Parameters.AddWithValue("$mid", header.MessageId);
    cmd.Parameters.AddWithValue("$sub", header.Subject);
    cmd.Parameters.AddWithValue("$from", header.From);
    cmd.Parameters.AddWithValue("$date", header.Date.ToString("O"));
    cmd.Parameters.AddWithValue("$kind", header.EnvelopeKind);
    cmd.Parameters.AddWithValue("$badge", header.EnvelopeBadge);
    cmd.Parameters.AddWithValue("$tipo", header.EnvelopeTipo);
    cmd.Parameters.AddWithValue("$reply", header.InReplyTo);
    cmd.Parameters.AddWithValue("$seen", header.IsSeen ? 1 : 0);
    cmd.Parameters.AddWithValue("$flag", header.IsFlagged ? 1 : 0);
    cmd.Parameters.AddWithValue("$att", header.HasAttachments ? 1 : 0);
    cmd.Parameters.AddWithValue("$pri", header.Priority);
    cmd.Parameters.AddWithValue("$trashed", trashed);
    cmd.Parameters.AddWithValue("$trash", trash ? 1 : 0);
    cmd.ExecuteNonQuery();
    using var idCmd = _db.CreateCommand();
    idCmd.Transaction = tx;
    idCmd.CommandText = "SELECT id FROM messages WHERE mailbox_id = $m AND folder = $f AND uid = $u;";
    idCmd.Parameters.AddWithValue("$m", mailboxId);
    idCmd.Parameters.AddWithValue("$f", header.Folder);
    idCmd.Parameters.AddWithValue("$u", header.Uid);
    var id = (long)(idCmd.ExecuteScalar() ?? 0L);
    IndexFts(tx, id, header, null, null);
    return id;
  }

  private void IndexFts(
    SqliteTransaction tx,
    long id,
    MailArchiveHeader header,
    string? bodyText,
    string? attachmentText) {
    if (!KeywordIndexEnabled)
      return;
    using (var del = _db.CreateCommand()) {
      del.Transaction = tx;
      del.CommandText = "DELETE FROM messages_fts WHERE rowid = $id;";
      del.Parameters.AddWithValue("$id", id);
      del.ExecuteNonQuery();
    }

    var body = bodyText;
    var att = attachmentText;
    if (body is null || att is null) {
      using var read = _db.CreateCommand();
      read.Transaction = tx;
      read.CommandText = "SELECT body_text, attachment_text FROM messages WHERE id = $id;";
      read.Parameters.AddWithValue("$id", id);
      using var reader = read.ExecuteReader();
      if (reader.Read()) {
        body ??= reader.IsDBNull(0) ? "" : reader.GetString(0);
        att ??= reader.IsDBNull(1) ? "" : reader.GetString(1);
      }
    }

    using var ins = _db.CreateCommand();
    ins.Transaction = tx;
    ins.CommandText = """
      INSERT INTO messages_fts(rowid, subject, from_addr, body_text, attachment_text, envelope_badge)
      VALUES ($id, $sub, $from, $body, $att, $badge);
      """;
    ins.Parameters.AddWithValue("$id", id);
    ins.Parameters.AddWithValue("$sub", header.Subject);
    ins.Parameters.AddWithValue("$from", header.From);
    ins.Parameters.AddWithValue("$body", body ?? "");
    ins.Parameters.AddWithValue("$att", att ?? "");
    ins.Parameters.AddWithValue("$badge", header.EnvelopeBadge);
    ins.ExecuteNonQuery();
  }

  private static List<MailArchiveHeader> ReadHeaders(SqliteCommand cmd) {
    var rows = new List<MailArchiveHeader>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
      rows.Add(ReadHeader(reader));
    return rows;
  }

  private static MailArchiveHeader ReadHeader(SqliteDataReader reader) {
    DateTimeOffset.TryParse(reader.GetString(4), out var date);
    return new MailArchiveHeader {
      Uid = (uint)reader.GetInt64(0),
      Folder = reader.GetString(1),
      Subject = reader.GetString(2),
      From = reader.GetString(3),
      Date = date,
      IsSeen = reader.GetInt64(5) != 0,
      IsFlagged = reader.GetInt64(6) != 0,
      HasAttachments = reader.GetInt64(7) != 0,
      Priority = reader.GetString(8),
      EnvelopeKind = reader.GetString(9),
      EnvelopeBadge = reader.GetString(10),
      EnvelopeTipo = reader.GetString(11),
      MessageId = reader.GetString(12),
      InReplyTo = reader.GetString(13),
      EmlPath = reader.IsDBNull(14) ? "" : reader.GetString(14),
      Labels = reader.FieldCount > 15 && !reader.IsDBNull(15) ? reader.GetString(15) : ""
    };
  }

  internal static string FtsQuery(string text) {
    var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length == 0)
      return "\"\"";
    return string.Join(" AND ", parts.Select(p => "\"" + p.Replace("\"", " ") + "\""));
  }

  private List<string> EmlPaths(string mailboxId, string folder, IReadOnlyCollection<uint>? uids = null) {
    using var cmd = _db.CreateCommand();
    cmd.CommandText = """
      SELECT eml_path FROM messages
      WHERE mailbox_id = $m AND folder = $f AND eml_path IS NOT NULL AND eml_path <> ''
      """ + UidFilter(cmd, uids) + ";";
    cmd.Parameters.AddWithValue("$m", mailboxId);
    cmd.Parameters.AddWithValue("$f", folder);
    var paths = new List<string>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
      paths.Add(reader.GetString(0));
    return paths;
  }

  private IReadOnlyList<MailArchiveHeader> MergeSemantic(
    string mailboxId,
    string folder,
    IReadOnlyList<MailArchiveHeader> lexical,
    float[] queryVector) {
    var query = EmbeddingVector.Truncate(queryVector, EmbeddingModelSpec.StoredDimensions);
    List<(float Score, MailArchiveHeader Header)> extra;
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = """
        SELECT m.uid, m.folder, m.subject, m.from_addr, m.date_utc, m.is_seen, m.is_flagged, m.has_attachments,
               m.priority, m.envelope_kind, m.envelope_badge, m.envelope_tipo, m.message_id, m.in_reply_to, m.eml_path,
               (SELECT GROUP_CONCAT(name, ', ') FROM message_labels l
                WHERE l.mailbox_id = m.mailbox_id AND l.folder = m.folder AND l.uid = m.uid),
               e.vector
        FROM message_embeddings e
        JOIN messages m ON m.id = e.message_id
        WHERE m.mailbox_id = $m AND m.folder = $f AND e.model_id = $model;
        """;
      cmd.Parameters.AddWithValue("$m", mailboxId);
      cmd.Parameters.AddWithValue("$f", folder);
      cmd.Parameters.AddWithValue("$model", EmbeddingModelSpec.Id);
      extra = [];
      using var reader = cmd.ExecuteReader();
      while (reader.Read()) {
        var blob = reader.IsDBNull(16) ? null : reader.GetFieldValue<byte[]>(16);
        if (blob is null || blob.Length == 0)
          continue;
        var vector = EmbeddingVector.FromBlob(blob);
        var score = EmbeddingVector.Cosine(query, vector);
        if (score < EmbeddingModelSpec.MinScore)
          continue;
        extra.Add((score, ReadHeader(reader)));
      }
    }

    if (extra.Count == 0)
      return lexical;

    var seen = new HashSet<uint>(lexical.Select(h => h.Uid));
    extra.Sort((a, b) => b.Score.CompareTo(a.Score));
    var merged = new List<MailArchiveHeader>(lexical);
    var added = 0;
    foreach (var row in extra) {
      if (!seen.Add(row.Header.Uid))
        continue;
      merged.Add(row.Header);
      added++;
      if (added >= EmbeddingModelSpec.VectorTopK)
        break;
    }

    return merged;
  }

  private void DeleteEmbeddings(
    SqliteTransaction tx,
    string mailboxId,
    string folder,
    IReadOnlyCollection<uint>? uids = null) {
    using var cmd = _db.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = """
      DELETE FROM message_embeddings
      WHERE message_id IN (
        SELECT id FROM messages WHERE mailbox_id = $m AND folder = $f
      """ + UidFilter(cmd, uids) + """
      );
      """;
    cmd.Parameters.AddWithValue("$m", mailboxId);
    cmd.Parameters.AddWithValue("$f", folder);
    cmd.ExecuteNonQuery();
  }

  private void DeleteFts(
    SqliteTransaction tx,
    string mailboxId,
    string folder,
    IReadOnlyCollection<uint>? uids = null) {
    using var cmd = _db.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = """
      DELETE FROM messages_fts
      WHERE rowid IN (
        SELECT id FROM messages WHERE mailbox_id = $m AND folder = $f
      """ + UidFilter(cmd, uids) + """
      );
      """;
    cmd.Parameters.AddWithValue("$m", mailboxId);
    cmd.Parameters.AddWithValue("$f", folder);
    cmd.ExecuteNonQuery();
  }

  private ArchiveIndexStats ReadIndexStats(string modelId) {
    var messages = Scalar("SELECT COUNT(*) FROM messages;");
    var keyword = Scalar("SELECT COUNT(*) FROM messages_fts;");
    int meaning;
    using (var cmd = _db.CreateCommand()) {
      cmd.CommandText = "SELECT COUNT(*) FROM message_embeddings WHERE model_id = $model;";
      cmd.Parameters.AddWithValue("$model", modelId);
      meaning = (int)(long)(cmd.ExecuteScalar() ?? 0L);
    }

    int pending;
    using (var cmd = _db.CreateCommand()) {
      cmd.CommandText = """
        SELECT COUNT(*)
        FROM messages m
        LEFT JOIN message_embeddings e ON e.message_id = m.id
        WHERE (e.message_id IS NULL OR e.model_id <> $model)
          AND (m.body_text <> '' OR m.subject <> '');
        """;
      cmd.Parameters.AddWithValue("$model", modelId);
      pending = (int)(long)(cmd.ExecuteScalar() ?? 0L);
    }

    var ftsOrphans = Scalar("SELECT COUNT(*) FROM messages_fts WHERE rowid NOT IN (SELECT id FROM messages);");
    var embeddingOrphans = Scalar("""
      SELECT COUNT(*) FROM message_embeddings
      WHERE message_id NOT IN (SELECT id FROM messages);
      """);
    return new ArchiveIndexStats(messages, keyword, meaning, pending, ftsOrphans + embeddingOrphans);
  }

  private int Scalar(string sql) {
    using var cmd = _db.CreateCommand();
    cmd.CommandText = sql;
    return (int)(long)(cmd.ExecuteScalar() ?? 0L);
  }

  private static string UidFilter(SqliteCommand cmd, IReadOnlyCollection<uint>? uids) {
    if (uids is null)
      return "";
    var names = new List<string>();
    var i = 0;
    foreach (var uid in uids) {
      var name = "$uid" + i++;
      names.Add(name);
      cmd.Parameters.AddWithValue(name, uid);
    }

    return names.Count == 0 ? " AND 0" : " AND uid IN (" + string.Join(", ", names) + ")";
  }

  private void EnsureSchema() {
    using var cmd = _db.CreateCommand();
    cmd.CommandText = """
      CREATE TABLE IF NOT EXISTS messages (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        mailbox_id TEXT NOT NULL,
        folder TEXT NOT NULL,
        uid INTEGER NOT NULL,
        message_id TEXT NOT NULL DEFAULT '',
        subject TEXT NOT NULL DEFAULT '',
        from_addr TEXT NOT NULL DEFAULT '',
        date_utc TEXT NOT NULL DEFAULT '',
        envelope_kind TEXT NOT NULL DEFAULT '',
        envelope_badge TEXT NOT NULL DEFAULT '',
        envelope_tipo TEXT NOT NULL DEFAULT '',
        in_reply_to TEXT NOT NULL DEFAULT '',
        is_seen INTEGER NOT NULL DEFAULT 0,
        is_flagged INTEGER NOT NULL DEFAULT 0,
        has_attachments INTEGER NOT NULL DEFAULT 0,
        priority TEXT NOT NULL DEFAULT '',
        eml_path TEXT NOT NULL DEFAULT '',
        body_text TEXT NOT NULL DEFAULT '',
        attachment_text TEXT NOT NULL DEFAULT '',
        trashed_utc TEXT NOT NULL DEFAULT '',
        UNIQUE(mailbox_id, folder, uid)
      );
      CREATE VIRTUAL TABLE IF NOT EXISTS messages_fts USING fts5(
        subject, from_addr, body_text, attachment_text, envelope_badge
      );
      CREATE TABLE IF NOT EXISTS message_labels (
        mailbox_id TEXT NOT NULL,
        folder TEXT NOT NULL,
        uid INTEGER NOT NULL,
        name TEXT NOT NULL,
        PRIMARY KEY (mailbox_id, folder, uid, name)
      );
      CREATE TABLE IF NOT EXISTS message_embeddings (
        message_id INTEGER PRIMARY KEY,
        model_id TEXT NOT NULL,
        dims INTEGER NOT NULL,
        vector BLOB NOT NULL
      );
      """;
    cmd.ExecuteNonQuery();
    EnsureColumn("messages", "trashed_utc", "TEXT NOT NULL DEFAULT ''");
  }

  private void EnsureColumn(string table, string column, string definition) {
    using var info = _db.CreateCommand();
    info.CommandText = "PRAGMA table_info(" + table + ");";
    using var reader = info.ExecuteReader();
    while (reader.Read()) {
      if (reader.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase))
        return;
    }

    reader.Dispose();
    using var alter = _db.CreateCommand();
    alter.CommandText = "ALTER TABLE " + table + " ADD COLUMN " + column + " " + definition + ";";
    alter.ExecuteNonQuery();
  }
}


public readonly record struct MailArchiveUid(string MailboxId, string Folder, uint Uid);


public readonly record struct EmbeddingWorkItem(
  string MailboxId,
  long MessageId,
  string Subject,
  string From,
  string Body,
  string Attachments);


public readonly record struct ArchiveIndexStats(
  int Messages,
  int KeywordRows,
  int MeaningRows,
  int MeaningPending,
  int Orphans);


public sealed class MailArchiveHeader {
  public string MailboxId { get; set; } = "";

  public uint Uid { get; init; }

  public string Folder { get; init; } = "";

  public string Subject { get; init; } = "";

  public string From { get; init; } = "";

  public DateTimeOffset Date { get; init; }

  public bool IsSeen { get; init; }

  public bool IsFlagged { get; init; }

  public bool HasAttachments { get; init; }

  public string Priority { get; init; } = "";

  public string EnvelopeKind { get; init; } = "";

  public string EnvelopeBadge { get; init; } = "";

  public string EnvelopeTipo { get; init; } = "";

  public string MessageId { get; init; } = "";

  public string InReplyTo { get; init; } = "";

  public string EmlPath { get; init; } = "";

  public string Labels { get; init; } = "";
}
