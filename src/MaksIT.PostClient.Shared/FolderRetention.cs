namespace MaksIT.PostClient.Shared;


public sealed class FolderRetention {
  public string MailboxId { get; set; } = "";

  public string Folder { get; set; } = "";

  public int Days { get; set; }
}
