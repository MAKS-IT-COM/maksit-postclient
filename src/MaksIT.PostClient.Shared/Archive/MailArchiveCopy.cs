namespace MaksIT.PostClient.Shared.Archive;


public sealed class MailArchiveCopy {
  public MailArchiveHeader Header { get; init; } = new();

  public string BodyText { get; init; } = "";

  public string AttachmentText { get; init; } = "";

  public IReadOnlyList<string> Labels { get; init; } = [];

  public string ModelId { get; init; } = "";

  public float[]? Vector { get; init; }
}
