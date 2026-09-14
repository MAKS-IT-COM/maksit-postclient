namespace MaksIT.PostClient.Shared;


public sealed class SentDispatch {
  public string MailboxId { get; set; } = "";

  public string MessageId { get; set; } = "";

  public string Identificativo { get; set; } = "";

  public string Subject { get; set; } = "";

  public List<string> To { get; set; } = [];

  public DateTimeOffset SentAt { get; set; }

  public string Status { get; set; } = ReceiptStatus.Submitted;

  public string LastTipo { get; set; } = "";
}


public interface ISentReceiptStore {
  void Remember(SentDispatch dispatch);

  void ApplyEvidence(
    string mailboxId,
    string? relatedMessageId,
    string? identificativo,
    string? subject,
    string? tipo);

  string StatusFor(string mailboxId, string? messageId, string? subject = null);

  void ForgetMailbox(string mailboxId);
}
