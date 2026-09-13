using Microsoft.Data.Sqlite;


namespace MaksIT.PostClient.Shared;


public sealed class MailArchiveStore : IDisposable {
  private readonly SqliteConnection _db;
  private readonly Lock _gate = new();

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

  public IReadOnlyList<MailArchiveUid> MissingBodies(string mailboxId) {
    lock (_gate) {
      using var cmd = _db.CreateCommand();
      cmd.CommandText = """
        SELECT folder, uid FROM messages
        WHERE mailbox_id = $m AND (eml_path IS NULL OR eml_path = '')
        ORDER BY date_utc DESC, uid DESC;
        """;
      cmd.Parameters.AddWithValue("$m", mailboxId);
      var rows = new List<MailArchiveUid>();
      using var reader = cmd.ExecuteReader();
      while (reader.Read())
        rows.Add(new MailArchiveUid(reader.GetString(0), (uint)reader.GetInt64(1)));
      return rows;
    }
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

  public IReadOnlyList<MailArchiveHeader> Search(string mailboxId, string folder, string query) {
    var text = query.Trim();
    if (text.Length == 0)
      return ListFolder(mailboxId, folder);

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
        return ReadHeaders(cmd);
      }
      catch (SqliteException) {
        return SearchLike(mailboxId, folder, text);
      }
    }
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
        envelope_tipo, in_reply_to, is_seen, is_flagged, has_attachments, priority)
      VALUES ($m, $f, $u, $mid, $sub, $from, $date, $kind, $badge, $tipo, $reply, $seen, $flag, $att, $pri)
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
        priority = excluded.priority;
      """;
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
    while (reader.Read()) {
      DateTimeOffset.TryParse(reader.GetString(4), out var date);
      rows.Add(new MailArchiveHeader {
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
      });
    }

    return rows;
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
      """;
    cmd.ExecuteNonQuery();
  }
}


public readonly record struct MailArchiveUid(string Folder, uint Uid);


public sealed class MailArchiveHeader {
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
