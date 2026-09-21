using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace MaksIT.PostClient.UI.ViewModels;


public sealed class ComposeFileRow {
  public required string Name { get; init; }

  public required string Path { get; init; }

  public required byte[] Bytes { get; init; }

  public string ContentType { get; init; } = "application/octet-stream";

  public string Line =>
    $"{Name} ({Bytes.Length / 1024.0:0.#} KB)";
}


public sealed class ComposeIdentity {
  public required MailboxAccount Account { get; init; }

  public IMailSession? Session { get; set; }

  public string Line =>
    Account.Label + " — " + Account.Address;

  public override string ToString() =>
    Line;
}


public partial class ComposeViewModel : ObservableObject {
  public const long MaxBytes = 25 * 1024 * 1024;

  private readonly Func<ComposeIdentity, CancellationToken, Task<IMailSession?>>? _ensureSession;

  [ObservableProperty]
  private ComposeIdentity? selectedFrom;

  [ObservableProperty]
  private string subject = "";

  [ObservableProperty]
  private string body = "";

  [ObservableProperty]
  private string status = "";

  [ObservableProperty]
  private bool isBusy;

  public string Title { get; }

  public string InReplyTo { get; set; } = "";

  public string References { get; set; } = "";

  public RecipientFieldViewModel ToField { get; } = new();

  public RecipientFieldViewModel CcField { get; } = new();

  public RecipientFieldViewModel BccField { get; } = new();

  public ObservableCollection<ComposeFileRow> Files { get; } = [];

  public ObservableCollection<ComposeIdentity> FromAccounts { get; } = [];

  [ObservableProperty]
  private bool zipAttachments;

  public bool HasFiles =>
    Files.Count > 0;

  public event Func<ZipSendRequest, Task<ZipSendRequest?>>? ZipOptionsRequested;

  public UiCopy Copy =>
    UiLocale.Copy;

  public ComposeViewModel(
    IReadOnlyList<ComposeIdentity> identities,
    ComposeIdentity selected,
    string title,
    Func<ComposeIdentity, CancellationToken, Task<IMailSession?>>? ensureSession = null) {
    Title = title;
    _ensureSession = ensureSession;
    foreach (var row in identities)
      FromAccounts.Add(row);
    SelectedFrom = selected;
    Status = Copy.FromMailboxTip;
    Files.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasFiles));
    UiLocale.Changed += OnLocaleChanged;
  }

  public void Detach() =>
    UiLocale.Changed -= OnLocaleChanged;

  private void OnLocaleChanged() =>
    OnPropertyChanged(nameof(Copy));

  public string? AddFile(string path) {
    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
      return "File not found.";
    var info = new FileInfo(path);
    if (info.Length > MaxBytes)
      return "Attachment is larger than 25 MB.";
    Files.Add(new ComposeFileRow {
      Name = info.Name,
      Path = path,
      Bytes = File.ReadAllBytes(path),
      ContentType = GuessType(info.Name)
    });
    return null;
  }

  [RelayCommand]
  private void RemoveFile(ComposeFileRow? row) {
    if (row is not null)
      Files.Remove(row);
  }

  [RelayCommand]
  private async Task SendAsync() {
    ToField.CommitDraft();
    CcField.CommitDraft();
    BccField.CommitDraft();
    if (!ToField.HasValid) {
      Status = "To is required.";
      return;
    }

    if (SelectedFrom is not { } identity) {
      Status = "Select a mailbox in From.";
      return;
    }

    var attachments = await AttachmentsForSendAsync();
    if (attachments is null)
      return;

    IsBusy = true;
    Status = "Sending as " + identity.Line + "…";
    try {
      var session = identity.Session is { IsConnected: true } live
        ? live
        : _ensureSession is null
          ? null
          : await _ensureSession(identity, CancellationToken.None);
      if (session is not { IsConnected: true }) {
        Status = "Connect or save the password for " + identity.Account.Label + " first.";
        return;
      }

      identity.Session = session;
      var request = new MailSendRequest {
        From = identity.Account.Address,
        To = ToField.Snapshot(),
        Cc = CcField.Snapshot(),
        Bcc = BccField.Snapshot(),
        Subject = Subject ?? "",
        BodyText = Body ?? "",
        InReplyTo = InReplyTo,
        References = References,
        Attachments = attachments
      };
      var result = await session.SendAsync(request);
      if (!result.IsSuccess) {
        Status = string.Join(" ", result.Messages);
        return;
      }

      Status = identity.Account.TracksCertifiedReceipts
        ? string.Format(Copy.SentFromCertified, identity.Account.Label)
        : string.Format(Copy.SentFrom, identity.Account.Label);
      Sent?.Invoke(request, result.Value ?? "", identity.Account.Id);
    }
    catch (Exception ex) {
      Status = ex.Message;
    }
    finally {
      IsBusy = false;
    }
  }

  public event Action<MailSendRequest, string, string>? Sent;

  private async Task<IReadOnlyList<MailFileAttachment>?> AttachmentsForSendAsync() {
    var files = Files
      .Select(f => new MailFileAttachment {
        Name = f.Name,
        Bytes = f.Bytes,
        ContentType = f.ContentType
      })
      .ToList();
    if (!ZipAttachments || files.Count == 0)
      return files;
    var prompt = new ZipSendRequest { FileName = AttachmentZip.SuggestedName(Subject) };
    if (ZipOptionsRequested is not null) {
      var chosen = await ZipOptionsRequested(prompt);
      if (chosen is null)
        return null;
      prompt = chosen;
    }

    var zip = AttachmentZip.FromFiles(files, string.IsNullOrWhiteSpace(prompt.Password) ? null : prompt.Password);
    return [
      new MailFileAttachment {
        Name = AttachmentZip.FileName(prompt.FileName),
        Bytes = zip,
        ContentType = "application/zip"
      }
    ];
  }

  private static string GuessType(string name) {
    var ext = Path.GetExtension(name).ToLowerInvariant();
    return ext switch {
      ".pdf" => "application/pdf",
      ".png" => "image/png",
      ".jpg" or ".jpeg" => "image/jpeg",
      ".txt" => "text/plain",
      ".xml" => "application/xml",
      ".eml" => "message/rfc822",
      ".zip" => "application/zip",
      _ => "application/octet-stream"
    };
  }
}


public sealed class ZipSendRequest {
  public string FileName { get; set; } = "";

  public string Password { get; set; } = "";
}
