using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaksIT.Results;
using MaksIT.PostClient.Client;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.UI.ViewModels;


public partial class MainViewModel : ObservableObject, IAsyncDisposable {
  private readonly ConfigurationFileService _files;
  private readonly ISecretStore _secrets;
  private readonly IMailSessionFactory _sessions;
  private readonly IMailAuthService _auth;
  private readonly ISentReceiptStore _receipts;
  private readonly MailArchiveStore _archive;
  private readonly Dictionary<string, IMailSession> _connections = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, List<FolderRowViewModel>> _foldersByMailbox = new(StringComparer.OrdinalIgnoreCase);
  private readonly HashSet<string> _connecting = new(StringComparer.OrdinalIgnoreCase);
  private CancellationTokenSource? _work;
  private CancellationTokenSource? _backfill;
  private readonly HashSet<string> _indexMailboxes = new(StringComparer.OrdinalIgnoreCase);
  private readonly HashSet<string> _catalogDone = new(StringComparer.OrdinalIgnoreCase);
  private string? _indexMailboxId;
  private MailMessageBody? _reading;
  private MessageRowViewModel? _pendingWindow;
  private bool _syncingTree;
  private bool _suppressFolderLoad;
  private bool _applyingProvider;
  private bool _accountSettingsOpen;
  private bool _syncingBodyKind;
  private string _bodyPreference = MailBodyKind.Html;
  private string _listSortHeader = "Date";
  private bool _listSortDescending = true;
  private OAuthTokenSet? _pendingOauth;

  public bool AccountSettingsOpen {
    get => _accountSettingsOpen;
    set => _accountSettingsOpen = value;
  }

  [ObservableProperty]
  private MailboxAccount? selectedMailbox;

  [ObservableProperty]
  private FolderRowViewModel? selectedFolder;

  [ObservableProperty]
  private FolderNodeViewModel? selectedFolderNode;

  [ObservableProperty]
  private MessageRowViewModel? selectedMessage;

  [ObservableProperty]
  private string status = "";

  [ObservableProperty]
  private UiCopy copy = UiCopy.For(UiLanguage.En);

  [ObservableProperty]
  private string folderStats = "";

  [ObservableProperty]
  private bool isBusy;

  [ObservableProperty]
  private string editorName = "";

  [ObservableProperty]
  private string editorAddress = "";

  [ObservableProperty]
  private string editorUsername = "";

  [ObservableProperty]
  private string editorPassword = "";

  [ObservableProperty]
  private ChoiceRow editorProvider = ChoiceRow.Provider(MailProvider.Imap);

  [ObservableProperty]
  private string editorGoogleClientId = "";

  [ObservableProperty]
  private string editorGoogleClientSecret = "";

  [ObservableProperty]
  private string editorMicrosoftClientId = "";

  [ObservableProperty]
  private bool showPasswordFields = true;

  [ObservableProperty]
  private bool showOAuthButtons;

  [ObservableProperty]
  private bool showGoogleSignIn;

  [ObservableProperty]
  private bool showMicrosoftSignIn;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(ShowAuthHint))]
  private string oauthHint = "";

  public bool ShowAuthHint =>
    !string.IsNullOrWhiteSpace(OauthHint);

  [ObservableProperty]
  private ChoiceRow editorIncomingProtocol = ChoiceRow.Incoming(MailProtocol.Imap);

  [ObservableProperty]
  private ChoiceRow editorIncomingSecurity = ChoiceRow.Security(MailSecurity.Ssl);

  [ObservableProperty]
  private string editorIncomingHost = "";

  [ObservableProperty]
  private decimal editorIncomingPort = 993;

  [ObservableProperty]
  private ChoiceRow editorSmtpSecurity = ChoiceRow.Security(MailSecurity.Ssl);

  [ObservableProperty]
  private string editorSmtpHost = "";

  [ObservableProperty]
  private decimal editorSmtpPort = 465;

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
  private bool hasReading;

  [ObservableProperty]
  private bool hasReadingCc;

  [ObservableProperty]
  private bool hasReadingEnvelope;

  [ObservableProperty]
  private bool hasReadingFiles;

  [ObservableProperty]
  private bool unwrapEnvelope;

  [ObservableProperty]
  private bool groupConversations;

  [ObservableProperty]
  private string readingBodyKind = MailBodyKind.Html;

  [ObservableProperty]
  private string readingLayout = MailLayout.Stacked;

  [ObservableProperty]
  private string messageFilter = "";

  [ObservableProperty]
  private string practiceLabel = "";

  [ObservableProperty]
  private string quotaLine = "";

  [ObservableProperty]
  private string indexLine = "";

  [ObservableProperty]
  private string readingFattura = "";

  [ObservableProperty]
  private bool hasReadingFattura;

  public ObservableCollection<MailboxAccount> Mailboxes { get; } = [];

  public ObservableCollection<FolderRowViewModel> Folders { get; } = [];

  public ObservableCollection<FolderNodeViewModel> FolderTree { get; } = [];

  public ObservableCollection<MessageRowViewModel> Messages { get; } = [];

  public ObservableCollection<MessageRowViewModel> VisibleMessages { get; } = [];

  public ObservableCollection<MailFileAttachment> ReadingFiles { get; } = [];

  public List<MessageRowViewModel> SelectedMessages { get; } = [];

  public IReadOnlyList<ChoiceRow> IncomingProtocols =>
    ChoiceRow.IncomingProtocols;

  public IReadOnlyList<ChoiceRow> SecurityModes =>
    ChoiceRow.SecurityModes;

  public IReadOnlyList<ChoiceRow> Providers =>
    ChoiceRow.Providers;

  public IReadOnlyList<ChoiceRow> Languages { get; } = [
    new() { Id = UiLanguage.En, Title = UiLanguage.Title(UiLanguage.En) },
    new() { Id = UiLanguage.It, Title = UiLanguage.Title(UiLanguage.It) }
  ];

  public ChoiceRow SelectedLanguage {
    get => Languages.First(l => l.Id == UiLocale.Id);
    set {
      if (value is null || value.Id == UiLocale.Id)
        return;
      SetLanguage(value.Id);
    }
  }

  public bool IsEnglish =>
    UiLocale.Id == UiLanguage.En;

  public bool IsItalian =>
    UiLocale.Id == UiLanguage.It;

  public bool HtmlEngineReady { get; private set; } = true;

  public bool ShowHtmlBody =>
    HasReading && HtmlEngineReady && MailBodyKind.IsHtml(ReadingBodyKind);

  public bool ShowTextBody =>
    HasReading && (MailBodyKind.IsText(ReadingBodyKind)
      || (!HtmlEngineReady && MailBodyKind.IsHtml(ReadingBodyKind)));

  public bool ShowSourceBody =>
    HasReading && MailBodyKind.IsSource(ReadingBodyKind);

  public bool ShowFatturaBody =>
    HasReading && MailBodyKind.IsFattura(ReadingBodyKind);

  public bool IsHtmlKind =>
    MailBodyKind.IsHtml(ReadingBodyKind);

  public bool IsTextKind =>
    MailBodyKind.IsText(ReadingBodyKind);

  public bool IsSourceKind =>
    MailBodyKind.IsSource(ReadingBodyKind);

  public bool IsFatturaKind =>
    MailBodyKind.IsFattura(ReadingBodyKind);

  public bool IsWideLayout =>
    MailLayout.IsWide(ReadingLayout);

  public bool IsStackedLayout =>
    MailLayout.IsStacked(ReadingLayout);

  public event Action<ComposeViewModel>? ComposeRequested;

  public event Action<MessageWindowViewModel>? MessageWindowRequested;

  public event Action? AccountSettingsRequested;

  public event Action? AccountSaved;

  public event Action<string, string>? NoticeRequested;

  public event Func<PromptRequest, Task<string?>>? PromptRequested;

  public event Action? ExportArchiveRequested;

  public event Action? ExportFascicoloRequested;

  public event Action? ImportEmlRequested;

  public event Action? ImportPstRequested;

  public event Action? ImportThunderbirdRequested;

  public event Action<string>? PrintHtmlRequested;

  public event Action<byte[], string>? SavePdfRequested;

  public event Action<byte[], string>? SaveAttachmentsZipRequested;

  public MainViewModel(
    ConfigurationFileService files,
    ISecretStore secrets,
    IMailSessionFactory sessions,
    IMailAuthService auth,
    ISentReceiptStore receipts,
    MailArchiveStore archive) {
    _files = files;
    _secrets = secrets;
    _sessions = sessions;
    _auth = auth;
    _receipts = receipts;
    _archive = archive;
    files.Current.EnsureDefaults();
    UiLocale.Apply(files.Current.Language);
    copy = UiLocale.Copy;
    status = copy.AddMailboxThenGet;
    unwrapEnvelope = files.Current.UnwrapEnvelope;
    groupConversations = files.Current.GroupConversations;
    _bodyPreference = MailBodyKind.Preference(files.Current.ReadingBodyKind);
    readingBodyKind = _bodyPreference;
    readingLayout = MailLayout.Normalize(files.Current.ReadingLayout);
    messageFilter = files.Current.Layout?.MessageFilter ?? "";
    var savedSort = files.Current.Layout?.ColumnSort;
    if (savedSort is not null && !string.IsNullOrWhiteSpace(savedSort.Header)) {
      _listSortHeader = savedSort.Header;
      _listSortDescending = string.Equals(
        savedSort.Direction,
        nameof(System.ComponentModel.ListSortDirection.Descending),
        StringComparison.OrdinalIgnoreCase);
    }

    editorGoogleClientId = files.Current.GoogleClientId;
    editorGoogleClientSecret = StoredGoogleClientSecret();
    editorMicrosoftClientId = files.Current.MicrosoftClientId;
    ReloadMailboxes();
  }

  public async ValueTask DisposeAsync() {
    StopBackfill();
    _work?.Cancel();
    foreach (var session in _connections.Values.ToList())
      await session.DisposeAsync();
    _connections.Clear();
    _work?.Dispose();
    _archive.Dispose();
  }

  [RelayCommand]
  private void NewMailbox() {
    _pendingOauth = null;
    SelectedMailbox = null;
    EditorName = "";
    EditorAddress = "";
    EditorUsername = "";
    EditorPassword = "";
    EditorGoogleClientId = _files.Current.GoogleClientId;
    EditorGoogleClientSecret = "";
    EditorMicrosoftClientId = _files.Current.MicrosoftClientId;
    _applyingProvider = true;
    EditorProvider = ChoiceRow.Provider(MailProvider.Imap);
    _applyingProvider = false;
    EditorIncomingProtocol = ChoiceRow.Incoming(MailProtocol.Imap);
    EditorIncomingSecurity = ChoiceRow.Security(MailSecurity.Ssl);
    EditorIncomingHost = "";
    EditorIncomingPort = MailSecurity.DefaultIncomingPort(MailProtocol.Imap, MailSecurity.Ssl);
    EditorSmtpSecurity = ChoiceRow.Security(MailSecurity.Ssl);
    EditorSmtpHost = "";
    EditorSmtpPort = MailSecurity.DefaultSmtpPort(MailSecurity.Ssl);
    UpdateAuthUi();
    AccountSettingsRequested?.Invoke();
  }

  [RelayCommand]
  private void OpenAccountSettings() {
    if (_pendingOauth is not null) {
      AccountSettingsRequested?.Invoke();
      return;
    }

    if (SelectedMailbox is null) {
      NewMailbox();
      return;
    }

    FillEditor(SelectedMailbox);
    AccountSettingsRequested?.Invoke();
  }

  [RelayCommand]
  private void SetLanguage(string? id) {
    var language = UiLanguage.Normalize(id);
    var configuration = _files.Current;
    configuration.Language = language;
    _files.Save(configuration);
    ApplyLanguage();
  }

  private void ApplyLanguage() {
    var previous = Copy;
    UiLocale.Apply(_files.Current.Language);
    Copy = UiLocale.Copy;
    if (string.IsNullOrWhiteSpace(Status) || Status == previous.AddMailboxThenGet)
      Status = Copy.AddMailboxThenGet;
    RelocalizeFolders();
    RefreshFolderStats();
    UpdateAuthUi();
    EditorIncomingSecurity = ChoiceRow.Security(EditorIncomingSecurity.Id);
    EditorSmtpSecurity = ChoiceRow.Security(EditorSmtpSecurity.Id);
    OnPropertyChanged(nameof(SecurityModes));
    OnPropertyChanged(nameof(SelectedLanguage));
    OnPropertyChanged(nameof(IsEnglish));
    OnPropertyChanged(nameof(IsItalian));
    foreach (var row in Messages)
      row.RefreshMarks();
  }

  private void RelocalizeFolders() {
    foreach (var rows in _foldersByMailbox.Values) {
      foreach (var folder in rows)
        folder.Name = MailFolderRole.DisplayName(null, folder.FullName);
    }

    NotifyFolderCounts();
  }

  [RelayCommand]
  private void EditMailbox() =>
    OpenAccountSettings();

  [RelayCommand]
  private void SaveMailbox() =>
    WriteMailbox(closeSettings: true);

  private void WriteMailbox(bool closeSettings) {
    if (string.IsNullOrWhiteSpace(EditorIncomingHost) || string.IsNullOrWhiteSpace(EditorAddress)) {
      Status = "Address and incoming host are required.";
      return;
    }

    var configuration = _files.Current;
    configuration.EnsureDefaults();
    var box = MailboxToSave();
    box.DisplayName = EditorName.Trim();
    box.Address = EditorAddress.Trim();
    box.Username = EditorUsername.Trim();
    box.IncomingProtocol = EditorIncomingProtocol.Id;
    box.IncomingSecurity = EditorIncomingSecurity.Id;
    box.ImapHost = EditorIncomingHost.Trim();
    box.ImapPort = EditorIncomingPort <= 0
      ? MailSecurity.DefaultIncomingPort(box.IncomingProtocol, box.IncomingSecurity)
      : (int)EditorIncomingPort;
    box.SmtpSecurity = EditorSmtpSecurity.Id;
    box.SmtpHost = string.IsNullOrWhiteSpace(EditorSmtpHost) ? EditorIncomingHost.Trim() : EditorSmtpHost.Trim();
    box.SmtpPort = EditorSmtpPort <= 0
      ? MailSecurity.DefaultSmtpPort(box.SmtpSecurity)
      : (int)EditorSmtpPort;
    box.Provider = MailProvider.Normalize(EditorProvider.Id);
    if (_pendingOauth is not null)
      box.AuthKind = MailAuthKind.Normalize(_pendingOauth.Provider);
    else if (MailProvider.UsesOAuth(box.Provider) && _auth.HasTokens(box.Id))
      box.AuthKind = MailAuthKind.Normalize(
        box.Provider == MailProvider.Outlook ? MailAuthKind.Microsoft : MailAuthKind.Google);
    else
      box.AuthKind = MailAuthKind.Password;
    MailProvider.Apply(box);
    if (configuration.FindMailbox(box.Id) is null)
      configuration.Mailboxes.Add(box);
    configuration.SelectedMailboxId = box.Id;
    PersistClientIds(configuration);
    PersistGoogleClientSecret();
    _files.Save(configuration);
    if (!string.IsNullOrWhiteSpace(EditorPassword))
      _secrets.Put(FileSecretStore.MailboxKey(box.Id), EditorPassword);
    if (_pendingOauth is not null) {
      _auth.SaveTokens(box.Id, _pendingOauth);
      _pendingOauth = null;
    }
    ReloadMailboxes();
    SelectedMailbox = Mailboxes.FirstOrDefault(m => m.Id == box.Id);
    Status = "Mailbox saved. Secrets stay in the OS secret file, not settings.json.";
    if (!closeSettings)
      return;
    AccountSettingsOpen = false;
    AccountSaved?.Invoke();
  }

  [RelayCommand]
  private async Task RemoveMailboxAsync() {
    if (SelectedMailbox is null)
      return;
    var id = SelectedMailbox.Id;
    var configuration = _files.Current;
    configuration.Mailboxes.RemoveAll(m => m.Id == id);
    if (configuration.SelectedMailboxId == id)
      configuration.SelectedMailboxId = configuration.Mailboxes.FirstOrDefault()?.Id;
    _files.Save(configuration);
    _secrets.Delete(FileSecretStore.MailboxKey(id));
    _auth.DeleteTokens(id);
    await DisconnectMailboxAsync(id);
    ReloadMailboxes();
    Status = "Mailbox removed.";
    AccountSaved?.Invoke();
  }

  [RelayCommand(CanExecute = nameof(CanGetMessages))]
  private async Task GetMessagesAsync() {
    if (ActiveSession is { IsConnected: true })
      await RefreshAsync();
    else
      await ConnectAsync();
  }

  [RelayCommand(CanExecute = nameof(CanConnect))]
  private async Task ConnectAsync() {
    if (SelectedMailbox is null)
      return;
    var box = SelectedMailbox;
    if (SessionFor(box) is not { IsConnected: true }) {
      var password = PasswordFor(box);
      var oauth = MailAuthKind.IsOAuth(box.AuthKind) || _auth.HasTokens(box.Id);
      if (string.IsNullOrWhiteSpace(password) && !oauth) {
        Status = "Enter the password once in Account Settings, then Get Messages.";
        if (!AccountSettingsOpen) {
          FillEditor(box);
          AccountSettingsRequested?.Invoke();
        }
        return;
      }

      await RunAsync(Copy.Connecting, async token => {
        var ok = await ConnectMailboxCoreAsync(box, password, token);
        if (!ok)
          return;
        if (!string.IsNullOrWhiteSpace(EditorPassword) && SelectedMailbox?.Id == box.Id)
          _secrets.Put(FileSecretStore.MailboxKey(box.Id), EditorPassword);
        if (_auth.HasTokens(box.Id) && !MailAuthKind.IsOAuth(box.AuthKind))
          box.AuthKind = MailProvider.Normalize(box.Provider) == MailProvider.Outlook
            ? MailAuthKind.Microsoft
            : MailAuthKind.Google;
        var configuration = _files.Current;
        configuration.SelectedMailboxId = box.Id;
        _files.Save(configuration);
        Status = "Connected to " + box.Label + ".";
      });
    }
    else {
      SyncFoldersCollection(box);
      RebuildFolderTree();
    }

    _ = ConnectIdleMailboxesAsync();
  }

  [RelayCommand]
  private async Task RefreshAsync() {
    if (SelectedFolder is null)
      return;
    await RunAsync(
      Copy.GettingMessages,
      token => LoadMessagesAsync(SelectedFolder.FullName, token, resumeBodies: true));
  }

  [RelayCommand]
  private void OpenArchiveFolder() {
    AppPaths.EnsureDirectories();
    try {
      Process.Start(new ProcessStartInfo {
        FileName = AppPaths.DataDirectory(),
        UseShellExecute = true
      });
    }
    catch (Exception ex) {
      Status = ex.Message;
    }
  }

  [RelayCommand]
  private void ExportArchive() =>
    ExportArchiveRequested?.Invoke();

  public void ExportArchiveTo(string destDir) {
    try {
      _archive.Checkpoint();
      ArchiveFiles.ExportTo(destDir);
      Status = "Exported archive to " + destDir + ". " + ArchiveFiles.Hint();
    }
    catch (Exception ex) {
      Status = ex.Message;
    }
  }

  [RelayCommand]
  private void ExportFascicolo() =>
    ExportFascicoloRequested?.Invoke();

  [RelayCommand(CanExecute = nameof(HasOpenMessage))]
  private void PrintMessage() {
    if (_reading is null)
      return;
    var html = MessagePrint.Html(_reading, UnwrapEnvelope);
    var path = Path.Combine(Path.GetTempPath(), "postclient-print.html");
    File.WriteAllText(path, html);
    PrintHtmlRequested?.Invoke(path);
  }

  [RelayCommand(CanExecute = nameof(HasOpenMessage))]
  private void SaveMessagePdf() {
    if (_reading is null)
      return;
    SavePdfRequested?.Invoke(MessagePrint.Pdf(_reading, UnwrapEnvelope), MessagePrint.EmlFileName(_reading.Header) + ".pdf");
  }

  [RelayCommand(CanExecute = nameof(HasReadingAttachments))]
  private void SaveAttachmentsZip() {
    if (ReadingFiles.Count == 0)
      return;
    var name = AttachmentZip.SuggestedName(_reading?.Header.Subject);
    SaveAttachmentsZipRequested?.Invoke(AttachmentZip.FromFiles(ReadingFiles), name);
    Status = "Save the ZIP on this PC — originals stay in the message.";
  }

  private bool HasReadingAttachments() =>
    HasReadingFiles;

  [RelayCommand]
  private void ImportEml() =>
    ImportEmlRequested?.Invoke();

  [RelayCommand]
  private void ImportPst() =>
    ImportPstRequested?.Invoke();

  [RelayCommand]
  private void ImportThunderbird() =>
    ImportThunderbirdRequested?.Invoke();

  [RelayCommand]
  private void ApplyPracticeLabel() {
    if (SelectedMailbox is null || SelectedFolder is null || PracticeLabel.Trim().Length == 0)
      return;
    var name = PracticeLabel.Trim();
    foreach (var row in TargetRows()) {
      _archive.AddLabel(SelectedMailbox.Id, row.Header.Folder, row.Header.Id, name);
      var labels = string.IsNullOrWhiteSpace(row.Header.Labels)
        ? name
        : row.Header.Labels + ", " + name;
      row.Header.Labels = labels;
      row.RefreshMarks();
    }

    Status = "Label “" + name + "” stays on this PC — not a shared studio table.";
    RebuildVisible();
  }

  private bool HasOpenMessage() =>
    _reading is not null;

  public async Task ExportFascicoloToAsync(string destDir) {
    if (SelectedMailbox is null)
      return;
    Directory.CreateDirectory(destDir);
    var rows = TargetRows();
    if (rows.Count == 0 && SelectedMessage is not null)
      rows = [SelectedMessage];
    var copied = 0;
    var index = new List<string>();
    foreach (var row in rows) {
      var eml = _archive.EmlPath(SelectedMailbox.Id, row.Header.Folder, row.Header.Id);
      if (string.IsNullOrWhiteSpace(eml) || !File.Exists(eml)) {
        if (_reading is { } body && body.Header.Id == row.Header.Id && body.RawEml.Length > 0) {
          eml = Path.Combine(destDir, MessagePrint.EmlFileName(row.Header));
          await File.WriteAllBytesAsync(eml, body.RawEml);
        }
        else
          continue;
      }
      else {
        var dest = Path.Combine(destDir, MessagePrint.EmlFileName(row.Header));
        File.Copy(eml, dest, overwrite: true);
        eml = dest;
      }

      copied++;
      index.Add(row.Header.Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm") + "  " + row.Header.Subject);
    }

    if (_reading is not null)
      await File.WriteAllBytesAsync(Path.Combine(destDir, "messaggio.pdf"), MessagePrint.Pdf(_reading, UnwrapEnvelope));
    await File.WriteAllLinesAsync(Path.Combine(destDir, "index.txt"), index);
    Status = "Fascicolo: " + copied + " .eml in " + destDir + " (this PC only).";
  }

  public async Task ImportEmlPathsAsync(IReadOnlyList<string> paths) {
    if (SelectedMailbox is null) {
      Status = "Select a mailbox first.";
      return;
    }

    var folder = SelectedFolder?.FullName ?? "Imported";
    var import = new LocalMailImport(_archive);
    var count = await import.ImportEmlFilesAsync(
      SelectedMailbox.Id,
      folder,
      paths,
      ActiveSession,
      UnwrapEnvelope,
      CancellationToken.None);
    Status = "Imported " + count + " .eml into " + folder + ".";
    if (SelectedFolder is not null)
      await LoadMessagesAsync(SelectedFolder.FullName, CancellationToken.None);
  }

  public async Task ImportPstPathAsync(string path) {
    if (SelectedMailbox is null) {
      Status = "Select a mailbox first.";
      return;
    }

    var import = new LocalMailImport(_archive);
    var count = await PstImport.ImportAsync(
      import,
      SelectedMailbox.Id,
      path,
      ActiveSession,
      UnwrapEnvelope,
      CancellationToken.None);
    Status = "Imported " + count + " messages from PST.";
    if (SelectedFolder is not null)
      await LoadMessagesAsync(SelectedFolder.FullName, CancellationToken.None);
  }

  public async Task ImportThunderbirdAsync() {
    if (SelectedMailbox is null) {
      Status = "Select a mailbox first.";
      return;
    }

    var import = new LocalMailImport(_archive);
    var count = await import.ImportThunderbirdAsync(
      SelectedMailbox.Id,
      ActiveSession,
      UnwrapEnvelope,
      CancellationToken.None);
    Status = count == 0
      ? "No Thunderbird mbox files found."
      : "Imported " + count + " messages from Thunderbird.";
    if (SelectedFolder is not null)
      await LoadMessagesAsync(SelectedFolder.FullName, CancellationToken.None);
  }

  [RelayCommand]
  private void StartCompose() =>
    OpenCompose("Write: " + (SelectedMailbox?.Address ?? ""), null, null, "", "");

  [RelayCommand(CanExecute = nameof(CanReply))]
  private void Reply() {
    OpenCompose(
      "Reply",
      Recipients(ReadingFrom),
      null,
      ReplySubject(),
      QuotedBody());
  }

  [RelayCommand(CanExecute = nameof(CanReply))]
  private void ReplyAll() {
    var self = SelectedMailbox?.Address ?? "";
    var to = DistinctRecipients(Recipients(ReadingFrom).Concat(Recipients(ReadingTo)), self);
    var toSet = new HashSet<string>(to.Select(r => r.Address), StringComparer.OrdinalIgnoreCase);
    var cc = DistinctRecipients(Recipients(ReadingCc), self)
      .Where(r => !toSet.Contains(r.Address))
      .ToList();
    OpenCompose("Reply All", to, cc, ReplySubject(), QuotedBody());
  }

  [RelayCommand(CanExecute = nameof(CanReply))]
  private void Forward() {
    var subject = SelectedMessage is null
      ? ""
      : (SelectedMessage.Header.Subject.StartsWith("Fwd:", StringComparison.OrdinalIgnoreCase)
        ? SelectedMessage.Header.Subject
        : "Fwd: " + SelectedMessage.Header.Subject);
    OpenCompose("Forward", null, null, subject, QuotedBody());
  }

  [RelayCommand(CanExecute = nameof(CanReply))]
  private void OpenMessageWindow(MessageRowViewModel? row) {
    row ??= SelectedMessage;
    if (row is null)
      return;
    if (IsLoadedBody(row.Header) && _reading is not null) {
      if (!row.Header.IsSeen)
        _ = MarkOpenedReadAsync(row.Header, CancellationToken.None);
      ShowMessageWindow(_reading);
      return;
    }

    _pendingWindow = row;
    if (!ReferenceEquals(SelectedMessage, row))
      SelectedMessage = row;
  }

  [RelayCommand(CanExecute = nameof(CanSignIn))]
  private async Task SignInGoogleAsync() =>
    await SignInProviderAsync(MailAuthKind.Google);

  [RelayCommand(CanExecute = nameof(CanSignIn))]
  private async Task SignInMicrosoftAsync() =>
    await SignInProviderAsync(MailAuthKind.Microsoft);

  public void SetSelectedMessages(IEnumerable<MessageRowViewModel> rows) {
    SelectedMessages.Clear();
    foreach (var row in rows)
      SelectedMessages.Add(row);
    NotifyMessageCommands();
  }

  [RelayCommand(CanExecute = nameof(CanOrganize))]
  private async Task MoveToFolderAsync(FolderRowViewModel? folder) {
    if (folder is null)
      return;
    await MoveRowsAsync(TargetRows(), folder.FullName);
  }

  public Task MoveDroppedAsync(string destFolder, IReadOnlyList<uint> ids, string? fromFolder = null) =>
    MoveIdsAsync(ids, destFolder, fromFolder);

  [RelayCommand(CanExecute = nameof(CanOrganize))]
  private async Task MarkReadAsync() =>
    await ApplyFlagsAsync(new MailFlagUpdate { Seen = true }, row => row.Header.IsSeen = true);

  [RelayCommand(CanExecute = nameof(CanOrganize))]
  private async Task MarkUnreadAsync() =>
    await ApplyFlagsAsync(new MailFlagUpdate { Seen = false }, row => row.Header.IsSeen = false);

  [RelayCommand(CanExecute = nameof(CanOrganize))]
  private async Task ToggleFlagAsync(MessageRowViewModel? row) {
    var rows = row is null ? TargetRows() : [row];
    if (rows.Count == 0)
      return;
    var flag = !rows[0].Header.IsFlagged;
    await ApplyFlagsAsync(
      new MailFlagUpdate { Flagged = flag },
      r => r.Header.IsFlagged = flag,
      rows);
  }

  [RelayCommand(CanExecute = nameof(CanOrganize))]
  private async Task SetPriorityAsync(string? priority) {
    if (string.IsNullOrWhiteSpace(priority))
      return;
    await ApplyFlagsAsync(
      new MailFlagUpdate { Priority = priority },
      row => row.Header.Priority = priority);
  }

  [RelayCommand(CanExecute = nameof(CanOrganize))]
  private async Task DeleteMessagesAsync() {
    var rows = TargetRows();
    if (rows.Count == 0)
      return;
    var trash = Folders.Concat(FoldersFor(SelectedMailbox))
      .FirstOrDefault(f => MailFolderRole.Kind(f.Name, f.FullName) == "trash");
    if (trash is not null
        && SelectedFolder is not null
        && !trash.FullName.Equals(SelectedFolder.FullName, StringComparison.OrdinalIgnoreCase)) {
      await MoveRowsAsync(rows, trash.FullName);
      return;
    }

    if (!await ApplyFlagsAsync(new MailFlagUpdate { Deleted = true }, null, rows))
      return;
    if (SelectedMailbox is null || SelectedFolder is null)
      return;
    DeleteEmlFiles(_archive.RemoveUids(SelectedMailbox.Id, SelectedFolder.FullName, TargetIds(rows)));
    RemoveRows(rows);
    Status = "Deleted.";
  }

  [RelayCommand(CanExecute = nameof(CanCreateFolder))]
  private async Task CreateFolderAsync() {
    var session = ActiveSession;
    var box = SelectedMailbox;
    if (session is not { IsConnected: true, SupportsFolders: true } || box is null)
      return;
    var name = await PromptAsync(Copy.NewFolder, Copy.FolderName, confirmOnly: false, Copy.FolderName);
    if (string.IsNullOrWhiteSpace(name))
      return;
    var parent = SelectedFolder is { } folder && MailFolderRole.IsCustom(folder.Name, folder.FullName)
      ? folder.FullName
      : null;
    await RunAsync(Copy.CreatingFolder, async token => {
      var result = await session.CreateFolderAsync(name.Trim(), parent, token);
      if (!result.IsSuccess) {
        Status = string.Join(" ", result.Messages);
        return;
      }

      await LoadFoldersAsync(box, session, token);
      Status = Copy.FolderCreated;
    });
  }

  [RelayCommand(CanExecute = nameof(CanEmptyFolder))]
  private async Task EmptyFolderAsync() {
    var session = ActiveSession;
    var box = SelectedMailbox;
    var folder = SelectedFolder;
    if (session is not { IsConnected: true, SupportsFolders: true } || box is null || folder is null)
      return;
    var trash = Folders.Concat(FoldersFor(SelectedMailbox))
      .FirstOrDefault(f => MailFolderRole.Kind(f.Name, f.FullName) == "trash");
    var purge = trash is null
      || trash.FullName.Equals(folder.FullName, StringComparison.OrdinalIgnoreCase);
    var confirm = string.Format(purge ? Copy.EmptyTrashConfirm : Copy.EmptyFolderConfirm, folder.Name);
    if (!await ConfirmAsync(Copy.EmptyFolder, confirm))
      return;
    await RunAsync(Copy.EmptyingFolder, async token => {
      var result = await session.EmptyFolderAsync(
        folder.FullName,
        purge ? null : trash!.FullName,
        token);
      if (!result.IsSuccess) {
        Status = string.Join(" ", result.Messages);
        return;
      }

      ForgetLocalFolder(box.Id, folder.FullName);
      folder.Total = 0;
      folder.Unread = 0;
      if (SelectedFolder?.FullName.Equals(folder.FullName, StringComparison.OrdinalIgnoreCase) == true) {
        Messages.Clear();
        VisibleMessages.Clear();
        SelectedMessage = null;
        ClearReading();
        RefreshFolderStats();
      }

      NotifyFolderCounts();
      Status = Copy.FolderEmptied;
    });
  }

  [RelayCommand(CanExecute = nameof(CanEmptyFolder))]
  private async Task MarkFolderReadAsync() =>
    await MarkFolderSeenAsync(true);

  [RelayCommand(CanExecute = nameof(CanEmptyFolder))]
  private async Task MarkFolderUnreadAsync() =>
    await MarkFolderSeenAsync(false);

  [RelayCommand(CanExecute = nameof(CanDeleteCustomFolder))]
  private async Task DeleteCustomFolderAsync() {
    var session = ActiveSession;
    var box = SelectedMailbox;
    var folder = SelectedFolder;
    if (session is not { IsConnected: true, SupportsFolders: true } || box is null || folder is null)
      return;
    if (!MailFolderRole.IsCustom(folder.Name, folder.FullName)) {
      Status = Copy.CannotDeleteSystemFolder;
      return;
    }

    var confirm = string.Format(Copy.DeleteFolderConfirm, folder.Name);
    if (!await ConfirmAsync(Copy.DeleteFolder, confirm))
      return;
    var fullName = folder.FullName;
    var open = SelectedFolder?.FullName.Equals(fullName, StringComparison.OrdinalIgnoreCase) == true;
    await RunAsync(Copy.DeletingFolder, async token => {
      var result = await session.DeleteFolderAsync(fullName, token);
      if (!result.IsSuccess) {
        Status = string.Join(" ", result.Messages);
        return;
      }

      ForgetLocalFolder(box.Id, fullName);
      if (open) {
        Messages.Clear();
        VisibleMessages.Clear();
        SelectedMessage = null;
        ClearReading();
      }

      await LoadFoldersAsync(box, session, token);
      Status = Copy.FolderDeleted;
    });
  }

  private async Task MarkFolderSeenAsync(bool seen) {
    var session = ActiveSession;
    var box = SelectedMailbox;
    var folder = SelectedFolder;
    if (session is not { IsConnected: true, SupportsFolders: true } || box is null || folder is null)
      return;
    await RunAsync(Copy.UpdatingFlags, async token => {
      var result = await session.SetFolderSeenAsync(folder.FullName, seen, token);
      if (!result.IsSuccess) {
        Status = string.Join(" ", result.Messages);
        return;
      }

      _archive.SetSeen(box.Id, folder.FullName, seen);
      foreach (var row in Messages) {
        row.Header.IsSeen = seen;
        row.RefreshMarks();
      }

      folder.Unread = seen ? 0 : folder.Total;
      NotifyFolderCounts();
      RefreshFolderStats();
      Status = seen ? Copy.FolderMarkedRead : Copy.FolderMarkedUnread;
    });
  }

  private async Task<bool> ConfirmAsync(string title, string message) {
    var result = await PromptAsync(title, message, confirmOnly: true);
    return result is not null;
  }

  private async Task<string?> PromptAsync(string title, string message, bool confirmOnly, string? placeholder = null) {
    if (PromptRequested is null)
      return confirmOnly ? "" : null;
    return await PromptRequested(new PromptRequest {
      Title = title,
      Message = message,
      ConfirmOnly = confirmOnly,
      Placeholder = placeholder ?? ""
    });
  }

  private void ForgetLocalFolder(string mailboxId, string folder) {
    DeleteEmlFiles(_archive.RemoveFolder(mailboxId, folder));
  }

  private static void DeleteEmlFiles(IEnumerable<string> paths) {
    foreach (var path in paths) {
      try {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
          File.Delete(path);
      }
      catch {
      }
    }
  }

  private string ReplySubject() {
    if (SelectedMessage is null)
      return "";
    return SelectedMessage.Header.Subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase)
      ? SelectedMessage.Header.Subject
      : "Re: " + SelectedMessage.Header.Subject;
  }

  private string QuotedBody() =>
    string.IsNullOrWhiteSpace(ReadingBody) ? "" : "\n\n--- Original message ---\n" + ReadingBody;

  private void OpenCompose(
    string title,
    IReadOnlyList<MailRecipient>? to,
    IReadOnlyList<MailRecipient>? cc,
    string subject,
    string body) {
    if (!TryComposeIdentities(out var identities, out var selected) || selected is null) {
      Status = "Add a mailbox to send From.";
      return;
    }

    var compose = new ComposeViewModel(identities, selected, title, EnsureComposeSessionAsync);
    if (to is { Count: > 0 })
      compose.ToField.AddFromText(JoinRecipients(to));
    if (cc is { Count: > 0 })
      compose.CcField.AddFromText(JoinRecipients(cc));
    compose.Subject = subject;
    compose.Body = body;
    if (_reading is not null && title.StartsWith("Reply", StringComparison.OrdinalIgnoreCase)) {
      compose.InReplyTo = _reading.Header.MessageId;
      compose.References = MailId.ThreadLine(_reading.Header.InReplyTo, _reading.Header.MessageId);
    }

    compose.Sent += RememberSent;
    ComposeRequested?.Invoke(compose);
  }

  private bool TryComposeIdentities(out List<ComposeIdentity> identities, out ComposeIdentity? selected) {
    identities = [];
    selected = null;
    foreach (var box in Mailboxes) {
      var row = new ComposeIdentity { Account = box, Session = SessionFor(box) };
      identities.Add(row);
      if (SelectedMailbox is not null && box.Id == SelectedMailbox.Id)
        selected = row;
    }

    selected ??= identities.FirstOrDefault();
    return identities.Count > 0;
  }

  private async Task<IMailSession?> EnsureComposeSessionAsync(ComposeIdentity identity, CancellationToken token) {
    var box = identity.Account;
    if (SessionFor(box) is { IsConnected: true } live)
      return live;
    var password = PasswordFor(box);
    var oauth = MailAuthKind.IsOAuth(box.AuthKind) || _auth.HasTokens(box.Id);
    if (string.IsNullOrWhiteSpace(password) && !oauth)
      return null;
    var ok = await ConnectMailboxCoreAsync(box, password, token);
    return ok ? SessionFor(box) : null;
  }

  private void RememberSent(MailSendRequest request, string messageId, string mailboxId) {
    var box = _files.Current.FindMailbox(mailboxId) ?? SelectedMailbox;
    if (box is null)
      return;
    _receipts.Remember(new SentDispatch {
      MailboxId = box.Id,
      MessageId = messageId,
      Subject = request.Subject ?? "",
      To = request.To.Select(r => r.Address).ToList(),
      SentAt = DateTimeOffset.UtcNow,
      Status = ReceiptStatus.Submitted
    });
    Status = "Sent from " + box.Label + ". Waiting for PEC/REM ricevute (acceptance, then delivery).";
  }

  private bool CanConnect() =>
    !IsBusy && SelectedMailbox is not null;

  private bool CanGetMessages() =>
    !IsBusy && SelectedMailbox is not null;

  private bool CanReply() =>
    !IsBusy && SelectedMessage is not null && ActiveSession is { IsConnected: true };

  private bool CanOrganize() =>
    !IsBusy && ActiveSession is { IsConnected: true, SupportsFolders: true };

  private bool CanManageFolders() =>
    CanOrganize() && SelectedMailbox is not null;

  private bool CanCreateFolder() =>
    CanManageFolders();

  private bool CanEmptyFolder() =>
    CanManageFolders() && SelectedFolder is not null;

  private bool CanDeleteCustomFolder() =>
    CanEmptyFolder()
    && MailFolderRole.IsCustom(SelectedFolder?.Name, SelectedFolder?.FullName);

  partial void OnEditorIncomingProtocolChanged(ChoiceRow value) {
    ApplyIncomingPortDefault();
    if (!_applyingProvider && MailProvider.HasHostPreset(EditorProvider.Id))
      ApplyProviderPreset();
  }

  partial void OnEditorIncomingSecurityChanged(ChoiceRow value) =>
    ApplyIncomingPortDefault();

  partial void OnEditorSmtpSecurityChanged(ChoiceRow value) {
    if (MailSecurity.IsDefaultSmtpPort((int)EditorSmtpPort))
      EditorSmtpPort = MailSecurity.DefaultSmtpPort(value.Id);
  }

  private void ApplyIncomingPortDefault() {
    if (MailSecurity.IsDefaultIncomingPort((int)EditorIncomingPort))
      EditorIncomingPort = MailSecurity.DefaultIncomingPort(EditorIncomingProtocol.Id, EditorIncomingSecurity.Id);
  }

  partial void OnEditorProviderChanged(ChoiceRow value) {
    if (_applyingProvider)
      return;
    ApplyProviderPreset();
    UpdateAuthUi();
  }

  public void ApplyListSort(string header, bool descending) {
    _listSortHeader = string.IsNullOrWhiteSpace(header) ? "Date" : header;
    _listSortDescending = descending;
    RebuildVisible();
  }

  partial void OnUnwrapEnvelopeChanged(bool value) {
    var configuration = _files.Current;
    configuration.UnwrapEnvelope = value;
    _files.Save(configuration);
    if (_reading is not null)
      ApplyReading(_reading);
  }

  partial void OnGroupConversationsChanged(bool value) {
    var configuration = _files.Current;
    configuration.GroupConversations = value;
    _files.Save(configuration);
    RebuildVisible();
  }

  partial void OnReadingBodyKindChanged(string value) {
    if (_syncingBodyKind)
      return;
    var kind = MailBodyKind.Normalize(value);
    if (!MailBodyKind.IsSource(kind)) {
      _bodyPreference = MailBodyKind.Preference(kind);
      var configuration = _files.Current;
      configuration.ReadingBodyKind = _bodyPreference;
      _files.Save(configuration);
    }

    NotifyBodyKind();
    if (_reading is not null)
      ApplyReading(_reading, keepKind: true);
  }

  [RelayCommand]
  private void SetReadingKind(string? kind) {
    ReadingBodyKind = MailBodyKind.Normalize(kind);
  }

  [RelayCommand]
  private void SetReadingLayout(string? layout) {
    ReadingLayout = MailLayout.Normalize(layout);
  }

  private void ApplyBodyKind(string kind) {
    _syncingBodyKind = true;
    ReadingBodyKind = kind;
    _syncingBodyKind = false;
    NotifyBodyKind();
  }

  partial void OnReadingLayoutChanged(string value) {
    var layout = MailLayout.Normalize(value);
    if (layout != value) {
      ReadingLayout = layout;
      return;
    }

    var configuration = _files.Current;
    configuration.ReadingLayout = layout;
    _files.Save(configuration);
    OnPropertyChanged(nameof(IsWideLayout));
    OnPropertyChanged(nameof(IsStackedLayout));
  }

  partial void OnHasReadingChanged(bool value) =>
    NotifyBodyKind();

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

  public void DisableHtmlEngine(string? reason = null) {
    if (!HtmlEngineReady)
      return;
    HtmlEngineReady = false;
    NotifyBodyKind();
    Status = string.IsNullOrWhiteSpace(reason) ? WebViewSetup.MissingEngineHint() : reason;
  }

  partial void OnMessageFilterChanged(string value) =>
    RebuildVisible();

  private bool CanSignIn() =>
    !IsBusy;

  partial void OnSelectedMailboxChanged(MailboxAccount? value) {
    ConnectCommand.NotifyCanExecuteChanged();
    GetMessagesCommand.NotifyCanExecuteChanged();
    NotifyMessageCommands();
    if (value is null)
      return;
    SyncFoldersCollection(value);
    if (SessionFor(value) is { IsConnected: true })
      return;
    if (!AccountSettingsOpen)
      _ = ConnectAsync();
  }

  partial void OnSelectedFolderNodeChanged(FolderNodeViewModel? value) {
    if (_syncingTree || value is null)
      return;
    if (value.Mailbox is { } box && SelectedMailbox?.Id != box.Id)
      SelectedMailbox = box;
    if (value.Folder is { } folder)
      SelectedFolder = folder;
    else if (value.IsAccount && value.Mailbox is { } account)
      SelectedFolder = InboxOf(account);
  }

  partial void OnSelectedFolderChanged(FolderRowViewModel? value) {
    RefreshFolderStats();
    NotifyFolderCommands();
    if (value is null || _syncingTree || _suppressFolderLoad)
      return;
    _ = RunAsync(Copy.OpeningFolder, token => LoadMessagesAsync(value.FullName, token));
  }

  partial void OnSelectedMessageChanged(MessageRowViewModel? value) {
    NotifyMessageCommands();
    if (value is null)
      return;
    _ = RunAsync(Copy.OpeningMessage, token => LoadBodyAsync(value.Header, token));
  }

  private void FillEditor(MailboxAccount box) {
    _pendingOauth = null;
    EditorName = box.DisplayName;
    EditorAddress = box.Address;
    EditorUsername = box.Username;
    EditorPassword = "";
    EditorGoogleClientId = _files.Current.GoogleClientId;
    EditorGoogleClientSecret = StoredGoogleClientSecret();
    EditorMicrosoftClientId = _files.Current.MicrosoftClientId;
    _applyingProvider = true;
    EditorIncomingProtocol = ChoiceRow.Incoming(box.IncomingProtocol);
    EditorIncomingSecurity = ChoiceRow.Security(box.IncomingSecurity, box.ImapSsl);
    EditorIncomingHost = box.ImapHost;
    EditorIncomingPort = box.ImapPort;
    EditorSmtpSecurity = ChoiceRow.Security(box.SmtpSecurity, box.SmtpSsl);
    EditorSmtpHost = box.SmtpHost;
    EditorSmtpPort = box.SmtpPort;
    EditorProvider = ChoiceRow.Provider(box.Provider);
    _applyingProvider = false;
    UpdateAuthUi();
  }

  private async Task SignInProviderAsync(string kind) {
    PersistClientIds(_files.Current);
    PersistGoogleClientSecret();
    _files.Save(_files.Current);
    IsBusy = true;
    SignInGoogleCommand.NotifyCanExecuteChanged();
    SignInMicrosoftCommand.NotifyCanExecuteChanged();
    var preview = kind == MailAuthKind.Microsoft
      ? OAuthClientId.Preview(EditorMicrosoftClientId)
      : OAuthClientId.Preview(EditorGoogleClientId);
    Status = string.IsNullOrEmpty(preview)
      ? "Waiting for browser sign-in…"
      : "Waiting for browser sign-in (" + preview + ")…";
    Result<OAuthTokenSet>? result = null;
    Exception? error = null;
    try {
      result = await _auth.SignInAsync(kind, EditorAddress).ConfigureAwait(false);
    }
    catch (Exception ex) {
      error = ex;
    }

    Dispatcher.UIThread.Post(() => {
      IsBusy = false;
      SignInGoogleCommand.NotifyCanExecuteChanged();
      SignInMicrosoftCommand.NotifyCanExecuteChanged();
      ConnectCommand.NotifyCanExecuteChanged();
      GetMessagesCommand.NotifyCanExecuteChanged();
      if (error is not null) {
        Status = error.Message;
        OauthHint = Status;
        return;
      }

      if (result is null) {
        Status = "Sign-in failed.";
        return;
      }

      CompleteSignIn(kind, result);
    });
  }

  private void CompleteSignIn(string kind, Result<OAuthTokenSet> result) {
    if (!result.IsSuccess || result.Value is null) {
      var text = string.Join(" ", result.Messages);
      Status = string.IsNullOrWhiteSpace(text) ? "Sign-in failed." : text;
      OauthHint = Status;
      return;
    }

    _pendingOauth = result.Value;
    if (!string.IsNullOrWhiteSpace(result.Value.Email)) {
      EditorAddress = result.Value.Email;
      EditorUsername = result.Value.Email;
    }

    if (string.IsNullOrWhiteSpace(EditorName))
      EditorName = EditorAddress;
    _applyingProvider = true;
    EditorProvider = ChoiceRow.Provider(
      kind == MailAuthKind.Microsoft ? MailProvider.Outlook : MailProvider.Gmail);
    _applyingProvider = false;
    ApplyProviderPreset();
    UpdateAuthUi();
    if (string.IsNullOrWhiteSpace(EditorAddress) || string.IsNullOrWhiteSpace(EditorIncomingHost)) {
      Status = "Signed in as "
        + (string.IsNullOrWhiteSpace(result.Value.Email) ? "the account" : result.Value.Email)
        + ". Fill Address and incoming host, then Save Account.";
      return;
    }

    WriteMailbox(closeSettings: false);
    Status = "Signed in as "
      + (string.IsNullOrWhiteSpace(result.Value.Email) ? "the account" : result.Value.Email)
      + ". Connecting…";
    _ = ConnectAsync();
  }

  private MailboxAccount MailboxToSave() {
    var byAddress = _files.Current.FindMailboxByAddress(EditorAddress);
    if (byAddress is not null)
      return byAddress;
    if (SelectedMailbox is null)
      return new MailboxAccount();
    if (_pendingOauth is not null
        && !string.IsNullOrWhiteSpace(SelectedMailbox.Address)
        && !SelectedMailbox.Address.Equals(EditorAddress.Trim(), StringComparison.OrdinalIgnoreCase))
      return new MailboxAccount();
    return SelectedMailbox;
  }

  private void ApplyProviderPreset() {
    var box = new MailboxAccount {
      Provider = EditorProvider.Id,
      IncomingProtocol = EditorIncomingProtocol.Id
    };
    MailProvider.Apply(box);
    EditorIncomingHost = box.ImapHost;
    EditorIncomingPort = box.ImapPort;
    EditorIncomingSecurity = ChoiceRow.Security(box.IncomingSecurity);
    EditorSmtpHost = box.SmtpHost;
    EditorSmtpPort = box.SmtpPort;
    EditorSmtpSecurity = ChoiceRow.Security(box.SmtpSecurity);
  }

  private void UpdateAuthUi() {
    var provider = MailProvider.Normalize(EditorProvider.Id);
    ShowOAuthButtons = MailProvider.UsesOAuth(provider);
    ShowGoogleSignIn = provider == MailProvider.Gmail;
    ShowMicrosoftSignIn = provider == MailProvider.Outlook;
    var oauth = _pendingOauth is not null
      || (SelectedMailbox is not null
          && (MailAuthKind.IsOAuth(SelectedMailbox.AuthKind) || _auth.HasTokens(SelectedMailbox.Id)));
    ShowPasswordFields = provider != MailProvider.Outlook || !oauth;
    OauthHint = provider == MailProvider.Gmail
      ? GmailHint()
      : provider == MailProvider.Outlook
        ? OutlookHint()
        : MailProvider.IsPec(provider)
          ? Copy.PecHint
          : "";
  }

  private string GmailHint() {
    var stored = _secrets.Get(FileSecretStore.OAuthClientSecretKey(MailAuthKind.Google));
    var hasSecret = stored.IsSuccess && !string.IsNullOrWhiteSpace(stored.Value);
    return Copy.GmailHintPrefix
      + (hasSecret ? Copy.GmailHintStored : Copy.GmailHintPaste)
      + Copy.GmailHintImap;
  }

  private string OutlookHint() {
    var preview = OAuthClientId.Preview(EditorMicrosoftClientId);
    var usingId = string.IsNullOrEmpty(preview)
      ? Copy.OutlookHintGuid
      : string.Format(Copy.OutlookHintNamed, preview);
    return usingId + Copy.OutlookHintPasswords;
  }

  partial void OnEditorGoogleClientIdChanged(string value) {
    if (ShowGoogleSignIn)
      UpdateAuthUi();
  }

  partial void OnEditorMicrosoftClientIdChanged(string value) {
    if (ShowMicrosoftSignIn)
      UpdateAuthUi();
  }

  private void PersistClientIds(Configuration configuration) {
    configuration.GoogleClientId = OAuthClientId.Normalize(EditorGoogleClientId);
    configuration.MicrosoftClientId = OAuthClientId.Normalize(EditorMicrosoftClientId);
  }

  private void PersistGoogleClientSecret() {
    var secret = OAuthClientSecret.Normalize(EditorGoogleClientSecret);
    if (string.IsNullOrWhiteSpace(secret) && EditorGoogleClientId.TrimStart().StartsWith('{'))
      secret = OAuthClientSecret.Normalize(EditorGoogleClientId);
    if (string.IsNullOrWhiteSpace(secret))
      return;
    _secrets.Put(FileSecretStore.OAuthClientSecretKey(MailAuthKind.Google), secret);
  }

  private string StoredGoogleClientSecret() {
    var stored = _secrets.Get(FileSecretStore.OAuthClientSecretKey(MailAuthKind.Google));
    return stored.IsSuccess ? stored.Value ?? "" : "";
  }

  private void ReloadMailboxes() {
    Mailboxes.Clear();
    foreach (var box in _files.Current.Mailboxes)
      Mailboxes.Add(box);
    var selected = _files.Current.FindMailbox(_files.Current.SelectedMailboxId);
    SelectedMailbox = selected is null
      ? Mailboxes.FirstOrDefault()
      : Mailboxes.FirstOrDefault(m => m.Id == selected.Id);
    RebuildFolderTree();
  }

  private void RebuildFolderTree() {
    _syncingTree = true;
    var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var byId = FolderTree
      .Where(n => n.Mailbox is not null)
      .ToDictionary(n => n.Mailbox!.Id, StringComparer.OrdinalIgnoreCase);
    var index = 0;
    foreach (var box in Mailboxes) {
      keep.Add(box.Id);
      if (!byId.TryGetValue(box.Id, out var node)) {
        node = FolderNodeViewModel.Account(box);
        FolderTree.Insert(index, node);
      }
      else {
        var current = index < FolderTree.Count ? FolderTree[index] : null;
        if (!ReferenceEquals(current, node)) {
          FolderTree.Remove(node);
          FolderTree.Insert(Math.Min(index, FolderTree.Count), node);
        }
      }

      SyncFolderChildren(node, box);
      index++;
    }

    for (var i = FolderTree.Count - 1; i >= 0; i--) {
      var id = FolderTree[i].Mailbox?.Id;
      if (id is null || !keep.Contains(id))
        FolderTree.RemoveAt(i);
    }

    var selectedBoxId = SelectedFolderNode?.Mailbox?.Id ?? SelectedMailbox?.Id;
    var selectedFolder = SelectedFolderNode?.Folder?.FullName ?? SelectedFolder?.FullName;
    SelectedFolderNode = FindTreeNode(selectedBoxId, selectedFolder)
      ?? FolderNodeFor(SelectedMailbox, "inbox")
      ?? FolderTree.FirstOrDefault(n => n.Mailbox?.Id == SelectedMailbox?.Id)?.Children.FirstOrDefault()
      ?? FolderTree.FirstOrDefault(n => n.Mailbox?.Id == SelectedMailbox?.Id);
    _syncingTree = false;
  }

  private void SyncFolderChildren(FolderNodeViewModel node, MailboxAccount box) {
    node.Children.Clear();
    foreach (var folder in FoldersFor(box)
      .OrderBy(f => MailFolderRole.SortKey(f.Name, f.FullName))
      .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
      node.Children.Add(FolderNodeViewModel.ForFolder(box, folder));
    node.NotifyCounts();
  }

  private FolderNodeViewModel? FindTreeNode(string? mailboxId, string? folder) {
    if (string.IsNullOrWhiteSpace(mailboxId))
      return null;
    var account = FolderTree.FirstOrDefault(n => n.Mailbox?.Id == mailboxId);
    if (account is null)
      return null;
    if (string.IsNullOrWhiteSpace(folder))
      return account.Children.FirstOrDefault() ?? account;
    return account.Children.FirstOrDefault(n =>
        n.Folder?.FullName.Equals(folder, StringComparison.OrdinalIgnoreCase) == true)
      ?? account.Children.FirstOrDefault()
      ?? account;
  }

  private FolderNodeViewModel? FolderNodeFor(MailboxAccount? box, string kind) {
    if (box is null)
      return null;
    var account = FolderTree.FirstOrDefault(n => n.Mailbox?.Id == box.Id);
    return account?.Children.FirstOrDefault(n => MailFolderRole.Kind(n.Folder?.Name, n.Folder?.FullName) == kind);
  }

  private async Task LoadFoldersAsync(MailboxAccount box, IMailSession session, CancellationToken token) {
    var result = await session.ListFoldersAsync(token);
    if (!result.IsSuccess) {
      Status = string.Join(" ", result.Messages);
      return;
    }

    var rows = (result.Value ?? [])
      .Select(folder => new FolderRowViewModel {
        FullName = folder.FullName,
        Name = folder.Name,
        Unread = folder.Unread,
        Total = folder.Total
      })
      .ToList();
    _foldersByMailbox[box.Id] = rows;
    RebuildFolderTree();
    if (SelectedMailbox?.Id != box.Id)
      return;
    SyncFoldersCollection(box);
    var wanted = SelectedFolderNode?.Mailbox?.Id == box.Id ? SelectedFolderNode.Folder?.FullName : null;
    var previous = SelectedFolder?.FullName;
    _suppressFolderLoad = true;
    try {
      SelectedFolder = rows.FirstOrDefault(f => f.FullName.Equals(wanted, StringComparison.OrdinalIgnoreCase))
        ?? InboxOf(box)
        ?? rows.FirstOrDefault();
    }
    finally {
      _suppressFolderLoad = false;
    }

    if (SelectedFolder is not null
        && (previous is null
            || !SelectedFolder.FullName.Equals(previous, StringComparison.OrdinalIgnoreCase)))
      await LoadMessagesAsync(SelectedFolder.FullName, token);

    var quota = await session.GetQuotaAsync(token);
    if (quota.IsSuccess && quota.Value is { } info) {
      var line = info.Line();
      if (SelectedMailbox?.Id == box.Id)
        QuotaLine = line;
    }
  }

  private void SyncFoldersCollection(MailboxAccount box) {
    Folders.Clear();
    foreach (var folder in FoldersFor(box))
      Folders.Add(folder);
  }

  private List<FolderRowViewModel> FoldersFor(MailboxAccount? box) {
    if (box is null)
      return [];
    return _foldersByMailbox.TryGetValue(box.Id, out var rows) ? rows : [];
  }

  private FolderRowViewModel? InboxOf(MailboxAccount box) =>
    FoldersFor(box).FirstOrDefault(f => MailFolderRole.Kind(f.Name, f.FullName) == "inbox")
    ?? FoldersFor(box).FirstOrDefault();

  private IMailSession? ActiveSession =>
    SessionFor(SelectedMailbox);

  private IMailSession? SessionFor(MailboxAccount? box) {
    if (box is null)
      return null;
    return _connections.TryGetValue(box.Id, out var session) ? session : null;
  }

  private string PasswordFor(MailboxAccount box) {
    if (SelectedMailbox?.Id == box.Id && !string.IsNullOrWhiteSpace(EditorPassword))
      return EditorPassword;
    var stored = _secrets.Get(FileSecretStore.MailboxKey(box.Id));
    return stored.Value ?? "";
  }

  private async Task<bool> ConnectMailboxCoreAsync(MailboxAccount box, string password, CancellationToken token) {
    lock (_connections) {
      if (!_connecting.Add(box.Id))
        return true;
    }

    try {
      if (SessionFor(box) is { IsConnected: true } live) {
        if (!_foldersByMailbox.ContainsKey(box.Id))
          await LoadFoldersAsync(box, live, token);
        return true;
      }

      var session = _sessions.Create(box);
      var connected = await session.ConnectAsync(box, password, token);
      if (!connected.IsSuccess) {
        await session.DisposeAsync();
        if (SelectedMailbox?.Id == box.Id)
          Status = string.Join(" ", connected.Messages);
        return false;
      }

      if (_connections.Remove(box.Id, out var previous))
        await previous.DisposeAsync();
      _connections[box.Id] = session;
      await LoadFoldersAsync(box, session, token);
      EnsureIndexing(box.Id);
      return true;
    }
    finally {
      lock (_connections)
        _connecting.Remove(box.Id);
    }
  }

  private async Task ConnectIdleMailboxesAsync() {
    foreach (var box in Mailboxes.ToList()) {
      if (SessionFor(box) is { IsConnected: true })
        continue;
      lock (_connections) {
        if (_connecting.Contains(box.Id))
          continue;
      }
      var oauth = MailAuthKind.IsOAuth(box.AuthKind) || _auth.HasTokens(box.Id);
      var password = PasswordFor(box);
      if (string.IsNullOrWhiteSpace(password) && !oauth)
        continue;
      try {
        await ConnectMailboxCoreAsync(box, password, CancellationToken.None);
      }
      catch {
      }
    }
  }

  private void ApplyFolderSync(string mailboxId, string folder, MailFolderSync sync) {
    if (sync.Flags.Count > 0)
      _archive.UpdateFlags(mailboxId, folder, sync.Flags.Select(f => (f.Id, f.IsSeen, f.IsFlagged)));
    _archive.UpsertHeaders(mailboxId, sync.Headers.Select(MailArchiveMap.FromHeader));
    if (sync.Present is null)
      return;
    var keep = sync.Present.ToHashSet();
    var gone = _archive.Uids(mailboxId, folder).Where(uid => !keep.Contains(uid)).ToList();
    if (gone.Count > 0)
      DeleteEmlFiles(_archive.RemoveUids(mailboxId, folder, gone));
  }

  private async Task LoadMessagesAsync(string folder, CancellationToken token, bool resumeBodies = false) {
    var box = SelectedMailbox;
    if (box is null)
      return;
    ShowArchive(box.Id, folder);
    var session = ActiveSession;
    if (session is not { IsConnected: true }) {
      Status = ArchiveFiles.Hint();
      return;
    }

    var known = _archive.Uids(box.Id, folder);
    var result = await session.ListMessagesAsync(folder, known, token);
    if (!result.IsSuccess) {
      Status = string.Join(" ", result.Messages) + " Showing local archive. " + ArchiveFiles.Hint();
      return;
    }

    var sync = result.Value ?? new MailFolderSync();
    ApplyFolderSync(box.Id, folder, sync);
    if (known.Count > 0)
      NoticeNewMail(sync.Headers.Where(h => !known.Contains(h.Id)).ToList());
    ShowArchive(box.Id, folder);
    if (MailFolderRole.Kind(null, folder) == "sent") {
      await IngestReceiptFoldersAsync(token);
      OverlaySentMarks();
    }

    Status = ArchiveFiles.Hint();
    if (resumeBodies)
      EnsureBodyBackfill(box.Id);
  }

  private async Task IngestReceiptFoldersAsync(CancellationToken token) {
    var session = ActiveSession;
    if (session is null || SelectedMailbox is null)
      return;
    foreach (var folder in FoldersFor(SelectedMailbox)) {
      var kind = MailFolderRole.Kind(folder.Name, folder.FullName);
      if (kind is not "inbox" and not "receipts")
        continue;
      var listed = await session.ListMessagesAsync(
        folder.FullName,
        _archive.Uids(SelectedMailbox.Id, folder.FullName),
        token);
      if (!listed.IsSuccess)
        continue;
      var sync = listed.Value ?? new MailFolderSync();
      ApplyFolderSync(SelectedMailbox.Id, folder.FullName, sync);
      foreach (var header in sync.Headers)
        ReconcileHeader(header, folder.FullName);
    }
  }

  private void OverlaySentMarks() {
    if (SelectedMailbox is null)
      return;
    foreach (var row in Messages) {
      row.Header.DeliveryStatus = _receipts.StatusFor(
        SelectedMailbox.Id,
        row.Header.MessageId,
        row.Header.Subject);
      row.RefreshMarks();
    }
  }

  private void ReconcileHeader(MailMessageHeader header, string folder) {
    if (SelectedMailbox is null)
      return;
    var related = header.InReplyTo;
    if (header.EnvelopeKind is EnvelopeKind.PecReceipt or EnvelopeKind.EidasRem)
      _receipts.ApplyEvidence(
        SelectedMailbox.Id,
        related,
        null,
        header.Subject,
        header.EnvelopeTipo);
    if (MailFolderRole.Kind(null, folder) == "sent")
      header.DeliveryStatus = _receipts.StatusFor(SelectedMailbox.Id, header.MessageId, header.Subject);
  }

  private void RebuildVisible() {
    VisibleMessages.Clear();
    var query = MessageFilter.Trim();
    if (SelectedMailbox is null || SelectedFolder is null) {
      RefreshFolderStats();
      return;
    }

    List<MessageRowViewModel> rows;
    if (query.Length == 0)
      rows = [.. Messages];
    else {
      var hits = _archive.Search(SelectedMailbox.Id, SelectedFolder.FullName, query);
      var map = Messages
        .GroupBy(m => m.Header.Id)
        .ToDictionary(g => g.Key, g => g.First());
      rows = [];
      foreach (var hit in hits) {
        if (map.TryGetValue(hit.Uid, out var row))
          rows.Add(row);
      }
    }

    if (!GroupConversations) {
      rows.Sort(CompareRows);
      foreach (var row in rows) {
        row.SetChain(0, 1);
        VisibleMessages.Add(row);
      }
    }
    else {
      var map = rows
        .GroupBy(r => r.Header.Id)
        .ToDictionary(g => g.Key, g => g.First());
      var links = MailChain.Order(rows.Select(ChainItem).ToList(), true);
      if (IsDateSort) {
        if (!_listSortDescending)
          links = MailListOrder.ReverseThreads(links);
      }
      else
        links = MailListOrder.ReorderThreads(links, (a, b) => CompareRows(map[a], map[b]));

      foreach (var link in links) {
        if (!map.TryGetValue(link.Uid, out var row))
          continue;
        row.SetChain(link.Depth, link.Size);
        VisibleMessages.Add(row);
      }
    }

    RefreshFolderStats();
  }

  private static MailChainItem ChainItem(MessageRowViewModel row) =>
    new() {
      Uid = row.Header.Id,
      MessageId = row.Header.MessageId,
      InReplyTo = row.Header.InReplyTo,
      Date = row.Header.Date
    };

  private bool IsDateSort =>
    string.IsNullOrWhiteSpace(_listSortHeader)
    || _listSortHeader.Equals("Date", StringComparison.OrdinalIgnoreCase);

  private int CompareRows(MessageRowViewModel a, MessageRowViewModel b) {
    var cmp = _listSortHeader switch {
      "Unread" => a.UnreadSort.CompareTo(b.UnreadSort),
      "Flag" => a.FlagSort.CompareTo(b.FlagSort),
      "Priority" => a.PrioritySort.CompareTo(b.PrioritySort),
      "Attachments" => a.AttachmentSort.CompareTo(b.AttachmentSort),
      "Delivery" => a.DeliverySort.CompareTo(b.DeliverySort),
      "Type" => string.Compare(a.Badge, b.Badge, StringComparison.OrdinalIgnoreCase),
      "From" => string.Compare(a.From, b.From, StringComparison.OrdinalIgnoreCase),
      "Label" => string.Compare(a.Labels, b.Labels, StringComparison.OrdinalIgnoreCase),
      "Subject" => string.Compare(a.Subject, b.Subject, StringComparison.OrdinalIgnoreCase),
      _ => a.SortDate.CompareTo(b.SortDate)
    };
    if (cmp == 0)
      cmp = a.Header.Id.CompareTo(b.Header.Id);
    return _listSortDescending ? -cmp : cmp;
  }

  private async Task LoadBodyAsync(MailMessageHeader header, CancellationToken token) {
    var box = SelectedMailbox;
    if (box is null)
      return;
    var eml = _archive.EmlPath(box.Id, header.Folder, header.Id)
      ?? ArchiveFiles.EmlPath(box.Id, header.Folder, header.Id);
    var body = await MimeBody.TryFromEmlAsync(header.Folder, header.Id, eml, token);
    if (body is null && ActiveSession is { IsConnected: true } session) {
      var result = await session.GetMessageAsync(header.Folder, header.Id, token);
      if (!result.IsSuccess || result.Value is null) {
        Status = string.Join(" ", result.Messages);
        return;
      }

      body = result.Value;
    }

    if (body is null) {
      Status = "Message is not in the local archive yet. Connect and Get Messages.";
      return;
    }

    StoreArchivedBody(box.Id, header, body);
    _reading = body;
    ApplyReading(body);
    if (SelectedMailbox is not null
        && (body.Envelope.Kind == EnvelopeKind.PecReceipt || body.Envelope.Kind == EnvelopeKind.EidasRem)) {
      _receipts.ApplyEvidence(
        SelectedMailbox.Id,
        body.Envelope.Msgid,
        body.Envelope.Identificativo,
        body.Envelope.Oggetto,
        body.Envelope.Tipo);
      if (SelectedFolder is not null
          && MailFolderRole.Kind(SelectedFolder.Name, SelectedFolder.FullName) == "sent")
        OverlaySentMarks();
      var explain = ReceiptStatus.Explain(body.Envelope.Tipo);
      if (!string.IsNullOrWhiteSpace(explain))
        Status = explain;
    }
    await MarkOpenedReadAsync(header, token);
    if (_pendingWindow is { } pending
        && pending.Header.Id == header.Id
        && pending.Header.Folder.Equals(header.Folder, StringComparison.OrdinalIgnoreCase)) {
      _pendingWindow = null;
      ShowMessageWindow(body);
    }
  }

  private bool IsLoadedBody(MailMessageHeader header) =>
    _reading is { } body
    && body.Header.Id == header.Id
    && body.Header.Folder.Equals(header.Folder, StringComparison.OrdinalIgnoreCase);

  private void ShowMessageWindow(MailMessageBody body) {
    if (!TryComposeIdentities(out var identities, out var selected) || selected is null)
      return;
    var vm = new MessageWindowViewModel(
      body,
      identities,
      selected,
      UnwrapEnvelope,
      _bodyPreference,
      HtmlEngineReady,
      EnsureComposeSessionAsync);
    vm.ComposeRequested += compose => {
      compose.Sent += RememberSent;
      ComposeRequested?.Invoke(compose);
    };
    MessageWindowRequested?.Invoke(vm);
  }

  private async Task MarkOpenedReadAsync(MailMessageHeader header, CancellationToken token) {
    var row = Messages.FirstOrDefault(m =>
      m.Header.Id == header.Id
      && m.Header.Folder.Equals(header.Folder, StringComparison.OrdinalIgnoreCase));
    if (row is null || row.Header.IsSeen || SelectedFolder is null)
      return;
    var session = ActiveSession;
    if (session is not { IsConnected: true })
      return;
    if (session.SupportsFolders) {
      var result = await session.SetMessageFlagsAsync(
        SelectedFolder.FullName,
        [row.Header.Id],
        new MailFlagUpdate { Seen = true },
        token);
      if (!result.IsSuccess)
        return;
    }

    ApplyUnreadDelta(SelectedFolder, [row], true);
    row.Header.IsSeen = true;
    row.RefreshMarks();
    if (SelectedMailbox is not null)
      _archive.UpsertHeaders(SelectedMailbox.Id, [MailArchiveMap.FromHeader(row.Header)]);
    NotifyFolderCounts();
    RefreshFolderStats();
  }

  private void ApplyReading(MailMessageBody body, bool keepKind = false) {
    var inner = UnwrapEnvelope && body.Envelope.HasInnerMessage;
    ReadingSubject = inner && !string.IsNullOrWhiteSpace(body.InnerSubject)
      ? body.InnerSubject
      : body.Header.Subject;
    ReadingFrom = inner && !string.IsNullOrWhiteSpace(body.InnerFrom)
      ? body.InnerFrom
      : body.Header.From;
    ReadingTo = inner && !string.IsNullOrWhiteSpace(body.InnerTo)
      ? body.InnerTo
      : body.To;
    ReadingCc = inner && !string.IsNullOrWhiteSpace(body.InnerCc)
      ? body.InnerCc
      : body.Cc;
    HasReadingCc = !string.IsNullOrWhiteSpace(ReadingCc);
    ReadingDate = body.Header.Date == DateTimeOffset.MinValue
      ? ""
      : body.Header.Date.ToLocalTime().ToString("f");
    var files = inner ? body.InnerAttachmentFiles : body.AttachmentFiles;
    var html = inner ? body.InnerHtml : body.Html;
    var text = inner ? body.InnerText : body.Text;
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
    ReadingSource = body.RawEml.Length == 0
      ? ""
      : System.Text.Encoding.UTF8.GetString(body.RawEml);
    ReadingEnvelope = body.Envelope.Line;
    HasReadingEnvelope = !string.IsNullOrWhiteSpace(ReadingEnvelope);
    ReadingFiles.Clear();
    foreach (var file in files) {
      if (IsInlineImage(file, html ?? ""))
        continue;
      ReadingFiles.Add(file);
    }
    HasReadingFiles = ReadingFiles.Count > 0;
    HasReading = true;
    var fattura = FatturaPaDocument.FromAttachments(files);
    ReadingFattura = fattura?.Text ?? "";
    HasReadingFattura = fattura is not null;
    if (!keepKind)
      ApplyBodyKind(fattura is not null
        ? MailBodyKind.Fattura
        : MailBodyKind.DefaultView(html, inner ? body.InnerText : body.Text, _bodyPreference));
  }

  private static bool IsInlineImage(MailFileAttachment file, string html) {
    if (string.IsNullOrWhiteSpace(file.ContentId))
      return false;
    if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
      return false;
    return html.Contains("cid:" + file.ContentId, StringComparison.OrdinalIgnoreCase);
  }

  private void ClearReading() {
    _reading = null;
    _pendingWindow = null;
    HasReading = false;
    HasReadingCc = false;
    ReadingSubject = "";
    ReadingFrom = "";
    ReadingTo = "";
    ReadingCc = "";
    ReadingDate = "";
    ReadingBody = "";
    ReadingHtmlDocument = "";
    ReadingSource = "";
    ReadingEnvelope = "";
    HasReadingEnvelope = false;
    HasReadingFiles = false;
    HasReadingFattura = false;
    ReadingFattura = "";
    ReadingFiles.Clear();
  }

  private void StoreArchivedBody(string mailboxId, MailMessageHeader header, MailMessageBody body) {
    if (body.RawEml.Length == 0)
      return;
    try {
      var path = ArchiveFiles.EmlPath(mailboxId, header.Folder, header.Id);
      File.WriteAllBytes(path, body.RawEml);
      _archive.UpsertBody(
        mailboxId,
        MailArchiveMap.FromHeader(header),
        path,
        MailArchiveMap.BodyText(body, UnwrapEnvelope),
        MailArchiveMap.AttachmentIndex(body, UnwrapEnvelope));
    }
    catch {
    }
  }

  private static void ShowArchiveHint() {
  }

  private void ShowArchive(string mailboxId, string folder) {
    Messages.Clear();
    SelectedMessage = null;
    ClearReading();
    foreach (var row in _archive.ListFolder(mailboxId, folder)) {
      var header = MailArchiveMap.ToHeader(row);
      ReconcileHeader(header, folder);
      Messages.Add(new MessageRowViewModel { Header = header });
    }

    ApplyOpenedFolderCounts();
    RebuildVisible();
  }

  private void NoticeNewMail(IReadOnlyList<MailMessageHeader> newcomers) {
    if (newcomers.Count == 0)
      return;
    RaiseNotice(newcomers.Where(h => h.EnvelopeKind == EnvelopeKind.PecTransport).ToList(), "PEC arrived");
    RaiseNotice(newcomers.Where(h => h.EnvelopeKind == EnvelopeKind.PecReceipt).ToList(), "Ricevuta arrived");
    RaiseNotice(newcomers.Where(h => h.EnvelopeKind == EnvelopeKind.EidasRem).ToList(), "REM arrived");
  }

  private void RaiseNotice(IReadOnlyList<MailMessageHeader> rows, string title) {
    if (rows.Count == 0)
      return;
    var body = rows.Count == 1 ? rows[0].Subject : rows.Count + " new messages";
    NoticeRequested?.Invoke(title, body);
  }

  private void StopBackfill() {
    _indexMailboxes.Clear();
    _catalogDone.Clear();
    _indexMailboxId = null;
    _backfill?.Cancel();
    _backfill?.Dispose();
    _backfill = null;
    IndexLine = "";
  }

  private void EnsureIndexing(string mailboxId) {
    if (string.IsNullOrWhiteSpace(mailboxId))
      return;
    _indexMailboxes.Add(mailboxId);
    if (_backfill is not null)
      return;
    StartIndexing();
  }

  private void EnsureBodyBackfill(string mailboxId) {
    if (string.IsNullOrWhiteSpace(mailboxId) || _backfill is not null)
      return;
    if (_archive.MissingBodies(mailboxId).Count == 0)
      return;
    EnsureIndexing(mailboxId);
  }

  private void StopIndexingMailbox(string mailboxId) {
    _indexMailboxes.Remove(mailboxId);
    _catalogDone.Remove(mailboxId);
    if (!mailboxId.Equals(_indexMailboxId, StringComparison.OrdinalIgnoreCase))
      return;
    _backfill?.Cancel();
    _backfill?.Dispose();
    _backfill = null;
    _indexMailboxId = null;
    IndexLine = "";
    StartIndexing();
  }

  private void StartIndexing() {
    var mailboxId = _indexMailboxes.FirstOrDefault();
    if (mailboxId is null)
      return;
    var cts = new CancellationTokenSource();
    _backfill = cts;
    _indexMailboxId = mailboxId;
    _ = IndexMailboxAsync(mailboxId, cts.Token);
  }

  private async Task IndexMailboxAsync(string mailboxId, CancellationToken token) {
    try {
      var snapshot = await Dispatcher.UIThread.InvokeAsync(() => {
        var box = MailboxById(mailboxId);
        return (
          Session: SessionFor(box),
          Folders: FoldersFor(box).Select(f => (f.FullName, f.Name)).ToList(),
          OpenFolder: SelectedMailbox?.Id == mailboxId ? SelectedFolder?.FullName : null,
          CatalogDone: _catalogDone.Contains(mailboxId));
      });
      if (snapshot.Session is not { IsConnected: true })
        return;

      if (snapshot.Session.SupportsFolders && !snapshot.CatalogDone) {
        foreach (var folder in snapshot.Folders) {
          token.ThrowIfCancellationRequested();
          var display = MailFolderRole.DisplayName(folder.Name, folder.FullName);
          await SetIndexLineAsync(MailIndexProgress.Line(0, 0, display), token);
          var open = snapshot.OpenFolder is not null
            && folder.FullName.Equals(snapshot.OpenFolder, StringComparison.OrdinalIgnoreCase);
          if (open)
            continue;
          var known = _archive.Uids(mailboxId, folder.FullName);
          var listed = await snapshot.Session.ListMessagesAsync(folder.FullName, known, token);
          if (!listed.IsSuccess)
            continue;
          ApplyFolderSync(mailboxId, folder.FullName, listed.Value ?? new MailFolderSync());
          await Dispatcher.UIThread.InvokeAsync(() => RefreshIndexedFolderCounts(mailboxId, folder.FullName));
        }

        await Dispatcher.UIThread.InvokeAsync(() => _catalogDone.Add(mailboxId));
      }

      while (!token.IsCancellationRequested) {
        var missing = _archive.MissingBodies(mailboxId);
        if (missing.Count == 0)
          break;
        var total = missing.Count;
        var done = 0;
        await SetIndexLineAsync(MailIndexProgress.Line(0, total), token);
        foreach (var item in missing) {
          token.ThrowIfCancellationRequested();
          try {
            var result = await snapshot.Session.GetMessageAsync(item.Folder, item.Uid, token);
            if (result.IsSuccess && result.Value is not null)
              StoreArchivedBody(mailboxId, result.Value.Header, result.Value);
          }
          catch (OperationCanceledException) {
            throw;
          }
          catch {
          }

          done++;
          if (done == 1 || done == total || done % 10 == 0) {
            var display = MailFolderRole.DisplayName(null, item.Folder);
            await SetIndexLineAsync(MailIndexProgress.Line(done, total, display), token);
          }
        }
      }
    }
    catch (OperationCanceledException) {
      return;
    }
    finally {
      await Dispatcher.UIThread.InvokeAsync(() => {
        if (_indexMailboxId != mailboxId)
          return;
        _backfill?.Dispose();
        _backfill = null;
        _indexMailboxId = null;
        if (token.IsCancellationRequested)
          _indexMailboxes.Remove(mailboxId);
        else if (_archive.MissingBodies(mailboxId).Count == 0)
          _indexMailboxes.Remove(mailboxId);
        if (!token.IsCancellationRequested)
          IndexLine = "";
        StartIndexing();
      });
    }
  }

  private async Task SetIndexLineAsync(string line, CancellationToken token) =>
    await Dispatcher.UIThread.InvokeAsync(() => {
      if (!token.IsCancellationRequested)
        IndexLine = line;
    });

  private MailboxAccount? MailboxById(string mailboxId) =>
    Mailboxes.FirstOrDefault(m => m.Id.Equals(mailboxId, StringComparison.OrdinalIgnoreCase))
    ?? _files.Current.FindMailbox(mailboxId);

  private void RefreshIndexedFolderCounts(string mailboxId, string folder) {
    var rows = _archive.ListFolder(mailboxId, folder);
    var view = FoldersFor(MailboxById(mailboxId))
      .FirstOrDefault(f => f.FullName.Equals(folder, StringComparison.OrdinalIgnoreCase));
    if (view is null)
      return;
    view.Total = rows.Count;
    view.Unread = rows.Count(r => !r.IsSeen);
    NotifyFolderCounts();
  }

  private async Task DisconnectMailboxAsync(string mailboxId) {
    StopIndexingMailbox(mailboxId);
    if (_connections.Remove(mailboxId, out var session))
      await session.DisposeAsync();
    _foldersByMailbox.Remove(mailboxId);
    if (SelectedMailbox?.Id == mailboxId) {
      Messages.Clear();
      RebuildVisible();
      ClearReading();
      Folders.Clear();
      FolderStats = "";
    }

    RebuildFolderTree();
  }

  private void NotifyMessageCommands() {
    ReplyCommand.NotifyCanExecuteChanged();
    ReplyAllCommand.NotifyCanExecuteChanged();
    ForwardCommand.NotifyCanExecuteChanged();
    MoveToFolderCommand.NotifyCanExecuteChanged();
    MarkReadCommand.NotifyCanExecuteChanged();
    MarkUnreadCommand.NotifyCanExecuteChanged();
    ToggleFlagCommand.NotifyCanExecuteChanged();
    SetPriorityCommand.NotifyCanExecuteChanged();
    DeleteMessagesCommand.NotifyCanExecuteChanged();
    OpenMessageWindowCommand.NotifyCanExecuteChanged();
    PrintMessageCommand.NotifyCanExecuteChanged();
    SaveMessagePdfCommand.NotifyCanExecuteChanged();
    SaveAttachmentsZipCommand.NotifyCanExecuteChanged();
    NotifyFolderCommands();
  }

  private void NotifyFolderCommands() {
    CreateFolderCommand.NotifyCanExecuteChanged();
    EmptyFolderCommand.NotifyCanExecuteChanged();
    MarkFolderReadCommand.NotifyCanExecuteChanged();
    MarkFolderUnreadCommand.NotifyCanExecuteChanged();
    DeleteCustomFolderCommand.NotifyCanExecuteChanged();
  }

  partial void OnHasReadingFilesChanged(bool value) =>
    SaveAttachmentsZipCommand.NotifyCanExecuteChanged();

  private IReadOnlyList<MessageRowViewModel> TargetRows() =>
    SelectedMessages.Count > 0
      ? SelectedMessages.ToList()
      : SelectedMessage is { } one ? [one] : [];

  private IReadOnlyList<uint> TargetIds(IReadOnlyList<MessageRowViewModel>? rows = null) =>
    (rows ?? TargetRows()).Select(r => r.Header.Id).ToList();

  private async Task MoveRowsAsync(IReadOnlyList<MessageRowViewModel> rows, string destFolder) {
    if (rows.Count == 0)
      return;
    await MoveIdsAsync(TargetIds(rows), destFolder, rows: rows);
  }

  private async Task MoveIdsAsync(
    IReadOnlyList<uint> ids,
    string destFolder,
    string? fromFolder = null,
    IReadOnlyList<MessageRowViewModel>? rows = null) {
    var session = ActiveSession;
    if (session is not { IsConnected: true, SupportsFolders: true })
      return;
    var source = fromFolder ?? SelectedFolder?.FullName;
    if (string.IsNullOrWhiteSpace(source) || ids.Count == 0)
      return;
    if (source.Equals(destFolder, StringComparison.OrdinalIgnoreCase)) {
      Status = "Already in that folder.";
      return;
    }

    await RunAsync(Copy.Moving, async token => {
      var result = await session.MoveMessagesAsync(source, ids, destFolder, token);
      if (!result.IsSuccess) {
        Status = string.Join(" ", result.Messages);
        return;
      }

      ApplyFolderMoveCounts(source, destFolder, ids.Count, rows);
      if (SelectedMailbox is not null)
        DeleteEmlFiles(_archive.RemoveUids(SelectedMailbox.Id, source, ids.ToList()));
      if (SelectedFolder is not null)
        await LoadMessagesAsync(SelectedFolder.FullName, token);
      Status = $"Moved {ids.Count} message(s).";
    });
  }

  private async Task<bool> ApplyFlagsAsync(
    MailFlagUpdate update,
    Action<MessageRowViewModel>? apply,
    IReadOnlyList<MessageRowViewModel>? rows = null) {
    var session = ActiveSession;
    if (session is not { IsConnected: true, SupportsFolders: true } || SelectedFolder is null)
      return false;
    var targets = rows ?? TargetRows();
    if (targets.Count == 0)
      return false;
    var ok = false;
    await RunAsync(Copy.UpdatingFlags, async token => {
      var result = await session.SetMessageFlagsAsync(
        SelectedFolder.FullName,
        TargetIds(targets),
        update,
        token);
      if (!result.IsSuccess) {
        Status = string.Join(" ", result.Messages);
        return;
      }

      if (update.Seen is { } seen)
        ApplyUnreadDelta(SelectedFolder, targets, seen);
      if (apply is not null) {
        foreach (var row in targets) {
          apply(row);
          row.RefreshMarks();
        }

        if (SelectedMailbox is not null)
          _archive.UpdateFlags(
            SelectedMailbox.Id,
            SelectedFolder.FullName,
            targets.Select(r => (r.Header.Id, r.Header.IsSeen, r.Header.IsFlagged)));
      }

      NotifyFolderCounts();
      RefreshFolderStats();
      Status = "Updated.";
      ok = true;
    });
    return ok;
  }

  private void RemoveRows(IReadOnlyList<MessageRowViewModel> rows) {
    ApplyRemovedCounts(rows);
    foreach (var row in rows) {
      Messages.Remove(row);
      VisibleMessages.Remove(row);
    }

    if (SelectedMessage is not null && rows.Contains(SelectedMessage))
      SelectedMessage = VisibleMessages.FirstOrDefault();
    SelectedMessages.Clear();
    RefreshFolderStats();
  }

  private void ApplyOpenedFolderCounts() {
    if (SelectedFolder is null)
      return;
    var unread = Messages.Count(m => !m.Header.IsSeen);
    SelectedFolder.Total = Messages.Count;
    SelectedFolder.Unread = unread;

    NotifyFolderCounts();
  }

  private void ApplyUnreadDelta(FolderRowViewModel folder, IReadOnlyList<MessageRowViewModel> rows, bool seen) {
    var change = seen
      ? rows.Count(r => !r.Header.IsSeen)
      : rows.Count(r => r.Header.IsSeen);
    folder.Unread = Math.Max(0, folder.Unread + (seen ? -change : change));
  }

  private void ApplyRemovedCounts(IReadOnlyList<MessageRowViewModel> rows) {
    if (SelectedFolder is not { } folder)
      return;
    folder.Total = Math.Max(0, folder.Total - rows.Count);
    folder.Unread = Math.Max(0, folder.Unread - rows.Count(r => !r.Header.IsSeen));
    NotifyFolderCounts();
  }

  private void ApplyFolderMoveCounts(
    string source,
    string destFolder,
    int count,
    IReadOnlyList<MessageRowViewModel>? rows) {
    var unread = rows?.Count(r => !r.Header.IsSeen) ?? 0;
    var from = FolderRow(source);
    var dest = FolderRow(destFolder);
    if (from is not null) {
      from.Total = Math.Max(0, from.Total - count);
      from.Unread = Math.Max(0, from.Unread - unread);
    }

    if (dest is not null) {
      dest.Total += count;
      dest.Unread += unread;
    }

    NotifyFolderCounts();
  }

  private FolderRowViewModel? FolderRow(string fullName) =>
    FoldersFor(SelectedMailbox).FirstOrDefault(f =>
      f.FullName.Equals(fullName, StringComparison.OrdinalIgnoreCase));

  private void NotifyFolderCounts() {
    foreach (var account in FolderTree) {
      foreach (var child in account.Children)
        child.NotifyCounts();
      account.NotifyCounts();
    }
  }

  private void RefreshFolderStats() {
    if (SelectedFolder is null) {
      FolderStats = "";
      return;
    }

    FolderStats = MailFolderStats.Line(
      SelectedFolder.Name,
      SelectedFolder.Total,
      SelectedFolder.Unread,
      VisibleMessages.Count);
  }

  private static IReadOnlyList<MailRecipient> Recipients(string? text) =>
    RecipientParser.Parse(text);

  private static List<MailRecipient> DistinctRecipients(IEnumerable<MailRecipient> rows, string self) =>
    rows
      .Where(r => r.IsValid && !r.Address.Equals(self, StringComparison.OrdinalIgnoreCase))
      .DistinctBy(r => r.Address, StringComparer.OrdinalIgnoreCase)
      .ToList();

  private static string JoinRecipients(IEnumerable<MailRecipient> rows) =>
    string.Join(", ", rows.Select(r => r.Tooltip));

  private async Task RunAsync(string busy, Func<CancellationToken, Task> work) {
    _work?.Cancel();
    _work?.Dispose();
    _work = new CancellationTokenSource();
    IsBusy = true;
    ConnectCommand.NotifyCanExecuteChanged();
    GetMessagesCommand.NotifyCanExecuteChanged();
    SignInGoogleCommand.NotifyCanExecuteChanged();
    SignInMicrosoftCommand.NotifyCanExecuteChanged();
    NotifyMessageCommands();
    Status = busy;
    try {
      await work(_work.Token);
    }
    catch (OperationCanceledException) {
      Status = Copy.Cancelled;
    }
    finally {
      IsBusy = false;
      ConnectCommand.NotifyCanExecuteChanged();
      GetMessagesCommand.NotifyCanExecuteChanged();
      SignInGoogleCommand.NotifyCanExecuteChanged();
      SignInMicrosoftCommand.NotifyCanExecuteChanged();
      NotifyMessageCommands();
    }
  }
}


public sealed class PromptRequest {
  public required string Title { get; init; }

  public required string Message { get; init; }

  public string Placeholder { get; init; } = "";

  public bool ConfirmOnly { get; init; }
}
