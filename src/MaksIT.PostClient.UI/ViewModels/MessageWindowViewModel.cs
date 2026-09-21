using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace MaksIT.PostClient.UI.ViewModels;


public partial class MessageWindowViewModel : ObservableObject {
  private readonly MailMessageBody _body;
  private readonly IReadOnlyList<ComposeIdentity> _fromAccounts;
  private readonly ComposeIdentity _from;
  private readonly Func<ComposeIdentity, CancellationToken, Task<IMailSession?>>? _ensureFrom;
  private readonly string _preference;
  private bool _syncingKind;
  private bool _htmlReady;

  [ObservableProperty]
  private string readingSubject = "";

  [ObservableProperty]
  private string readingFrom = "";

  [ObservableProperty]
  private string readingTo = "";

  [ObservableProperty]
  private string readingCc = "";

  [ObservableProperty]
  private string readingDate = "";

  [ObservableProperty]
  private string readingBody = "";

  [ObservableProperty]
  private string readingHtmlDocument = "";

  [ObservableProperty]
  private string readingSource = "";

  [ObservableProperty]
  private string readingEnvelope = "";

  [ObservableProperty]
  private bool hasReadingCc;

  [ObservableProperty]
  private bool hasReadingEnvelope;

  [ObservableProperty]
  private bool hasReadingFiles;

  [ObservableProperty]
  private bool unwrapEnvelope;

  [ObservableProperty]
  private string readingBodyKind = MailBodyKind.Html;

  public string Title =>
    string.IsNullOrWhiteSpace(ReadingSubject) ? Copy.MessageTitle : ReadingSubject;

  public UiCopy Copy =>
    UiLocale.Copy;

  public bool HtmlEngineReady =>
    _htmlReady;

  public bool ShowHtmlBody =>
    _htmlReady && MailBodyKind.IsHtml(ReadingBodyKind);

  public bool ShowTextBody =>
    MailBodyKind.IsText(ReadingBodyKind)
    || (!_htmlReady && MailBodyKind.IsHtml(ReadingBodyKind));

  public bool ShowSourceBody =>
    MailBodyKind.IsSource(ReadingBodyKind);

  public bool IsHtmlKind =>
    MailBodyKind.IsHtml(ReadingBodyKind);

  public bool IsTextKind =>
    MailBodyKind.IsText(ReadingBodyKind);

  public bool IsSourceKind =>
    MailBodyKind.IsSource(ReadingBodyKind);

  public bool IsFatturaKind =>
    MailBodyKind.IsFattura(ReadingBodyKind);

  public bool ShowFatturaBody =>
    UseFatturaPa && MailBodyKind.IsFattura(ReadingBodyKind);

  public bool UseFatturaPa =>
    FeatureGate.On(AppFeature.FatturaPa);

  public bool UseEnvelopeTools =>
    FeatureGate.On(AppFeature.PecEnvelope) || FeatureGate.On(AppFeature.RemEvidence);

  public bool ShowFatturaPaTools =>
    HasReadingFattura && UseFatturaPa;

  public bool HasReadingFattura { get; private set; }

  public string ReadingFattura { get; private set; } = "";

  public ObservableCollection<MailFileAttachment> ReadingFiles { get; } = [];

  public event Action<ComposeViewModel>? ComposeRequested;

  public event Action<byte[], string>? SaveAttachmentsZipRequested;

  public MessageWindowViewModel(
    MailMessageBody body,
    IReadOnlyList<ComposeIdentity> fromAccounts,
    ComposeIdentity from,
    bool unwrap,
    string preference,
    bool htmlReady,
    Func<ComposeIdentity, CancellationToken, Task<IMailSession?>>? ensureFrom = null) {
    _body = body;
    _fromAccounts = fromAccounts;
    _from = from;
    _ensureFrom = ensureFrom;
    _preference = MailBodyKind.Preference(preference);
    _htmlReady = htmlReady;
    unwrapEnvelope = unwrap;
    readingBodyKind = MailBodyKind.DefaultView(
      unwrap && body.Envelope.HasInnerMessage ? body.InnerHtml : body.Html,
      unwrap && body.Envelope.HasInnerMessage ? body.InnerText : body.Text,
      _preference);
    ApplyReading();
    UiLocale.Changed += OnLocaleChanged;
  }

  public void Detach() =>
    UiLocale.Changed -= OnLocaleChanged;

  private void OnLocaleChanged() {
    OnPropertyChanged(nameof(Copy));
    OnPropertyChanged(nameof(Title));
  }

  public void DisableHtmlEngine() {
    if (!_htmlReady)
      return;
    _htmlReady = false;
    NotifyBodyKind();
  }

  [RelayCommand]
  private void SetReadingKind(string? kind) {
    var next = MailBodyKind.Normalize(kind);
    if (MailBodyKind.IsFattura(next) && !UseFatturaPa)
      next = MailBodyKind.Html;
    ReadingBodyKind = next;
  }

  [RelayCommand]
  private void ViewFatturaPa() {
    if (UseFatturaPa)
      SetReadingKind(MailBodyKind.Fattura);
  }

  [RelayCommand]
  private void Reply() =>
    OpenCompose("Reply", Recipients(ReadingFrom), null, ReplySubject(), QuotedBody());

  [RelayCommand]
  private void ReplyAll() {
    var to = Distinct(Recipients(ReadingFrom).Concat(Recipients(ReadingTo)), _from.Account.Address);
    var skip = new HashSet<string>(to.Select(r => r.Address), StringComparer.OrdinalIgnoreCase);
    var cc = Distinct(Recipients(ReadingCc), _from.Account.Address).Where(r => !skip.Contains(r.Address)).ToList();
    OpenCompose("Reply All", to, cc, ReplySubject(), QuotedBody());
  }

  [RelayCommand]
  private void Forward() {
    var subject = _body.Header.Subject.StartsWith("Fwd:", StringComparison.OrdinalIgnoreCase)
      ? _body.Header.Subject
      : "Fwd: " + _body.Header.Subject;
    OpenCompose("Forward", null, null, subject, QuotedBody());
  }

  [RelayCommand(CanExecute = nameof(HasReadingAttachments))]
  private void SaveAttachmentsZip() {
    if (ReadingFiles.Count == 0)
      return;
    SaveAttachmentsZipRequested?.Invoke(
      AttachmentZip.FromFiles(ReadingFiles),
      AttachmentZip.SuggestedName(ReadingSubject));
  }

  private bool HasReadingAttachments() =>
    HasReadingFiles;

  partial void OnHasReadingFilesChanged(bool value) =>
    SaveAttachmentsZipCommand.NotifyCanExecuteChanged();

  partial void OnUnwrapEnvelopeChanged(bool value) =>
    ApplyReading(true);

  partial void OnReadingBodyKindChanged(string value) {
    if (_syncingKind)
      return;
    NotifyBodyKind();
    ApplyReading(true);
  }

  partial void OnReadingSubjectChanged(string value) =>
    OnPropertyChanged(nameof(Title));

  private void ApplyReading(bool keepKind = false) {
    var inner = UnwrapEnvelope && _body.Envelope.HasInnerMessage;
    ReadingSubject = inner && !string.IsNullOrWhiteSpace(_body.InnerSubject)
      ? _body.InnerSubject
      : _body.Header.Subject;
    ReadingFrom = inner && !string.IsNullOrWhiteSpace(_body.InnerFrom)
      ? _body.InnerFrom
      : _body.Header.From;
    ReadingTo = inner && !string.IsNullOrWhiteSpace(_body.InnerTo)
      ? _body.InnerTo
      : _body.To;
    ReadingCc = inner && !string.IsNullOrWhiteSpace(_body.InnerCc)
      ? _body.InnerCc
      : _body.Cc;
    HasReadingCc = !string.IsNullOrWhiteSpace(ReadingCc);
    ReadingDate = _body.Header.Date == DateTimeOffset.MinValue
      ? ""
      : _body.Header.Date.ToLocalTime().ToString("f");
    var files = inner ? _body.InnerAttachmentFiles : _body.AttachmentFiles;
    var html = inner ? _body.InnerHtml : _body.Html;
    var text = inner ? _body.InnerText : _body.Text;
    var markup = MessageHtml.HasMarkup(html);
    if (string.IsNullOrWhiteSpace(text))
      text = MessageText.StripHtml(html);
    if (markup) {
      var cid = MessageHtml.DataUris(files.Select(f => (f.ContentId, f.ContentType, f.Bytes)));
      ReadingHtmlDocument = MessageHtml.Document(MessageHtml.InlineCid(html, cid));
    }
    else if (keepKind && MailBodyKind.IsHtml(ReadingBodyKind))
      ReadingHtmlDocument = MessageHtml.Document(MessageHtml.FromPlain(text));
    else
      ReadingHtmlDocument = "";
    ReadingBody = text ?? "";
    ReadingSource = _body.RawEml.Length == 0
      ? ""
      : System.Text.Encoding.UTF8.GetString(_body.RawEml);
    ReadingEnvelope = _body.Envelope.Line;
    HasReadingEnvelope = !string.IsNullOrWhiteSpace(ReadingEnvelope);
    ReadingFiles.Clear();
    foreach (var file in files) {
      if (IsInlineImage(file, html ?? ""))
        continue;
      ReadingFiles.Add(file);
    }
    HasReadingFiles = ReadingFiles.Count > 0;
    var fattura = FatturaPaDocument.FromAttachments(files);
    ReadingFattura = fattura?.Text ?? "";
    HasReadingFattura = fattura is not null;
    OnPropertyChanged(nameof(ReadingFattura));
    OnPropertyChanged(nameof(HasReadingFattura));
    OnPropertyChanged(nameof(ShowFatturaPaTools));
    if (!keepKind) {
      _syncingKind = true;
      ReadingBodyKind = fattura is not null && UseFatturaPa
        ? MailBodyKind.Fattura
        : MailBodyKind.DefaultView(html, inner ? _body.InnerText : _body.Text, _preference);
      _syncingKind = false;
    }

    NotifyBodyKind();
  }

  private void NotifyBodyKind() {
    OnPropertyChanged(nameof(ShowHtmlBody));
    OnPropertyChanged(nameof(ShowTextBody));
    OnPropertyChanged(nameof(ShowSourceBody));
    OnPropertyChanged(nameof(ShowFatturaBody));
    OnPropertyChanged(nameof(IsHtmlKind));
    OnPropertyChanged(nameof(IsTextKind));
    OnPropertyChanged(nameof(IsSourceKind));
    OnPropertyChanged(nameof(IsFatturaKind));
    OnPropertyChanged(nameof(HtmlEngineReady));
  }

  private void OpenCompose(
    string title,
    IReadOnlyList<MailRecipient>? to,
    IReadOnlyList<MailRecipient>? cc,
    string subject,
    string body) {
    var compose = new ComposeViewModel(_fromAccounts, _from, title, _ensureFrom);
    if (to is { Count: > 0 })
      compose.ToField.AddFromText(string.Join(", ", to.Select(r => r.Tooltip)));
    if (cc is { Count: > 0 })
      compose.CcField.AddFromText(string.Join(", ", cc.Select(r => r.Tooltip)));
    compose.Subject = subject;
    compose.Body = body;
    if (title.StartsWith("Reply", StringComparison.OrdinalIgnoreCase)) {
      compose.InReplyTo = _body.Header.MessageId;
      compose.References = MailId.ThreadLine(_body.Header.InReplyTo, _body.Header.MessageId);
    }

    ComposeRequested?.Invoke(compose);
  }

  private string ReplySubject() =>
    _body.Header.Subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase)
      ? _body.Header.Subject
      : "Re: " + _body.Header.Subject;

  private string QuotedBody() =>
    string.IsNullOrWhiteSpace(ReadingBody) ? "" : "\n\n--- Original message ---\n" + ReadingBody;

  private static IReadOnlyList<MailRecipient> Recipients(string? text) =>
    RecipientParser.Parse(text);

  private static List<MailRecipient> Distinct(IEnumerable<MailRecipient> rows, string self) =>
    rows
      .Where(r => r.IsValid && !r.Address.Equals(self, StringComparison.OrdinalIgnoreCase))
      .DistinctBy(r => r.Address, StringComparer.OrdinalIgnoreCase)
      .ToList();

  private static bool IsInlineImage(MailFileAttachment file, string html) {
    if (string.IsNullOrWhiteSpace(file.ContentId))
      return false;
    if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
      return false;
    return html.Contains("cid:" + file.ContentId, StringComparison.OrdinalIgnoreCase);
  }
}
