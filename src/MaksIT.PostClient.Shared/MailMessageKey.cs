namespace MaksIT.PostClient.Shared;


public readonly struct MailMessageKey : IEquatable<MailMessageKey> {
  public string MailboxId { get; }

  public string Folder { get; }

  public uint Id { get; }

  public MailMessageKey(string mailboxId, string folder, uint id) {
    MailboxId = mailboxId ?? "";
    Folder = folder ?? "";
    Id = id;
  }

  public static MailMessageKey Of(string? mailboxId, string? folder, uint id) =>
    new((mailboxId ?? "").Trim(), (folder ?? "").Trim(), id);

  public bool Equals(MailMessageKey other) =>
    Id == other.Id
    && MailboxId.Equals(other.MailboxId, StringComparison.OrdinalIgnoreCase)
    && Folder.Equals(other.Folder, StringComparison.OrdinalIgnoreCase);

  public override bool Equals(object? obj) =>
    obj is MailMessageKey other && Equals(other);

  public override int GetHashCode() =>
    HashCode.Combine(
      StringComparer.OrdinalIgnoreCase.GetHashCode(MailboxId),
      StringComparer.OrdinalIgnoreCase.GetHashCode(Folder),
      Id);

  public static bool operator ==(MailMessageKey left, MailMessageKey right) =>
    left.Equals(right);

  public static bool operator !=(MailMessageKey left, MailMessageKey right) =>
    !left.Equals(right);
}
