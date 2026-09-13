using System.Text.Json;


namespace MaksIT.PostClient.Shared;


public sealed class FileSentReceiptStore : ISentReceiptStore {
  private static readonly JsonSerializerOptions Json = new() {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
  };
  private readonly string _path;
  private readonly Lock _gate = new();

  public FileSentReceiptStore(string? path = null) {
    _path = string.IsNullOrWhiteSpace(path) ? AppPaths.ReceiptsFile() : path;
  }

  public void Remember(SentDispatch dispatch) {
    ArgumentNullException.ThrowIfNull(dispatch);
    var messageId = ReceiptStatus.NormalizeId(dispatch.MessageId);
    if (string.IsNullOrWhiteSpace(dispatch.MailboxId) || messageId.Length == 0)
      return;
    lock (_gate) {
      var items = Load();
      var row = Find(items, dispatch.MailboxId, messageId, dispatch.Identificativo, dispatch.Subject);
      if (row is null) {
        dispatch.MessageId = messageId;
        dispatch.Status = ReceiptStatus.Combine(ReceiptStatus.Submitted, dispatch.Status);
        items.Add(dispatch);
      }
      else {
        if (string.IsNullOrWhiteSpace(row.Subject))
          row.Subject = dispatch.Subject;
        if (row.To.Count == 0)
          row.To = dispatch.To;
      }

      Save(items);
    }
  }

  public void ApplyEvidence(
    string mailboxId,
    string? relatedMessageId,
    string? identificativo,
    string? subject,
    string? tipo) {
    if (string.IsNullOrWhiteSpace(mailboxId))
      return;
    var status = ReceiptStatus.FromTipo(tipo);
    var related = ReceiptStatus.NormalizeId(relatedMessageId);
    lock (_gate) {
      var items = Load();
      var row = Find(items, mailboxId, related, identificativo, subject);
      if (row is null && related.Length > 0) {
        row = new SentDispatch {
          MailboxId = mailboxId,
          MessageId = related,
          Subject = subject ?? "",
          SentAt = DateTimeOffset.UtcNow,
          Status = ReceiptStatus.Submitted
        };
        items.Add(row);
      }

      if (row is null)
        return;
      if (!string.IsNullOrWhiteSpace(identificativo) && string.IsNullOrWhiteSpace(row.Identificativo))
        row.Identificativo = identificativo.Trim();
      if (!string.IsNullOrWhiteSpace(related) && string.IsNullOrWhiteSpace(row.MessageId))
        row.MessageId = related;
      if (status.Length > 0) {
        row.Status = ReceiptStatus.Combine(row.Status, status);
        row.LastTipo = (tipo ?? "").Trim();
      }

      Save(items);
    }
  }

  public string StatusFor(string mailboxId, string? messageId, string? subject = null) {
    if (string.IsNullOrWhiteSpace(mailboxId))
      return "";
    lock (_gate) {
      var row = Find(Load(), mailboxId, ReceiptStatus.NormalizeId(messageId), null, subject);
      return row?.Status ?? "";
    }
  }

  private static SentDispatch? Find(
    List<SentDispatch> items,
    string mailboxId,
    string? messageId,
    string? identificativo,
    string? subject) {
    var id = ReceiptStatus.NormalizeId(messageId);
    var cert = (identificativo ?? "").Trim();
    foreach (var row in items) {
      if (!row.MailboxId.Equals(mailboxId, StringComparison.OrdinalIgnoreCase))
        continue;
      if (id.Length > 0 && ReceiptStatus.NormalizeId(row.MessageId).Equals(id, StringComparison.OrdinalIgnoreCase))
        return row;
      if (cert.Length > 0 && row.Identificativo.Equals(cert, StringComparison.OrdinalIgnoreCase))
        return row;
    }

    var topic = (subject ?? "").Trim();
    if (topic.Length < 8)
      return null;
    foreach (var row in items) {
      if (!row.MailboxId.Equals(mailboxId, StringComparison.OrdinalIgnoreCase))
        continue;
      if (row.Subject.Length < 8)
        continue;
      if (topic.Contains(row.Subject, StringComparison.OrdinalIgnoreCase)
          || row.Subject.Contains(StripReceiptPrefix(topic), StringComparison.OrdinalIgnoreCase))
        return row;
    }

    return null;
  }

  private static string StripReceiptPrefix(string subject) {
    foreach (var prefix in new[] {
      "ACCETTAZIONE:", "CONSEGNA:", "AVVENUTA CONSEGNA:", "POSTA CERTIFICATA:",
      "NON ACCETTAZIONE:", "ERRORE CONSEGNA:", "MANCATA CONSEGNA:",
      "ANOMALIA MESSAGGIO:", "AVVISO DI MANCATA CONSEGNA:",
      "AVIS DE RECEPTION:", "ACCUSE DE RECEPTION:", "ACUSE DE RECIBO:",
      "ZUSTELLBESTAETIGUNG:", "ABHOLBESTAETIGUNG:", "RETRIEVAL:"
    }) {
      if (subject.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        return subject[prefix.Length..].Trim();
    }

    return subject;
  }

  private List<SentDispatch> Load() {
    if (!File.Exists(_path))
      return [];
    try {
      return JsonSerializer.Deserialize<List<SentDispatch>>(File.ReadAllText(_path), Json) ?? [];
    }
    catch {
      return [];
    }
  }

  private void Save(List<SentDispatch> items) {
    var dir = Path.GetDirectoryName(_path);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);
    File.WriteAllText(_path, JsonSerializer.Serialize(items, Json));
    if (!OperatingSystem.IsWindows())
      File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
  }
}
