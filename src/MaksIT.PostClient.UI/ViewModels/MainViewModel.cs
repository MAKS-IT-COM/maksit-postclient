using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaksIT.Results;
using MaksIT.PostClient.Client;
using MaksIT.PostClient.Shared;
using MaksIT.PostClient.UI;


namespace MaksIT.PostClient.UI.ViewModels;


public partial class MainViewModel : ObservableObject, IAsyncDisposable {
  private readonly ConfigurationFileService _files;
  private readonly ISecretStore _secrets;
  private readonly IMailSessionFactory _sessions;
  private readonly IMailAuthService _auth;
  private readonly ISentReceiptStore _receipts;
  private readonly MailArchiveCatalog _archive;
  private readonly MailWorkerClient _worker;
  private readonly ISemanticSearchService _semantic;
  private readonly IAppUpdateService _updates;
  private readonly Dictionary<string, IMailSession> _connections = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, List<FolderRowViewModel>> _foldersByMailbox = new(StringComparer.OrdinalIgnoreCase);
  private readonly HashSet<string> _connecting = new(StringComparer.OrdinalIgnoreCase);
  private CancellationTokenSource? _work;
  private CancellationTokenSource? _readingLoad;
  private CancellationTokenSource? _folderLoad;
  private CancellationTokenSource? _backfill;
  private CancellationTokenSource? _search;
  private int _searchGen;
  private int _indexPause;
  private bool _syncingList;
  private readonly HashSet<string> _indexMailboxes = new(StringComparer.OrdinalIgnoreCase);
  private readonly HashSet<string> _catalogDone = new(StringComparer.OrdinalIgnoreCase);
  private readonly HashSet<string> _catalogFolders = new(StringComparer.OrdinalIgnoreCase);
  private const int IndexCatalogChunk = 2_000;
  private const int IndexUiChunk = 250;
  private readonly StatusLineHold _indexHold;
  private readonly StatusLineHold _semanticHold;
  private bool _uiReady;
  private MailMessageBody? _reading;
  private MessageRowViewModel? _pendingWindow;
  private bool _syncingTree;
  private bool _suppressFolderLoad;
  private bool _applyingProvider;
  private bool _accountSettingsOpen;
  private bool _syncingBodyKind;
  private bool _updateBusy;
  private string _bodyPreference = MailBodyKind.Html;
  private string _listSortHeader = "Date";
  private bool _listSortDescending = true;
  private OAuthTokenSet? _pendingOauth;

  public ISemanticSearchService SemanticSearch =>
    _semantic;

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
  private ChoiceRow editorCertifiedKind = ChoiceRow.Certified(MailCertifiedKind.Ordinary);

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
  private string editorStorePath = "";

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
  [NotifyPropertyChangedFor(nameof(ShowIndexLine))]
  private string indexLine = "";

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(ShowSemanticLine))]
  private string semanticLine = "";

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

  public List<FolderNodeViewModel> SelectedFolderNodes { get; } = [];

  public IReadOnlyList<ChoiceRow> IncomingProtocols =>
    ChoiceRow.IncomingProtocols;

  public IReadOnlyList<ChoiceRow> SecurityModes =>
    ChoiceRow.SecurityModes;

  public IReadOnlyList<ChoiceRow> Providers =>
    ChoiceRow.ProvidersFor(_files.Current.Features, EditorProvider.Id);

  public IReadOnlyList<ChoiceRow> CertifiedKinds =>
    ChoiceRow.CertifiedKinds;

  public ObservableCollection<ChoiceRow> ArchiveStoreChoices { get; } = [];

  [ObservableProperty]
  private ChoiceRow editorArchiveStore = new() { Id = "", Title = "" };

  public bool ShowPstFields =>
    false;

  public bool ShowServerFields =>
    !MailProvider.IsLocalBucket(EditorProvider.Id);

  public bool ShowArchiveStore =>
    ShowServerFields;

  public bool ShowCertifiedKind =>
    ShowServerFields && MailProvider.Normalize(EditorProvider.Id) == MailProvider.Imap;

  public IReadOnlyList<ChoiceRow> Languages { get; } =
    UiLanguage.All.Select(id => new ChoiceRow { Id = id, Title = UiLanguage.Title(id) }).ToList();

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

  public bool IsFrench =>
    UiLocale.Id == UiLanguage.Fr;

  public bool IsGerman =>
    UiLocale.Id == UiLanguage.De;

  public bool IsSpanish =>
    UiLocale.Id == UiLanguage.Es;

  public bool HtmlEngineReady { get; private set; } = true;

  public bool ShowHtmlBody =>
    HasReading && HtmlEngineReady && MailBodyKind.IsHtml(ReadingBodyKind);

  public bool ShowTextBody =>
    HasReading && (MailBodyKind.IsText(ReadingBodyKind)
      || (!HtmlEngineReady && MailBodyKind.IsHtml(ReadingBodyKind)));

  public bool ShowSourceBody =>
    HasReading && MailBodyKind.IsSource(ReadingBodyKind);

  public bool ShowFatturaBody =>
    HasReading && UseFatturaPa && MailBodyKind.IsFattura(ReadingBodyKind);

  public bool UseFascicolo =>
    FeatureGate.On(AppFeature.Fascicolo);

  public bool UseFatturaPa =>
    FeatureGate.On(AppFeature.FatturaPa);

  public bool UseEnvelopeTools =>
    FeatureGate.On(AppFeature.PecEnvelope) || FeatureGate.On(AppFeature.RemEvidence);

  public bool UseDeliveryColumn =>
    UseEnvelopeTools
    || FeatureGate.On(AppFeature.FrEvidence)
    || FeatureGate.On(AppFeature.DeEvidence)
    || FeatureGate.On(AppFeature.EsEvidence)
    || FeatureGate.On(AppFeature.ChEvidence);

  public bool UseTypeColumn =>
    UseEnvelopeTools;

  public bool ShowFatturaPaTools =>
    HasReadingFattura && UseFatturaPa;

  public bool IsHtmlKind =>
    MailBodyKind.IsHtml(ReadingBodyKind);

  public bool IsTextKind =>
    MailBodyKind.IsText(ReadingBodyKind);

  public bool IsSourceKind =>
    MailBodyKind.IsSource(ReadingBodyKind);

  public bool IsFatturaKind =>
    MailBodyKind.IsFattura(ReadingBodyKind);

  public bool IsWideLayout {
    get => MailLayout.IsWide(ReadingLayout);
    set => ReadingLayout = value ? MailLayout.Wide : MailLayout.Stacked;
  }

  public bool IsStackedLayout {
    get => MailLayout.IsStacked(ReadingLayout);
    set {
      if (value)
        ReadingLayout = MailLayout.Stacked;
      else
        OnPropertyChanged(nameof(IsStackedLayout));
    }
  }

  public string LayoutToggleLabel =>
    IsWideLayout ? Copy.ThreeColumns : Copy.ListReading;

  public event Action<ComposeViewModel>? ComposeRequested;

  public event Action<MessageWindowViewModel>? MessageWindowRequested;

  public event Action? AccountSettingsRequested;

  public event Action? FeaturesRequested;

  public event Action? SemanticSearchRequested;

  public event Action? MessageListSelectionRestoreRequested;

  public event Action? FolderTreeSelectionRestoreRequested;

  public event Action? RulesRequested;

  public event Action? AccountSaved;

  public event Action<string, string>? NoticeRequested;

  public event Action? AboutRequested;

  public event Action? LogsRequested;

  public bool ShowIndexLine =>
    !string.IsNullOrWhiteSpace(IndexLine);

  public bool ShowSemanticLine =>
    !string.IsNullOrWhiteSpace(SemanticLine);

  public string EmptyFolderLabel {
    get {
      foreach (var node in TargetFolderNodes()) {
        if (MailRetention.IsTrash(node.Folder?.Name, node.Folder?.FullName))
          return Copy.DeleteAllItems;
      }

      return Copy.EmptyFolder;
    }
  }

  public event Func<PromptRequest, Task<string?>>? PromptRequested;

  public event Action? ExportArchiveRequested;

  public event Action? ExportFascicoloRequested;

  public event Action? ImportEmlRequested;

  public event Action? ImportPstRequested;

  public event Action? AttachPstRequested;

  public event Action? CreatePstRequested;

  public event Action<string>? IdentityHubSignInRequested;

  public event Action? RetentionRequested;

  public event Func<Task<string?>>? MoveStorePathRequested;

  public event Func<Task<string?>>? PickStorePathRequested;

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
    MailArchiveCatalog archive,
    MailWorkerClient worker,
    ISemanticSearchService semantic,
    IAppUpdateService updates) {
    _files = files;
    _secrets = secrets;
    _sessions = sessions;
    _auth = auth;
    _receipts = receipts;
    _archive = archive;
    _worker = worker;
    _semantic = semantic;
    _updates = updates;
    _indexHold = new StatusLineHold(line => IndexLine = line);
    _semanticHold = new StatusLineHold(line => SemanticLine = line);
    files.Current.EnsureDefaults();
    PstStoreMigrator.Migrate(files, archive);
    MailArchiveCatalog.MigrateLegacy(files.Current.Mailboxes);
    archive.OpenAll(files.Current.Mailboxes);
    FeatureGate.Use(files.Current.Features);
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
    MarkExistingSyncComplete();
    semanticLine = semantic.StatusLine;
    semantic.Changed += OnSemanticChanged;
    semantic.Faulted += OnSemanticFaulted;
  }

  public void StartBackgroundWork() {
    if (_uiReady)
      return;
    _uiReady = true;
    _semantic.Start();
    if (SelectedMailbox is not null && !AccountSettingsOpen)
      _ = ConnectAsync();
  }

  public async ValueTask DisposeAsync() {
    StopBackfill();
    _folderLoad?.Cancel();
    _folderLoad?.Dispose();
    _folderLoad = null;
    _search?.Cancel();
    _search?.Dispose();
    _search = null;
    _readingLoad?.Cancel();
    _readingLoad?.Dispose();
    _readingLoad = null;
    _work?.Cancel();
    _semantic.Changed -= OnSemanticChanged;
    _semantic.Faulted -= OnSemanticFaulted;
    _semantic.Dispose();
    _indexHold.Dispose();
    _semanticHold.Dispose();
    List<IMailSession> sessions;
    lock (_connections) {
      sessions = [.. _connections.Values];
      _connections.Clear();
    }

    foreach (var session in sessions)
      await session.DisposeAsync();
    _work?.Dispose();
    _archive.Dispose();
    _worker.Dispose();
  }

  private void OnSemanticFaulted(Exception exception) =>
    Dispatcher.UIThread.Post(() => ErrorDialog.Report(exception));

  private void OnSemanticChanged() =>
    Dispatcher.UIThread.Post(() => {
      var line = _semantic.StatusLine;
      if (line.Contains("ready", StringComparison.OrdinalIgnoreCase))
        line = "";
      _semanticHold.Set(line);
    });

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
    EditorCertifiedKind = ChoiceRow.Certified(MailCertifiedKind.Ordinary);
    _applyingProvider = false;
    EditorIncomingProtocol = ChoiceRow.Incoming(MailProtocol.Imap);
    EditorIncomingSecurity = ChoiceRow.Security(MailSecurity.Ssl);
    EditorIncomingHost = "";
    EditorStorePath = "";
    EditorIncomingPort = MailSecurity.DefaultIncomingPort(MailProtocol.Imap, MailSecurity.Ssl);
    EditorSmtpSecurity = ChoiceRow.Security(MailSecurity.Ssl);
    EditorSmtpHost = "";
    EditorSmtpPort = MailSecurity.DefaultSmtpPort(MailSecurity.Ssl);
    UpdateAuthUi();
    AccountSettingsRequested?.Invoke();
  }

  [RelayCommand]
  private void OpenFeatures() =>
    FeaturesRequested?.Invoke();

  [RelayCommand]
  private void OpenSemanticSearch() =>
    SemanticSearchRequested?.Invoke();

  [RelayCommand]
  private void OpenRules() =>
    RulesRequested?.Invoke();

  [RelayCommand]
  private void ShowAbout() =>
    AboutRequested?.Invoke();

  [RelayCommand]
  private void ShowLogs() =>
    LogsRequested?.Invoke();

  [RelayCommand(CanExecute = nameof(CanCheckForUpdates))]
  private async Task CheckForUpdatesAsync() {
    _updateBusy = true;
    CheckForUpdatesCommand.NotifyCanExecuteChanged();
    Status = Copy.UpdateChecking;
    try {
      var check = await _updates.CheckAsync();
      if (!check.IsSuccess) {
        var msg = string.Join(" ", check.Messages);
        Status = msg.Contains("404", StringComparison.Ordinal)
          ? Copy.UpdateNone
          : string.Format(Copy.UpdateFailed, msg);
        return;
      }

      var info = check.Value;
      if (info is null || !info.IsNewer) {
        Status = string.Format(Copy.UpdateLatest, AppVersion.Display());
        return;
      }

      var latest = info.Release.VersionText;
      var installed = AppVersion.Format(info.Installed);
      var hasAsset = info.Asset is not null;
      var accepted = PromptRequested is null
        ? null
        : await PromptRequested(new PromptRequest {
            Title = Copy.CheckUpdates.Replace("_", "", StringComparison.Ordinal),
            Message = hasAsset
              ? string.Format(Copy.UpdateAvailable, latest, installed)
              : string.Format(Copy.UpdateNoAsset, latest, installed),
            ConfirmOnly = true,
            ConfirmLabel = hasAsset ? Copy.UpdateDownload : Copy.UpdateOpenPage
          });
      if (accepted is null) {
        Status = string.Format(Copy.UpdateAvailable, latest, installed);
        return;
      }

      if (hasAsset) {
        Status = Copy.UpdateDownloading;
        var downloaded = await _updates.DownloadAsync(info.Asset!);
        if (!downloaded.IsSuccess || string.IsNullOrWhiteSpace(downloaded.Value)) {
          Status = string.Join(" ", downloaded.Messages);
          OpenUrl(info.Release.HtmlUrl);
          return;
        }

        if (!_updates.TryLaunch(downloaded.Value))
          OpenUrl(info.Release.HtmlUrl);
        else
          Status = "";
        return;
      }

      OpenUrl(info.Release.HtmlUrl);
    }
    finally {
      _updateBusy = false;
      CheckForUpdatesCommand.NotifyCanExecuteChanged();
    }
  }

  private bool CanCheckForUpdates() =>
    !_updateBusy;

  private static void OpenUrl(string? url) {
    if (string.IsNullOrWhiteSpace(url))
      return;
    try {
      Process.Start(new ProcessStartInfo {
        FileName = url,
        UseShellExecute = true
      });
    }
    catch {
    }
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
    EditorCertifiedKind = ChoiceRow.Certified(EditorCertifiedKind.Id);
    OnPropertyChanged(nameof(SecurityModes));
    OnPropertyChanged(nameof(CertifiedKinds));
    OnPropertyChanged(nameof(SelectedLanguage));
    OnPropertyChanged(nameof(IsEnglish));
    OnPropertyChanged(nameof(IsItalian));
    OnPropertyChanged(nameof(IsFrench));
    OnPropertyChanged(nameof(IsGerman));
    OnPropertyChanged(nameof(IsSpanish));
    OnPropertyChanged(nameof(LayoutToggleLabel));
    NotifyFeatures();
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
    if (ShowPstFields) {
      if (string.IsNullOrWhiteSpace(EditorStorePath) || !File.Exists(EditorStorePath.Trim())) {
        Status = Copy.PstPathRequired;
        return;
      }
    }
    else if (string.IsNullOrWhiteSpace(EditorIncomingHost) || string.IsNullOrWhiteSpace(EditorAddress)) {
      Status = "Address and incoming host are required.";
      return;
    }

    var configuration = _files.Current;
    configuration.EnsureDefaults();
    var box = MailboxToSave();
    box.DisplayName = EditorName.Trim();
    box.Address = EditorAddress.Trim();
    box.Username = EditorUsername.Trim();
    if (ShowPstFields) {
      var path = EditorStorePath.Trim();
      box.Provider = MailProvider.Pst;
      box.IncomingProtocol = MailProtocol.Pst;
      box.StorePath = path;
      box.ImapHost = "";
      box.AuthKind = MailAuthKind.Password;
      box.CertifiedKind = MailCertifiedKind.Ordinary;
      if (string.IsNullOrWhiteSpace(box.Address))
        box.Address = Path.GetFileNameWithoutExtension(path);
      if (string.IsNullOrWhiteSpace(box.DisplayName))
        box.DisplayName = Path.GetFileName(path);
    }
    else {
      box.IncomingProtocol = EditorIncomingProtocol.Id;
      box.IncomingSecurity = EditorIncomingSecurity.Id;
      box.ImapHost = EditorIncomingHost.Trim();
      box.StorePath = "";
      box.ImapPort = EditorIncomingPort <= 0
        ? MailSecurity.DefaultIncomingPort(box.IncomingProtocol, box.IncomingSecurity)
        : (int)EditorIncomingPort;
      box.SmtpSecurity = EditorSmtpSecurity.Id;
      box.SmtpHost = string.IsNullOrWhiteSpace(EditorSmtpHost) ? EditorIncomingHost.Trim() : EditorSmtpHost.Trim();
      box.SmtpPort = EditorSmtpPort <= 0
        ? MailSecurity.DefaultSmtpPort(box.SmtpSecurity)
        : (int)EditorSmtpPort;
      box.Provider = MailProvider.Normalize(EditorProvider.Id);
      box.CertifiedKind = EditorCertifiedKind.Id;
      var nextStoreId = EditorArchiveStore?.Id ?? "";
      if (!box.ArchiveStoreId.Equals(nextStoreId, StringComparison.OrdinalIgnoreCase)) {
        var oldRoot = MailArchiveLayout.MailRoot(box, configuration.Mailboxes);
        box.ArchiveStoreId = nextStoreId;
        var newRoot = MailArchiveLayout.MailRoot(box, configuration.Mailboxes);
        if (!oldRoot.Equals(newRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(oldRoot)) {
          _worker.Call(new WorkerRequest {
            Op = "copy-store",
            Path = oldRoot,
            Dest = newRoot
          });
        }

        _archive.Close(box.Id);
        _archive.Open(box.Id, MailArchiveLayout.DatabasePath(box, configuration.Mailboxes));
      }
      else
        box.ArchiveStoreId = nextStoreId;
      if (_pendingOauth is not null)
        box.AuthKind = MailAuthKind.Normalize(_pendingOauth.Provider);
      else if (MailProvider.UsesOAuth(box.Provider) || _auth.HasTokens(box.Id))
        box.AuthKind = MailAuthKind.Normalize(
          box.Provider == MailProvider.Outlook ? MailAuthKind.Microsoft : MailAuthKind.Google);
      else
        box.AuthKind = MailAuthKind.Password;
      MailProvider.Apply(box);
    }
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
    await RemoveMailboxCoreAsync(SelectedMailbox.Id);
    Status = "Mailbox removed.";
  }

  [RelayCommand(CanExecute = nameof(CanDetachPst))]
  private async Task DetachPstAsync() {
    var box = SelectedFolderNode?.Mailbox;
    if (box is null || !box.IsLocalStore)
      return;
    var label = box.Label;
    await RemoveMailboxCoreAsync(box.Id);
    Status = string.Format(Copy.DetachStoreDone, label);
  }

  private bool CanDetachPst() =>
    ShowDetachPst;

  private async Task RemoveMailboxCoreAsync(string id) {
    var configuration = _files.Current;
    configuration.Mailboxes.RemoveAll(m => m.Id == id);
    if (configuration.SelectedMailboxId == id)
      configuration.SelectedMailboxId = configuration.Mailboxes.FirstOrDefault()?.Id;
    foreach (var rule in configuration.Rules ?? []) {
      if (rule.MailboxId.Equals(id, StringComparison.OrdinalIgnoreCase))
        rule.MailboxId = "";
      if (rule.FolderMailboxId.Equals(id, StringComparison.OrdinalIgnoreCase))
        rule.FolderMailboxId = "";
    }

    _files.Save(configuration);
    _secrets.Delete(FileSecretStore.MailboxKey(id));
    _auth.DeleteTokens(id);
    await DisconnectMailboxAsync(id);
    ReloadMailboxes();
    AccountSaved?.Invoke();
  }

  [RelayCommand(CanExecute = nameof(CanGetMessages))]
  private async Task GetMessagesAsync() {
    if (SelectedMailbox is { IsLocalStore: true })
      return;
    if (ActiveSession is { IsConnected: true })
      await RefreshAsync();
    else
      await ConnectAsync();
    await RunRetentionAsync();
  }

  [RelayCommand(CanExecute = nameof(CanConnect))]
  private async Task ConnectAsync() {
    if (SelectedMailbox is null)
      return;
    var box = SelectedMailbox;
    if (SessionFor(box) is not { IsConnected: true }) {
      var password = PasswordFor(box);
      var oauth = MailAuthKind.IsOAuth(box.AuthKind) || _auth.HasTokens(box.Id);
      if (!box.IsLocalStore && string.IsNullOrWhiteSpace(password) && !oauth) {
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
  private void AttachPst() =>
    AttachPstRequested?.Invoke();

  [RelayCommand]
  private void CreatePst() =>
    CreatePstRequested?.Invoke();

  [RelayCommand]
  private async Task MoveStoreAsync() {
    if (MoveStorePathRequested is null)
      return;
    var path = await MoveStorePathRequested();
    if (!string.IsNullOrWhiteSpace(path))
      await MoveStorePathAsync(path);
  }

  [RelayCommand]
  private void OpenRetention() =>
    RetentionRequested?.Invoke();

  public async Task RunRetentionAsync() {
    var configuration = _files.Current;
    configuration.EnsureDefaults();
    foreach (var job in MailRetention.Jobs(configuration.Retention)) {
      var box = Mailboxes.FirstOrDefault(m => m.Id.Equals(job.MailboxId, StringComparison.OrdinalIgnoreCase));
      if (box is null || box.IsLocalStore)
        continue;
      var known = FoldersFor(box).Select(f => f.FullName).ToList();
      if (known.Count == 0)
        known = _archive.ListFolders(box.Id).ToList();
      var folder = MailRetention.BindFolder(job.Folder, known);
      if (string.IsNullOrWhiteSpace(folder))
        continue;
      var cutoff = DateTimeOffset.UtcNow.AddDays(-job.Days);
      var ids = _archive.UidsOlderThan(box.Id, folder, cutoff);
      if (ids.Count == 0)
        continue;
      var session = SessionFor(box);
      if (session is not { IsConnected: true })
        continue;
      if (MailRetention.IsTrash(folder)) {
        await session.SetMessageFlagsAsync(folder, ids, new MailFlagUpdate { Deleted = true });
        continue;
      }

      if (session is not { SupportsFolders: true })
        continue;
      var trash = MailRetention.ResolveTrash(
        FoldersFor(box).Select(f => (f.Name, f.FullName)).ToList());
      if (folder.Equals(trash, StringComparison.OrdinalIgnoreCase)) {
        await session.SetMessageFlagsAsync(folder, ids, new MailFlagUpdate { Deleted = true });
        continue;
      }

      var moved = await session.MoveMessagesAsync(folder, ids, trash, CancellationToken.None);
      if (moved.IsSuccess)
        DropArchived(box.Id, folder, ids);
    }

    var result = await Task.Run(() => _worker.Call(new WorkerRequest { Op = "retention" }))
      .ConfigureAwait(false);
    await Dispatcher.UIThread.InvokeAsync(() => {
      Status = result.Ok
        ? "Retention processed " + (result.Value ?? "0") + " messages."
        : result.Error ?? "Retention failed.";
    });
    if (SelectedFolder is not null)
      await LoadMessagesAsync(SelectedFolder.FullName, CancellationToken.None);
  }

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
      CancellationToken.None,
      ActiveRules(),
      FoldersForRules(SelectedMailbox.Id));
    Status = "Imported " + count + " .eml into " + folder + ".";
    if (SelectedFolder is not null)
      await LoadMessagesAsync(SelectedFolder.FullName, CancellationToken.None);
  }

  public async Task ImportPstPathAsync(string path, string? storeDirectory = null) {
    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
      return;
    var dest = string.IsNullOrWhiteSpace(storeDirectory)
      ? AppPaths.ProposedStoreDirectory(path)
      : storeDirectory;
    Directory.CreateDirectory(dest);
    var sidecar = LocalStoreSidecar.TryRead(dest);
    var configuration = _files.Current;
    configuration.EnsureDefaults();
    MailboxAccount box;
    if (sidecar is not null) {
      box = configuration.FindMailbox(sidecar.Id)
        ?? new MailboxAccount {
          Id = sidecar.Id,
          DisplayName = sidecar.Name,
          Address = sidecar.Name,
          StorePath = dest,
          IncomingProtocol = MailProtocol.Store,
          Provider = MailProvider.Store
        };
      box.StorePath = dest;
      if (configuration.FindMailbox(box.Id) is null)
        configuration.Mailboxes.Add(box);
    }
    else {
      box = new MailboxAccount {
        DisplayName = Path.GetFileNameWithoutExtension(path),
        Address = Path.GetFileNameWithoutExtension(path),
        StorePath = dest,
        IncomingProtocol = MailProtocol.Store,
        Provider = MailProvider.Store
      };
      LocalStoreSidecar.Write(dest, box.Id, box.Label);
      MailArchiveLayout.EnsureSystemFolders(dest);
      configuration.Mailboxes.Add(box);
    }

    configuration.SelectedMailboxId = box.Id;
    _files.Save(configuration);
    _archive.Open(box.Id, MailArchiveLayout.DatabasePath(box, configuration.Mailboxes));
    ReloadMailboxes();
    SelectedMailbox = Mailboxes.FirstOrDefault(m => m.Id == box.Id);
    Status = "Importing PST…";
    var result = await Task.Run(() => _worker.Call(new WorkerRequest {
      Op = "import-pst",
      MailboxId = box.Id,
      Path = path
    })).ConfigureAwait(false);
    await Dispatcher.UIThread.InvokeAsync(() => {
      Status = result.Ok
        ? "Imported " + result.Value + " messages from PST into " + box.Label + "."
        : result.Error ?? "PST import failed.";
    });
    if (SelectedFolder is not null)
      await LoadMessagesAsync(SelectedFolder.FullName, CancellationToken.None);
  }

  public async Task AttachStorePathAsync(string path) {
    if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) {
      Status = Copy.PstPathRequired;
      return;
    }

    var sidecar = LocalStoreSidecar.TryRead(path);
    if (sidecar is null) {
      Status = "Folder is not a Postclient store (missing postclient.store.json).";
      return;
    }

    var configuration = _files.Current;
    configuration.EnsureDefaults();
    var existing = configuration.FindMailbox(sidecar.Id);
    var box = existing ?? new MailboxAccount {
      Id = sidecar.Id,
      DisplayName = string.IsNullOrWhiteSpace(sidecar.Name) ? Path.GetFileName(path) : sidecar.Name,
      Address = sidecar.Name,
      StorePath = path,
      IncomingProtocol = MailProtocol.Store,
      Provider = MailProvider.Store
    };
    box.StorePath = path;
    box.IncomingProtocol = MailProtocol.Store;
    box.Provider = MailProvider.Store;
    if (existing is null)
      configuration.Mailboxes.Add(box);
    configuration.SelectedMailboxId = box.Id;
    _files.Save(configuration);
    _archive.Open(box.Id, MailArchiveLayout.DatabasePath(box, configuration.Mailboxes));
    ReloadMailboxes();
    SelectedMailbox = Mailboxes.FirstOrDefault(m => m.Id == box.Id);
    Status = string.Format(Copy.AttachStoreDone, box.Label);
    await ConnectAsync();
  }

  public Task AttachPstPathAsync(string path) =>
    AttachStorePathAsync(path);

  public async Task CreateStorePathAsync(string path) {
    if (string.IsNullOrWhiteSpace(path))
      return;
    Directory.CreateDirectory(path);
    var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    if (string.IsNullOrWhiteSpace(name))
      name = "Mail";
    var box = new MailboxAccount {
      DisplayName = name,
      Address = name,
      StorePath = path,
      IncomingProtocol = MailProtocol.Store,
      Provider = MailProvider.Store
    };
    LocalStoreSidecar.Write(path, box.Id, name);
    MailArchiveLayout.EnsureSystemFolders(path);
    var configuration = _files.Current;
    configuration.EnsureDefaults();
    configuration.Mailboxes.Add(box);
    configuration.SelectedMailboxId = box.Id;
    _files.Save(configuration);
    _archive.Open(box.Id, MailArchiveLayout.DatabasePath(box, configuration.Mailboxes));
    ReloadMailboxes();
    SelectedMailbox = Mailboxes.FirstOrDefault(m => m.Id == box.Id);
    Status = string.Format(Copy.CreateStoreDone, box.Label);
    await ConnectAsync();
  }

  public Task CreatePstPathAsync(string path) =>
    CreateStorePathAsync(path);

  public async Task MoveStorePathAsync(string dest) {
    var box = SelectedFolderNode?.Mailbox ?? SelectedMailbox;
    if (box is null || !box.IsLocalStore || string.IsNullOrWhiteSpace(box.StorePath))
      return;
    var source = box.StorePath;
    if (string.IsNullOrWhiteSpace(dest) || dest.Equals(source, StringComparison.OrdinalIgnoreCase))
      return;
    Status = "Moving store…";
    var result = await Task.Run(() => _worker.Call(new WorkerRequest {
      Op = "copy-store",
      Path = source,
      Dest = dest
    })).ConfigureAwait(false);
    if (!result.Ok) {
      Status = result.Error ?? "Move store failed.";
      return;
    }

    try {
      Directory.Delete(source, recursive: true);
    }
    catch {
    }

    box.StorePath = dest;
    _files.Save(_files.Current);
    _archive.Open(box.Id, MailArchiveLayout.DatabasePath(box, _files.Current.Mailboxes));
    ReloadMailboxes();
    Status = string.Format(Copy.MoveStoreDone, box.Label);
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
      CancellationToken.None,
      ActiveRules(),
      FoldersForRules(SelectedMailbox.Id));
    Status = count == 0
      ? "No Thunderbird mbox files found."
      : "Imported " + count + " messages from Thunderbird.";
    if (SelectedFolder is not null)
      await LoadMessagesAsync(SelectedFolder.FullName, CancellationToken.None);
  }

  [RelayCommand(CanExecute = nameof(CanStartCompose))]
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
    if (SelectedMessages.Count > 0 && (SelectedMessage is null || !SelectedMessages.Contains(SelectedMessage)))
      SelectedMessage = SelectedMessages[0];
    NotifyMessageCommands();
  }

  public void RestoreMessageGridSelection() =>
    MessageListSelectionRestoreRequested?.Invoke();

  public void RestoreFolderTreeSelection() =>
    FolderTreeSelectionRestoreRequested?.Invoke();

  public void SetSelectedFolders(IEnumerable<FolderNodeViewModel> nodes, FolderNodeViewModel? primary) {
    SelectedFolderNodes.Clear();
    foreach (var node in nodes)
      SelectedFolderNodes.Add(node);
    if (primary is not null && !SelectedFolderNodes.Contains(primary))
      SelectedFolderNodes.Insert(0, primary);
    if (primary is not null)
      SelectedFolderNode = primary;
    else if (SelectedFolderNodes.Count > 0
        && (SelectedFolderNode is null || !SelectedFolderNodes.Contains(SelectedFolderNode)))
      SelectedFolderNode = SelectedFolderNodes[0];
    NotifyFolderCommands();
    OnPropertyChanged(nameof(EmptyFolderLabel));
  }

  public IReadOnlyList<uint> DragMessageIds(MessageRowViewModel source) {
    if (SelectedMessages.Contains(source))
      return SelectedMessages.Select(row => row.Header.Id).ToList();
    return [source.Header.Id];
  }

  public IReadOnlyList<string> DragFolderNames(FolderNodeViewModel source) {
    var boxId = source.Mailbox?.Id ?? "";
    var names = SelectedFolderNodes
      .Where(node =>
        node.Mailbox?.Id.Equals(boxId, StringComparison.OrdinalIgnoreCase) == true
        && node.Folder is not null
        && MailFolderRole.IsCustom(node.Folder.Name, node.Folder.FullName))
      .Select(node => node.Folder!.FullName)
      .ToList();
    if (source.Folder is { } folder
        && MailFolderRole.IsCustom(folder.Name, folder.FullName)
        && names.All(name => !name.Equals(folder.FullName, StringComparison.OrdinalIgnoreCase)))
      names.Insert(0, folder.FullName);
    if (names.Count == 0)
      return [];
    return MailSelection.Roots(names.Select(name => (boxId, name)))
      .Select(item => item.Folder)
      .ToList();
  }

  [RelayCommand(CanExecute = nameof(CanOrganize))]
  private async Task MoveToFolderAsync(FolderRowViewModel? folder) {
    if (folder is null)
      return;
    await MoveRowsAsync(TargetRows(), folder.FullName);
  }

  public bool CanDropMessagesOn(FolderNodeViewModel? node, string sourceMailboxId, string fromFolder) {
    if (node?.Mailbox is null)
      return false;
    return MailDrop.CanDropMessages(
      node.Mailbox.IncomingProtocol,
      node.Mailbox.Id,
      node.Folder?.FullName,
      sourceMailboxId,
      fromFolder);
  }

  public bool CanDropFolderOn(FolderNodeViewModel? node, string sourceMailboxId, string sourceFolder) {
    if (node?.Mailbox is null || string.IsNullOrWhiteSpace(sourceFolder))
      return false;
    var row = FoldersFor(MailboxById(sourceMailboxId))
      .FirstOrDefault(f => f.FullName.Equals(sourceFolder, StringComparison.OrdinalIgnoreCase));
    var destParent = node.IsAccount || node.Folder is null ? null : node.Folder.FullName;
    return MailDrop.CanDropFolder(
      node.Mailbox.IncomingProtocol,
      node.Mailbox.Id,
      destParent,
      sourceMailboxId,
      sourceFolder,
      MailFolderRole.IsCustom(row?.Name ?? MailFolderPath.Leaf(sourceFolder), sourceFolder));
  }

  public Task DropMessagesAsync(
    FolderNodeViewModel dest,
    string sourceMailboxId,
    string fromFolder,
    IReadOnlyList<uint> ids) {
    if (dest.Mailbox is null)
      return Task.CompletedTask;
    return MoveIdsAsync(
      ids,
      dest.Folder?.FullName ?? "",
      fromFolder,
      destMailboxId: dest.Mailbox.Id,
      sourceMailboxId: sourceMailboxId);
  }

  public Task DropFolderAsync(
    FolderNodeViewModel dest,
    string sourceMailboxId,
    string sourceFolder) =>
    DropFoldersAsync(dest, [(sourceMailboxId, sourceFolder)]);

  public async Task DropFoldersAsync(
    FolderNodeViewModel dest,
    IReadOnlyList<(string MailboxId, string Folder)> sources) {
    if (dest.Mailbox is null)
      return;
    var roots = MailSelection.Roots(
      sources.Where(source => CanDropFolderOn(dest, source.MailboxId, source.Folder)));
    if (roots.Count == 0)
      return;
    if (roots.Count == 1) {
      await MoveDroppedFolderAsync(dest, roots[0].MailboxId, roots[0].Folder);
      return;
    }

    var moved = 0;
    foreach (var source in roots) {
      if (!CanDropFolderOn(dest, source.MailboxId, source.Folder))
        continue;
      await MoveDroppedFolderAsync(dest, source.MailboxId, source.Folder);
      moved++;
    }

    if (moved > 1)
      Status = string.Format(Copy.FoldersMoved, moved);
  }

  private async Task MoveDroppedFolderAsync(
    FolderNodeViewModel dest,
    string sourceMailboxId,
    string sourceFolder) {
    if (dest.Mailbox is null || !CanDropFolderOn(dest, sourceMailboxId, sourceFolder))
      return;
    var sourceBox = MailboxById(sourceMailboxId);
    var destBox = dest.Mailbox;
    if (sourceBox is null)
      return;
    var destParent = dest.IsAccount || dest.Folder is null ? null : dest.Folder.FullName;
    var openFolder = SelectedMailbox?.Id == sourceBox.Id ? SelectedFolder?.FullName : null;
    await RunAsync(Copy.Moving, async token => {
      var sourceSession = await EnsureMailboxReadyAsync(sourceBox, token);
      var destSession = destBox.Id.Equals(sourceBox.Id, StringComparison.OrdinalIgnoreCase)
        ? sourceSession
        : await EnsureMailboxReadyAsync(destBox, token);
      if (sourceSession is not { IsConnected: true, SupportsFolders: true }
          || destSession is not { IsConnected: true, SupportsFolders: true }) {
        Status = Copy.CannotMoveSystemFolder;
        return;
      }

      var leaf = MailFolderPath.UniqueLeaf(
        MailFolderPath.Leaf(sourceFolder),
        SiblingNames(destBox, destParent, sourceFolder));
      if (destBox.Id.Equals(sourceBox.Id, StringComparison.OrdinalIgnoreCase)) {
        var renamed = await sourceSession.RenameFolderAsync(sourceFolder, destParent, leaf, token);
        if (!renamed.IsSuccess) {
          Status = string.Join(" ", renamed.Messages);
          return;
        }

        await LoadFoldersAsync(sourceBox, sourceSession, token);
        var destPath = FindMovedFolder(sourceBox, destParent, leaf) ?? MailFolderPath.Combine(destParent, leaf);
        RememberMovedFolder(sourceBox.Id, sourceFolder, destPath, null);
        await SelectAfterFolderMoveAsync(sourceBox, sourceFolder, destPath, openFolder, token);
        Status = Copy.FolderMoved;
        return;
      }

      var created = await destSession.CreateFolderAsync(leaf, destParent, token);
      if (!created.IsSuccess) {
        Status = string.Join(" ", created.Messages);
        return;
      }

      await LoadFoldersAsync(destBox, destSession, token);
      var destRoot = FindMovedFolder(destBox, destParent, leaf) ?? MailFolderPath.Combine(destParent, leaf);
      var slice = FolderSlice(sourceBox, sourceFolder);
      var failed = 0;
      var copied = 0;
      foreach (var folder in slice) {
        var destPath = MailFolderPath.Rewrite(folder.FullName, sourceFolder, destRoot) ?? destRoot;
        if (!folder.FullName.Equals(sourceFolder, StringComparison.OrdinalIgnoreCase)) {
          var nested = await destSession.CreateFolderAsync(
            MailFolderPath.Leaf(destPath),
            MailFolderPath.Parent(destPath),
            token);
          if (!nested.IsSuccess) {
            failed++;
            continue;
          }
        }

        foreach (var id in await FolderUidsAsync(sourceBox, sourceSession, folder.FullName, token)) {
          if (await CopyUidToMailboxAsync(sourceBox, sourceSession, destBox, folder.FullName, destPath, id, token))
            copied++;
          else
            failed++;
        }
      }

      if (failed > 0) {
        await LoadFoldersAsync(destBox, destSession, token);
        Status = string.Format(Copy.MovedMessages, copied);
        return;
      }

      foreach (var folder in slice.OrderByDescending(f => f.FullName.Length)) {
        var deleted = await sourceSession.DeleteFolderAsync(folder.FullName, token);
        if (deleted.IsSuccess)
          ForgetLocalFolder(sourceBox.Id, folder.FullName);
      }

      RememberMovedFolder(sourceBox.Id, sourceFolder, destRoot, destBox.Id);
      await LoadFoldersAsync(sourceBox, sourceSession, token);
      await LoadFoldersAsync(destBox, destSession, token);
      await SelectAfterFolderMoveAsync(sourceBox, sourceFolder, destRoot, openFolder, token, destBox);
      Status = Copy.FolderMoved;
    });
  }

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
    if (VisibleMessages.Count > 0
        && rows.Count >= VisibleMessages.Count
        && rows.Count >= Messages.Count)
      rows = VisibleMessages.ToList();
    var box = SelectedMailbox;
    var folder = SelectedFolder;
    if (box is null || folder is null)
      return;
    if (!MailRetention.IsTrash(folder.Name, folder.FullName)) {
      var trash = await EnsureTrashFolderAsync(box, CancellationToken.None);
      if (!string.IsNullOrWhiteSpace(trash)
          && !trash.Equals(folder.FullName, StringComparison.OrdinalIgnoreCase)) {
        await MoveRowsAsync(rows, trash);
        return;
      }
    }

    if (!await ApplyFlagsAsync(new MailFlagUpdate { Deleted = true }, null, rows))
      return;
    DropArchived(box.Id, folder.FullName, TargetIds(rows));
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
    var parent = SelectedFolder is { } folder && MailFolderRole.CanHoldFolders(folder.Name, folder.FullName)
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
    var folder = TargetFolderNodes().FirstOrDefault()?.Folder ?? SelectedFolder;
    if (session is not { IsConnected: true, SupportsFolders: true } || box is null || folder is null)
      return;
    var trash = await EnsureTrashFolderAsync(box, CancellationToken.None);
    var purge = string.IsNullOrWhiteSpace(trash)
      || trash.Equals(folder.FullName, StringComparison.OrdinalIgnoreCase);
    var confirm = string.Format(purge ? Copy.EmptyTrashConfirm : Copy.EmptyFolderConfirm, folder.Name);
    if (!await ConfirmAsync(purge ? Copy.DeleteAllItems : Copy.EmptyFolder, confirm))
      return;
    await RunAsync(Copy.EmptyingFolder, async token => {
      var result = await session.EmptyFolderAsync(
        folder.FullName,
        purge ? null : trash,
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
    var targets = TargetCustomFolderRoots();
    if (targets.Count == 0)
      return;
    if (targets.Any(target => !MailFolderRole.IsCustom(target.Folder.Name, target.Folder.FullName))) {
      Status = Copy.CannotDeleteSystemFolder;
      return;
    }

    var confirm = targets.Count == 1
      ? string.Format(Copy.DeleteFolderConfirm, targets[0].Folder.Name)
      : string.Format(Copy.DeleteFoldersConfirm, targets.Count);
    if (!await ConfirmAsync(Copy.DeleteFolder, confirm))
      return;
    var open = SelectedMailbox is { } openBox
      && SelectedFolder is { } current
      && targets.Any(target =>
        target.Box.Id.Equals(openBox.Id, StringComparison.OrdinalIgnoreCase)
        && MailFolderPath.IsSelfOrUnder(current.FullName, target.Folder.FullName));
    await RunAsync(Copy.DeletingFolder, async token => {
      var deleted = 0;
      foreach (var group in targets.GroupBy(target => target.Box.Id, StringComparer.OrdinalIgnoreCase)) {
        var box = group.First().Box;
        var session = await EnsureMailboxReadyAsync(box, token);
        if (session is not { IsConnected: true, SupportsFolders: true }) {
          Status = Copy.CannotDeleteSystemFolder;
          return;
        }

        foreach (var target in group.OrderByDescending(item => item.Folder.FullName.Length)) {
          foreach (var folder in FolderSlice(box, target.Folder.FullName)
            .OrderByDescending(item => item.FullName.Length)) {
            var result = await session.DeleteFolderAsync(folder.FullName, token);
            if (!result.IsSuccess) {
              Status = string.Join(" ", result.Messages);
              return;
            }

            ForgetLocalFolder(box.Id, folder.FullName);
          }

          deleted++;
        }

        await LoadFoldersAsync(box, session, token);
      }

      if (open) {
        Messages.Clear();
        VisibleMessages.Clear();
        SelectedMessage = null;
        ClearReading();
      }

      Status = deleted <= 1 ? Copy.FolderDeleted : string.Format(Copy.FoldersDeleted, deleted);
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

  private void ForgetLocalFolder(string mailboxId, string folder) =>
    DropArchived(mailboxId, folder);

  private void DropArchived(
    string mailboxId,
    string folder,
    IReadOnlyCollection<uint>? uids = null) {
    DeleteEmlFiles(
      uids is null
        ? _archive.RemoveFolder(mailboxId, folder)
        : _archive.RemoveUids(mailboxId, folder, uids));
    if (IsInitialSyncComplete(mailboxId))
      _semantic.Wake();
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
    if (TracksCertifiedReceipts(box)) {
      _receipts.Remember(new SentDispatch {
        MailboxId = box.Id,
        MessageId = messageId,
        Subject = request.Subject ?? "",
        To = request.To.Select(r => r.Address).ToList(),
        SentAt = DateTimeOffset.UtcNow,
        Status = ReceiptStatus.Submitted
      });
      Status = string.Format(Copy.SentFromCertified, box.Label);
      return;
    }

    Status = string.Format(Copy.SentFrom, box.Label);
  }

  private static bool TracksCertifiedReceipts(MailboxAccount? box) =>
    box is not null && box.TracksCertifiedReceipts;

  private bool TracksCertifiedReceipts(string mailboxId) {
    var box = _files.Current.FindMailbox(mailboxId)
      ?? Mailboxes.FirstOrDefault(m => m.Id.Equals(mailboxId, StringComparison.OrdinalIgnoreCase));
    return TracksCertifiedReceipts(box);
  }

  private void DropOrdinaryReceiptWaits() {
    foreach (var box in Mailboxes) {
      if (!TracksCertifiedReceipts(box))
        _receipts.ForgetMailbox(box.Id);
    }
  }

  private bool CanConnect() =>
    !IsBusy && SelectedMailbox is not null;

  private bool CanGetMessages() =>
    !IsBusy && SelectedMailbox is not null && !SelectedMailbox.IsLocalStore;

  private bool CanStartCompose() =>
    !IsBusy && SelectedMailbox is { IsLocalStore: false } && ActiveSession is { IsConnected: true };

  private bool CanReply() =>
    !IsBusy && SelectedMessage is not null
    && (ActiveSession is { IsConnected: true } || SelectedMailbox is { IsLocalStore: true });

  private bool HasMessageSelection() =>
    SelectedMessages.Count > 0 || SelectedMessage is not null;

  private bool CanSessionFolders() =>
    !IsBusy && ActiveSession is { IsConnected: true, SupportsFolders: true };

  private bool CanOrganize() =>
    !IsBusy && HasMessageSelection() && SelectedMailbox is not null
    && (ActiveSession is { IsConnected: true, SupportsFolders: true } || SelectedMailbox.IsLocalStore);

  private bool CanManageFolders() =>
    CanSessionFolders() && SelectedMailbox is not null;

  private bool CanCreateFolder() =>
    CanManageFolders();

  private bool CanEmptyFolder() {
    var folder = TargetFolderNodes().FirstOrDefault()?.Folder ?? SelectedFolder;
    if (folder is null || MailFolderRole.IsNamespace(folder.Name, folder.FullName))
      return false;
    if (SelectedMailbox is { IsLocalStore: true } && !IsBusy)
      return ActiveSession is { IsConnected: true, SupportsFolders: true };
    return CanManageFolders();
  }

  private bool CanDeleteCustomFolder() =>
    !IsBusy && TargetCustomFolderRoots().Count > 0;

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

  partial void OnEditorCertifiedKindChanged(ChoiceRow value) {
    if (_applyingProvider)
      return;
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
    OnPropertyChanged(nameof(LayoutToggleLabel));
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
    OnPropertyChanged(nameof(ShowFatturaPaTools));
  }

  public void NotifyFeatures() {
    FeatureGate.Use(_files.Current.Features);
    OnPropertyChanged(nameof(Providers));
    OnPropertyChanged(nameof(UseFascicolo));
    OnPropertyChanged(nameof(UseFatturaPa));
    OnPropertyChanged(nameof(UseEnvelopeTools));
    OnPropertyChanged(nameof(UseDeliveryColumn));
    OnPropertyChanged(nameof(UseTypeColumn));
    OnPropertyChanged(nameof(ShowFatturaPaTools));
    NotifyBodyKind();
    if (MailBodyKind.IsFattura(ReadingBodyKind) && !UseFatturaPa)
      SetReadingKind(MailBodyKind.Html);
  }

  partial void OnHasReadingFatturaChanged(bool value) =>
    OnPropertyChanged(nameof(ShowFatturaPaTools));

  public void DisableHtmlEngine(string? reason = null) {
    if (!HtmlEngineReady)
      return;
    HtmlEngineReady = false;
    NotifyBodyKind();
    Status = string.IsNullOrWhiteSpace(reason) ? WebViewSetup.MissingEngineHint() : reason;
  }

  partial void OnMessageFilterChanged(string value) {
    if (string.IsNullOrWhiteSpace(value))
      RebuildVisible();
    else
      ScheduleSearch(immediate: false);
  }

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
    if (!_uiReady)
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
    OnPropertyChanged(nameof(ShowFolderContext));
    OnPropertyChanged(nameof(ShowDetachPst));
    DetachPstCommand.NotifyCanExecuteChanged();
  }

  public bool ShowFolderContext =>
    TargetFolderNodes().Count > 0;

  public bool ShowDetachPst =>
    !IsBusy
    && SelectedFolderNode is { IsAccount: true, Mailbox: { } box }
    && box.IsLocalStore;

  partial void OnIsBusyChanged(bool value) {
    OnPropertyChanged(nameof(ShowDetachPst));
    DetachPstCommand.NotifyCanExecuteChanged();
  }

  partial void OnSelectedFolderChanged(FolderRowViewModel? value) {
    RefreshFolderStats();
    NotifyFolderCommands();
    if (value is null || _syncingTree || _suppressFolderLoad)
      return;
    if (MailFolderRole.IsNamespace(value.Name, value.FullName)) {
      _folderLoad?.Cancel();
      Messages.Clear();
      VisibleMessages.Clear();
      SelectedMessages.Clear();
      Status = "";
      return;
    }

    KickFolderLoad(value.FullName);
  }

  partial void OnSelectedMessageChanged(MessageRowViewModel? value) {
    NotifyMessageCommands();
    if (_syncingList || value is null)
      return;
    _ = OpenMessageAsync(value);
  }

  private async Task OpenMessageAsync(MessageRowViewModel row) {
    _readingLoad?.Cancel();
    _readingLoad?.Dispose();
    var cts = new CancellationTokenSource();
    _readingLoad = cts;
    try {
      Status = Copy.OpeningMessage;
      await LoadBodyAsync(row.Header, cts.Token, row.MailboxId);
    }
    catch (OperationCanceledException) {
    }
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
    EditorStorePath = box.DataFile;
    EditorIncomingPort = box.ImapPort;
    EditorSmtpSecurity = ChoiceRow.Security(box.SmtpSecurity, box.SmtpSsl);
    EditorSmtpHost = box.SmtpHost;
    EditorSmtpPort = box.SmtpPort;
    EditorProvider = ChoiceRow.Provider(box.IsLocalStore ? MailProvider.Store : box.Provider);
    EditorCertifiedKind = ChoiceRow.Certified(
      MailCertifiedKind.ForProvider(box.Provider, box.CertifiedKind));
    RebuildArchiveStoreChoices();
    EditorArchiveStore = ArchiveStoreChoices.FirstOrDefault(c =>
      c.Id.Equals(box.ArchiveStoreId, StringComparison.OrdinalIgnoreCase))
      ?? ArchiveStoreChoices[0];
    _applyingProvider = false;
    UpdateAuthUi();
  }

  private async Task SignInProviderAsync(string kind) {
    IsBusy = true;
    SignInGoogleCommand.NotifyCanExecuteChanged();
    SignInMicrosoftCommand.NotifyCanExecuteChanged();
    Status = "Waiting for Identity Hub…";
    IdentityHubSignInRequested?.Invoke(kind);
    await Task.CompletedTask;
  }

  public async Task CompleteIdentityHubSignIn(string kind, string json) {
    IsBusy = false;
    SignInGoogleCommand.NotifyCanExecuteChanged();
    SignInMicrosoftCommand.NotifyCanExecuteChanged();
    var result = await _auth.CompleteHubSignInAsync(json, kind);
    CompleteSignIn(kind, result);
  }

  public void CancelIdentityHubSignIn(string? message) {
    IsBusy = false;
    SignInGoogleCommand.NotifyCanExecuteChanged();
    SignInMicrosoftCommand.NotifyCanExecuteChanged();
    if (!string.IsNullOrWhiteSpace(message))
      Status = message;
  }

  public string HubLoginUrl(string kind) =>
    _auth.DesktopLoginUrl(kind);

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
    if (MailProvider.IsPst(EditorProvider.Id)) {
      EditorCertifiedKind = ChoiceRow.Certified(MailCertifiedKind.Ordinary);
      NotifyStoreUi();
      UpdateAuthUi();
      return;
    }

    var box = new MailboxAccount {
      Provider = EditorProvider.Id,
      IncomingProtocol = EditorIncomingProtocol.Id,
      CertifiedKind = EditorCertifiedKind.Id
    };
    MailProvider.Apply(box);
    EditorIncomingHost = box.ImapHost;
    EditorIncomingPort = box.ImapPort;
    EditorIncomingSecurity = ChoiceRow.Security(box.IncomingSecurity);
    EditorSmtpHost = box.SmtpHost;
    EditorSmtpPort = box.SmtpPort;
    EditorSmtpSecurity = ChoiceRow.Security(box.SmtpSecurity);
    EditorCertifiedKind = ChoiceRow.Certified(box.CertifiedKind);
    NotifyStoreUi();
  }

  private void UpdateAuthUi() {
    var provider = MailProvider.Normalize(EditorProvider.Id);
    ShowOAuthButtons = MailProvider.UsesOAuth(provider);
    ShowGoogleSignIn = provider == MailProvider.Gmail;
    ShowMicrosoftSignIn = provider == MailProvider.Outlook;
    var oauth = _pendingOauth is not null
      || (SelectedMailbox is not null
          && (MailAuthKind.IsOAuth(SelectedMailbox.AuthKind) || _auth.HasTokens(SelectedMailbox.Id)));
    ShowPasswordFields = ShowServerFields && (provider != MailProvider.Outlook || !oauth);
    OauthHint = provider == MailProvider.Gmail || provider == MailProvider.Outlook
      ? Copy.IdentityHubHint
      : MailCertifiedKind.TracksReceipts(EditorCertifiedKind.Id) || MailProvider.IsPec(provider)
          ? Copy.PecHint
          : MailProvider.IsLocalBucket(provider)
            ? Copy.IdentityHubHint
            : "";
    NotifyStoreUi();
  }

  private void NotifyStoreUi() {
    OnPropertyChanged(nameof(ShowPstFields));
    OnPropertyChanged(nameof(ShowServerFields));
    OnPropertyChanged(nameof(ShowArchiveStore));
    OnPropertyChanged(nameof(ShowCertifiedKind));
  }

  [RelayCommand]
  private async Task BrowseStorePathAsync() {
    var pick = PickStorePathRequested;
    if (pick is null)
      return;
    var path = await pick();
    if (!string.IsNullOrWhiteSpace(path))
      EditorStorePath = path;
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

  private void PersistPstStorePath(MailboxAccount box, string path) {
    if (string.IsNullOrWhiteSpace(path) || box.StorePath.Equals(path, StringComparison.OrdinalIgnoreCase))
      return;
    box.StorePath = path;
    var configuration = _files.Current;
    var stored = configuration.FindMailbox(box.Id);
    if (stored is not null)
      stored.StorePath = path;
    configuration.EnsureDefaults();
    _files.Save(configuration);
    var notice = (SessionFor(box) as PstMailSession)?.LastNotice;
    Dispatcher.UIThread.Post(() => {
      if (SelectedMailbox?.Id == box.Id)
        EditorStorePath = path;
      if (!string.IsNullOrWhiteSpace(notice))
        Status = notice;
    });
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
    DropOrdinaryReceiptWaits();
    var selected = _files.Current.FindMailbox(_files.Current.SelectedMailboxId);
    SelectedMailbox = selected is null
      ? Mailboxes.FirstOrDefault()
      : Mailboxes.FirstOrDefault(m => m.Id == selected.Id);
    RebuildArchiveStoreChoices();
    RebuildFolderTree();
  }

  private void RebuildArchiveStoreChoices() {
    var keep = EditorArchiveStore?.Id ?? "";
    ArchiveStoreChoices.Clear();
    ArchiveStoreChoices.Add(new ChoiceRow { Id = "", Title = Copy.LocalStoreAppData });
    foreach (var box in Mailboxes.Where(m => m.IsLocalStore))
      ArchiveStoreChoices.Add(new ChoiceRow { Id = box.Id, Title = box.Label });
    EditorArchiveStore = ArchiveStoreChoices.FirstOrDefault(c =>
      c.Id.Equals(keep, StringComparison.OrdinalIgnoreCase))
      ?? ArchiveStoreChoices[0];
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
        node = FolderNodeViewModel.Account(box, SavedFolderExpanded(box, null));
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

    var selected = SnapshotFolderSelection();
    var selectedBoxId = SelectedFolderNode?.Mailbox?.Id ?? SelectedMailbox?.Id;
    var selectedFolder = SelectedFolderNode?.Folder?.FullName ?? SelectedFolder?.FullName;
    SelectedFolderNode = FindTreeNode(selectedBoxId, selectedFolder)
      ?? FolderNodeFor(SelectedMailbox, "inbox")
      ?? FolderTree.FirstOrDefault(n => n.Mailbox?.Id == SelectedMailbox?.Id)?.Children.FirstOrDefault()
      ?? FolderTree.FirstOrDefault(n => n.Mailbox?.Id == SelectedMailbox?.Id);
    RemapFolderSelection(selected);
    _syncingTree = false;
    RestoreFolderTreeSelection();
  }

  private List<(string MailboxId, string? Folder)> SnapshotFolderSelection() {
    var nodes = SelectedFolderNodes.Count > 0
      ? SelectedFolderNodes
      : SelectedFolderNode is { } one ? [one] : [];
    var keys = new List<(string MailboxId, string? Folder)>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var node in nodes) {
      var boxId = node.Mailbox?.Id;
      if (string.IsNullOrWhiteSpace(boxId))
        continue;
      var folder = node.Folder?.FullName;
      var key = boxId + "\n" + (folder ?? "");
      if (!seen.Add(key))
        continue;
      keys.Add((boxId, folder));
    }

    return keys;
  }

  private void RemapFolderSelection(IReadOnlyList<(string MailboxId, string? Folder)> keys) {
    SelectedFolderNodes.Clear();
    foreach (var (mailboxId, folder) in keys) {
      var node = FindTreeNode(mailboxId, folder);
      if (node is not null && !SelectedFolderNodes.Contains(node))
        SelectedFolderNodes.Add(node);
    }

    if (SelectedFolderNode is not null && !SelectedFolderNodes.Contains(SelectedFolderNode))
      SelectedFolderNodes.Insert(0, SelectedFolderNode);
    else if (SelectedFolderNode is null && SelectedFolderNodes.Count > 0)
      SelectedFolderNode = SelectedFolderNodes[0];
  }

  private void SyncFolderChildren(FolderNodeViewModel node, MailboxAccount box) {
    var previous = SnapshotFolderExpand(node.Children);
    node.Children.Clear();
    var rows = FoldersFor(box);
    var known = rows.Select(r => r.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var map = new Dictionary<string, FolderNodeViewModel>(StringComparer.OrdinalIgnoreCase);
    foreach (var folder in rows) {
      var expanded = previous.TryGetValue(folder.FullName, out var keep)
        ? keep
        : SavedFolderExpanded(box, folder.FullName);
      map[folder.FullName] = FolderNodeViewModel.ForFolder(box, folder, expanded);
    }
    foreach (var folder in rows
      .OrderBy(f => MailFolderRole.SortKey(f.Name, f.FullName))
      .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)) {
      var child = map[folder.FullName];
      var parentPath = MailFolderRole.DisplayParent(
        folder.Name,
        folder.FullName,
        known,
        MailFolderLayout.For(box),
        folder.Delimiter);
      if (parentPath is not null && map.TryGetValue(parentPath, out var parent))
        parent.Children.Add(child);
      else
        node.Children.Add(child);
    }

    node.NotifyCounts();
  }

  private bool SavedFolderExpanded(MailboxAccount box, string? folder) =>
    _files.Current.Layout.IsFolderExpanded(box.Id, folder);

  private static Dictionary<string, bool> SnapshotFolderExpand(IEnumerable<FolderNodeViewModel> nodes) {
    var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    Walk(nodes);
    return map;

    void Walk(IEnumerable<FolderNodeViewModel> list) {
      foreach (var node in list) {
        if (node.Folder?.FullName is { Length: > 0 } name)
          map[name] = node.IsExpanded;
        Walk(node.Children);
      }
    }
  }

  private FolderNodeViewModel? FindTreeNode(string? mailboxId, string? folder) {
    if (string.IsNullOrWhiteSpace(mailboxId))
      return null;
    var account = FolderTree.FirstOrDefault(n => n.Mailbox?.Id == mailboxId);
    if (account is null)
      return null;
    if (string.IsNullOrWhiteSpace(folder))
      return account.Children.FirstOrDefault() ?? account;
    return FindFolderNode(account, folder)
      ?? account.Children.FirstOrDefault()
      ?? account;
  }

  private static FolderNodeViewModel? FindFolderNode(FolderNodeViewModel node, string folder) {
    if (node.Folder?.FullName.Equals(folder, StringComparison.OrdinalIgnoreCase) == true)
      return node;
    foreach (var child in node.Children) {
      var hit = FindFolderNode(child, folder);
      if (hit is not null)
        return hit;
    }

    return null;
  }

  private FolderNodeViewModel? FolderNodeFor(MailboxAccount? box, string kind) {
    if (box is null)
      return null;
    var account = FolderTree.FirstOrDefault(n => n.Mailbox?.Id == box.Id);
    if (account is null)
      return null;
    return FindKindNode(account, kind);
  }

  private static FolderNodeViewModel? FindKindNode(FolderNodeViewModel node, string kind) {
    if (node.Folder is { } folder && MailFolderRole.Kind(folder.Name, folder.FullName) == kind)
      return node;
    foreach (var child in node.Children) {
      var hit = FindKindNode(child, kind);
      if (hit is not null)
        return hit;
    }

    return null;
  }

  private async Task LoadFoldersAsync(MailboxAccount box, IMailSession session, CancellationToken token) {
    PauseIndexing();
    try {
      var result = await session.ListFoldersAsync(token).ConfigureAwait(false);
      if (!result.IsSuccess) {
        await UiAsync(() => Status = string.Join(" ", result.Messages)).ConfigureAwait(false);
        return;
      }

      var rows = (result.Value ?? [])
        .Select(folder => new FolderRowViewModel {
          FullName = folder.FullName,
          Name = folder.Name,
          Unread = folder.Unread,
          Total = folder.Total,
          Delimiter = folder.Delimiter
        })
        .ToList();
      var loadFolder = "";
      await UiAsync(() => {
        _foldersByMailbox[box.Id] = rows;
        RebuildFolderTree();
        if (SelectedMailbox?.Id != box.Id)
          return;
        SyncFoldersCollection(box);
        var wanted = SelectedFolderNode?.Mailbox?.Id == box.Id ? SelectedFolderNode.Folder?.FullName : null;
        _suppressFolderLoad = true;
        try {
          SelectedFolder = rows.FirstOrDefault(f => f.FullName.Equals(wanted, StringComparison.OrdinalIgnoreCase))
            ?? InboxOf(box)
            ?? rows.FirstOrDefault();
        }
        finally {
          _suppressFolderLoad = false;
        }

        if (SelectedFolder is not null)
          loadFolder = SelectedFolder.FullName;
      }).ConfigureAwait(false);

      if (!string.IsNullOrWhiteSpace(loadFolder))
        KickFolderLoad(loadFolder);

      var quota = await session.GetQuotaAsync(token).ConfigureAwait(false);
      if (quota.IsSuccess && quota.Value is { } info) {
        await UiAsync(() => {
          var node = FolderTree.FirstOrDefault(n => n.Mailbox?.Id == box.Id);
          if (node is null)
            return;
          node.Quota = info;
          node.NotifyQuota();
        }).ConfigureAwait(false);
      }
    }
    finally {
      ResumeIndexing();
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

  private FolderRowViewModel? TrashFolderOf(MailboxAccount? box) {
    if (box is null)
      return null;
    return FoldersFor(box)
      .Concat(Folders)
      .FirstOrDefault(f => MailRetention.IsTrash(f.Name, f.FullName));
  }

  private async Task<string?> EnsureTrashFolderAsync(MailboxAccount box, CancellationToken token) {
    var existing = TrashFolderOf(box);
    if (existing is not null)
      return existing.FullName;
    var session = SessionFor(box);
    if (session is not { IsConnected: true, SupportsFolders: true })
      return null;
    var created = await session.CreateFolderAsync(MailRetention.TrashFolder, null, token);
    if (!created.IsSuccess)
      created = await session.CreateFolderAsync("Trash", null, token);
    if (created.IsSuccess)
      await LoadFoldersAsync(box, session, token);
    return TrashFolderOf(box)?.FullName
      ?? (created.IsSuccess ? MailRetention.TrashFolder : null);
  }

  private IReadOnlyList<MailRule> ActiveRules() =>
    _files.Current.Rules ?? [];

  public IReadOnlyList<(string Name, string FullName)> FoldersForRules(string mailboxId) {
    if (string.IsNullOrWhiteSpace(mailboxId))
      return [];
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var rows = new List<(string Name, string FullName)>();
    void Add(string name, string full) {
      if (string.IsNullOrWhiteSpace(full) || !seen.Add(full.Trim()))
        return;
      rows.Add((name.Trim(), full.Trim()));
    }

    var box = Mailboxes.FirstOrDefault(m => m.Id.Equals(mailboxId, StringComparison.OrdinalIgnoreCase));
    foreach (var folder in FoldersFor(box))
      Add(folder.Name, folder.FullName);

    return rows;
  }

  public async Task EnsureFoldersForRulesAsync(string mailboxId) {
    var box = Mailboxes.FirstOrDefault(m => m.Id.Equals(mailboxId, StringComparison.OrdinalIgnoreCase));
    if (box is null)
      return;
    await ConnectMailboxCoreAsync(box, PasswordFor(box), CancellationToken.None);
    if (FoldersFor(box).Count > 0)
      return;
    if (SessionFor(box) is { IsConnected: true } live)
      await LoadFoldersAsync(box, live, CancellationToken.None);
  }

  public async Task<string> RunAllRulesAsync() {
    var rules = MailRuleEngine.Ready(ActiveRules()).ToList();
    if (rules.Count == 0)
      return Copy.RulesNone;
    var matched = 0;
    foreach (var box in Mailboxes.ToList()) {
      if (!rules.Any(r => MailRuleEngine.TargetsMailbox(r, box.Id)))
        continue;
      await EnsureFoldersForRulesAsync(box.Id);
      foreach (var rule in rules.Where(r => MailRuleEngine.TargetsMailbox(r, box.Id)))
        await EnsureFoldersForRulesAsync(MailRuleEngine.FolderMailbox(rule));
      var session = SessionFor(box);
      var folders = FoldersForRules(box.Id);
      var names = folders.Select(f => f.FullName).ToList();
      foreach (var extra in _archive.ListFolders(box.Id)) {
        if (!names.Any(n => n.Equals(extra, StringComparison.OrdinalIgnoreCase)))
          names.Add(extra);
      }

      foreach (var folder in names) {
        var headers = _archive.ListFolder(box.Id, folder).Select(MailArchiveMap.ToHeader).ToList();
        if (headers.Count == 0)
          continue;
        matched += await ApplyRulesToFolderAsync(box, session, folder, headers, folders, CancellationToken.None);
      }
    }

    if (SelectedMailbox is not null && SelectedFolder is not null)
      ShowArchive(SelectedMailbox.Id, SelectedFolder.FullName);
    var line = string.Format(Copy.RulesRan, matched);
    Status = line;
    return line;
  }

  private async Task ApplyIncomingRulesAsync(
    string folder,
    IReadOnlyList<MailMessageHeader> headers,
    CancellationToken token) {
    var box = SelectedMailbox;
    if (box is null)
      return;
    foreach (var rule in MailRuleEngine.Ready(ActiveRules()).Where(r => MailRuleEngine.TargetsMailbox(r, box.Id)))
      await EnsureFoldersForRulesAsync(MailRuleEngine.FolderMailbox(rule));
    await ApplyRulesToFolderAsync(box, ActiveSession, folder, headers, FoldersForRules(box.Id), token);
  }

  private async Task<int> ApplyRulesToFolderAsync(
    MailboxAccount box,
    IMailSession? session,
    string folder,
    IReadOnlyList<MailMessageHeader> headers,
    IReadOnlyList<(string Name, string FullName)> folders,
    CancellationToken token) {
    var rules = MailRuleEngine.Ready(ActiveRules()).ToList();
    if (rules.Count == 0 || headers.Count == 0)
      return 0;
    var moves = new Dictionary<string, List<uint>>(StringComparer.OrdinalIgnoreCase);
    var copies = new Dictionary<(string MailboxId, string Folder), List<uint>>();
    var read = new List<uint>();
    var flagged = new List<uint>();
    var touched = new HashSet<uint>();
    foreach (var header in headers) {
      foreach (var rule in rules) {
        var destFolders = DestFoldersFor(rule, box.Id, folders);
        if (!MailRuleEngine.CanApply(rule, box.Id, destFolders))
          continue;
        if (!MailRuleEngine.Matches(rule, header.From, "", header.Subject, "", header.HasAttachments))
          continue;
        touched.Add(header.Id);
        if (rule.Action == MailRuleAction.Delete) {
          var trash = MailRetention.ResolveTrash(folders);
          if (!string.IsNullOrWhiteSpace(trash))
            AddUid(moves, trash, header.Id);
        }
        else if (rule.Action == MailRuleAction.Move) {
          var dest = MailRuleEngine.ExactFolder(rule.Folder, destFolders);
          var destBox = MailRuleEngine.FolderMailbox(rule);
          if (!string.IsNullOrWhiteSpace(dest)) {
            if (destBox.Equals(box.Id, StringComparison.OrdinalIgnoreCase)) {
              if (!dest.Equals(folder, StringComparison.OrdinalIgnoreCase))
                AddUid(moves, dest, header.Id);
            }
            else
              AddCopy(copies, destBox, dest, header.Id);
          }
        }

        if (rule.Action == MailRuleAction.MarkRead)
          read.Add(header.Id);
        if (rule.Action == MailRuleAction.Flag)
          flagged.Add(header.Id);
        if (rule.Action == MailRuleAction.Label && !string.IsNullOrWhiteSpace(rule.Label))
          _archive.AddLabel(box.Id, folder, header.Id, rule.Label);
        if (rule.Stop)
          break;
      }
    }

    var movedIds = moves.SelectMany(p => p.Value).ToHashSet();
    foreach (var pair in copies)
      foreach (var id in pair.Value)
        movedIds.Add(id);
    read.RemoveAll(movedIds.Contains);
    flagged.RemoveAll(movedIds.Contains);
    if (session is { IsConnected: true, SupportsFolders: true }) {
      foreach (var pair in moves) {
        var moved = await session.MoveMessagesAsync(folder, pair.Value, pair.Key, token);
        if (moved.IsSuccess)
          DropArchived(box.Id, folder, pair.Value);
      }

      if (read.Count > 0) {
        await session.SetMessageFlagsAsync(folder, read, new MailFlagUpdate { Seen = true }, token);
        _archive.UpdateFlags(
          box.Id,
          folder,
          headers.Where(h => read.Contains(h.Id))
            .Select(h => (h.Id, true, h.IsFlagged || flagged.Contains(h.Id))));
      }

      if (flagged.Count > 0) {
        await session.SetMessageFlagsAsync(folder, flagged, new MailFlagUpdate { Flagged = true }, token);
        _archive.UpdateFlags(
          box.Id,
          folder,
          headers.Where(h => flagged.Contains(h.Id))
            .Select(h => (h.Id, h.IsSeen || read.Contains(h.Id), true)));
      }
    }

    if (copies.Count > 0)
      await CopyRuleMovesAsync(box, session, folder, folders, copies, token);

    return touched.Count;
  }

  private IReadOnlyList<(string Name, string FullName)> DestFoldersFor(
    MailRule rule,
    string sourceId,
    IReadOnlyList<(string Name, string FullName)> sourceFolders) {
    var destId = MailRuleEngine.FolderMailbox(rule);
    if (string.IsNullOrWhiteSpace(destId) || destId.Equals(sourceId, StringComparison.OrdinalIgnoreCase))
      return sourceFolders;
    return FoldersForRules(destId);
  }

  private static void AddCopy(
    Dictionary<(string MailboxId, string Folder), List<uint>> map,
    string mailboxId,
    string folder,
    uint id) {
    var key = (mailboxId, folder);
    if (!map.TryGetValue(key, out var ids)) {
      ids = [];
      map[key] = ids;
    }

    if (!ids.Contains(id))
      ids.Add(id);
  }

  private async Task CopyRuleMovesAsync(
    MailboxAccount source,
    IMailSession? sourceSession,
    string sourceFolder,
    IReadOnlyList<(string Name, string FullName)> sourceFolders,
    Dictionary<(string MailboxId, string Folder), List<uint>> copies,
    CancellationToken token) {
    var copied = new List<uint>();
    foreach (var pair in copies) {
      var destBox = Mailboxes.FirstOrDefault(m => m.Id.Equals(pair.Key.MailboxId, StringComparison.OrdinalIgnoreCase));
      if (destBox is null)
        continue;
      await EnsureFoldersForRulesAsync(destBox.Id);
      foreach (var id in pair.Value) {
        if (await CopyUidToMailboxAsync(source, sourceSession, destBox, sourceFolder, pair.Key.Folder, id, token))
          copied.Add(id);
      }
    }

    if (copied.Count == 0)
      return;
    var unique = copied.Distinct().ToList();
    if (sourceSession is { IsConnected: true, SupportsFolders: true }) {
      var trash = MailRetention.ResolveTrash(sourceFolders);
      if (!string.IsNullOrWhiteSpace(trash) && !trash.Equals(sourceFolder, StringComparison.OrdinalIgnoreCase))
        await sourceSession.MoveMessagesAsync(sourceFolder, unique, trash, token);
      else
        await sourceSession.SetMessageFlagsAsync(sourceFolder, unique, new MailFlagUpdate { Deleted = true }, token);
    }

    DropArchived(source.Id, sourceFolder, unique);
  }

  private async Task<bool> CopyUidToMailboxAsync(
    MailboxAccount source,
    IMailSession? sourceSession,
    MailboxAccount dest,
    string sourceFolder,
    string destFolder,
    uint id,
    CancellationToken token) {
    if (SessionFor(dest) is not { IsConnected: true })
      await ConnectMailboxCoreAsync(dest, PasswordFor(dest), token).ConfigureAwait(false);
    var destSession = SessionFor(dest);
    if (destSession is not { IsConnected: true })
      return false;
    byte[]? eml = null;
    var path = _archive.EmlPath(source.Id, sourceFolder, id)
      ?? MailArchiveLayout.EmlPath(source, Mailboxes, sourceFolder, id);
    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
      eml = await File.ReadAllBytesAsync(path, token);
    else if (sourceSession is { IsConnected: true }) {
      var body = await sourceSession.GetMessageAsync(sourceFolder, id, token);
      if (body.IsSuccess && body.Value?.RawEml is { Length: > 0 } raw)
        eml = raw;
    }

    if (eml is null || eml.Length == 0)
      return false;
    var appended = await destSession.AppendAsync(destFolder, eml, token);
    if (!appended.IsSuccess)
      return false;
    var destUid = appended.Value ?? 0;
    var destPath = destSession is IFileMailStore files
      ? files.MessagePath(destFolder, destUid)
      : MailArchiveLayout.EmlPath(dest, Mailboxes, destFolder, destUid);
    if (destUid > 0 && !string.IsNullOrWhiteSpace(destPath)) {
      Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
      if (!File.Exists(destPath))
        await File.WriteAllBytesAsync(destPath, eml, token);
      try {
        _archive.Open(dest.Id, MailArchiveLayout.DatabasePath(dest, Mailboxes));
        _archive.CopyIndexed(source.Id, sourceFolder, id, dest.Id, destFolder, destPath, destUid);
      }
      catch {
      }
    }

    return true;
  }

  private static void AddUid(Dictionary<string, List<uint>> map, string folder, uint id) {
    if (!map.TryGetValue(folder, out var ids)) {
      ids = [];
      map[folder] = ids;
    }

    if (!ids.Contains(id))
      ids.Add(id);
  }

  private FolderRowViewModel? InboxOf(MailboxAccount box) =>
    FoldersFor(box).FirstOrDefault(f => MailFolderRole.Kind(f.Name, f.FullName) == "inbox")
    ?? FoldersFor(box).FirstOrDefault();

  private IMailSession? ActiveSession =>
    SessionFor(SelectedMailbox);

  private IMailSession? SessionFor(MailboxAccount? box) {
    if (box is null)
      return null;
    lock (_connections)
      return _connections.TryGetValue(box.Id, out var session) ? session : null;
  }

  private string PasswordFor(MailboxAccount box) {
    if (SelectedMailbox?.Id == box.Id && !string.IsNullOrWhiteSpace(EditorPassword))
      return EditorPassword;
    var stored = _secrets.Get(FileSecretStore.MailboxKey(box.Id));
    return stored.Value ?? "";
  }

  private async Task<bool> ConnectMailboxCoreAsync(MailboxAccount box, string password, CancellationToken token) {
    while (true) {
      token.ThrowIfCancellationRequested();
      lock (_connections) {
        if (_connecting.Add(box.Id))
          break;
      }

      await Task.Delay(50, token).ConfigureAwait(false);
    }

    PauseIndexing();
    try {
      if (SessionFor(box) is { IsConnected: true } live) {
        var missingFolders = false;
        await UiAsync(() => missingFolders = !_foldersByMailbox.ContainsKey(box.Id)).ConfigureAwait(false);
        if (missingFolders)
          await LoadFoldersAsync(box, live, token).ConfigureAwait(false);
        return true;
      }

      var session = _sessions.Create(box);
      var connected = await session.ConnectAsync(box, password, token).ConfigureAwait(false);
      if (!connected.IsSuccess) {
        await session.DisposeAsync().ConfigureAwait(false);
        await UiAsync(() => {
          if (SelectedMailbox?.Id == box.Id)
            Status = string.Join(" ", connected.Messages);
        }).ConfigureAwait(false);
        return false;
      }

      IMailSession? previous;
      lock (_connections) {
        _connections.TryGetValue(box.Id, out previous);
        _connections[box.Id] = session;
      }

      if (previous is not null)
        await previous.DisposeAsync().ConfigureAwait(false);
      if (session is PstMailSession pst)
        pst.StorePathChanged += path => PersistPstStorePath(box, path);
      await LoadFoldersAsync(box, session, token).ConfigureAwait(false);
      await UiAsync(() => {
        EnsureIndexing(box.Id);
        NotifyFolderCommands();
      }).ConfigureAwait(false);
      return true;
    }
    finally {
      ResumeIndexing();
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
      if (!box.IsLocalStore && string.IsNullOrWhiteSpace(password) && !oauth)
        continue;
      try {
        await ConnectMailboxCoreAsync(box, password, CancellationToken.None).ConfigureAwait(false);
      }
      catch {
      }
    }
  }

  private void ApplyFolderSync(string mailboxId, string folder, MailFolderSync sync) {
    var ready = IsInitialSyncComplete(mailboxId);
    _archive.SetKeywordIndexEnabled(mailboxId, ready);
    if (sync.Flags.Count > 0)
      _archive.UpdateFlags(mailboxId, folder, sync.Flags.Select(f => (f.Id, f.IsSeen, f.IsFlagged)));
    _archive.UpsertHeaders(mailboxId, sync.Headers.Select(MailArchiveMap.FromHeader));
    if (sync.Present is not null) {
      var keep = sync.Present.ToHashSet();
      var gone = _archive.Uids(mailboxId, folder).Where(uid => !keep.Contains(uid)).ToList();
      if (gone.Count > 0)
        DropArchived(mailboxId, folder, gone);
    }

    if (ready)
      _semantic.Wake();
  }

  private async Task LoadMessagesAsync(string folder, CancellationToken token, bool resumeBodies = false) {
    var box = SelectedMailbox;
    if (box is null)
      return;
    await ShowArchiveAsync(box.Id, folder, token).ConfigureAwait(false);
    var session = SessionFor(box);
    if (session is not { IsConnected: true }) {
      await UiAsync(() => Status = ArchiveFiles.Hint()).ConfigureAwait(false);
      return;
    }

    PauseIndexing();
    try {
      var painted = false;
      while (true) {
        var known = await Task.Run(() => _archive.Uids(box.Id, folder), token).ConfigureAwait(false);
        var result = await session.ListMessagesAsync(folder, known, token, IndexUiChunk).ConfigureAwait(false);
        if (!result.IsSuccess) {
          await UiAsync(() =>
            Status = string.Join(" ", result.Messages) + " Showing local archive. " + ArchiveFiles.Hint())
            .ConfigureAwait(false);
          return;
        }

        token.ThrowIfCancellationRequested();
        var sync = result.Value ?? new MailFolderSync();
        ApplyFolderSync(box.Id, folder, sync);
        var fresh = sync.Headers.Where(h => !known.Contains(h.Id)).ToList();
        if (known.Count > 0) {
          await UiAsync(() => {
            if (SelectedMailbox?.Id == box.Id)
              NoticeNewMail(fresh);
          }).ConfigureAwait(false);
        }
        await ApplyIncomingRulesAsync(folder, fresh, token).ConfigureAwait(false);
        await ShowArchiveAsync(box.Id, folder, token).ConfigureAwait(false);
        if (!painted) {
          painted = true;
          await UiAsync(() => {
            IsBusy = false;
            NotifyFolderCommands();
          }).ConfigureAwait(false);
        }

        if (!sync.Incomplete)
          break;
      }

      await UiAsync(() => MarkFolderCataloged(box, folder, session)).ConfigureAwait(false);
      if (TracksCertifiedReceipts(box) && MailFolderRole.Kind(null, folder) == "sent") {
        await IngestReceiptFoldersAsync(token).ConfigureAwait(false);
        await UiAsync(OverlaySentMarks).ConfigureAwait(false);
      }

      await UiAsync(() => Status = ArchiveFiles.Hint()).ConfigureAwait(false);
      if (resumeBodies && IsInitialSyncComplete(box.Id))
        await UiAsync(() => EnsureBodyBackfill(box.Id)).ConfigureAwait(false);
    }
    finally {
      ResumeIndexing();
    }
  }

  private async Task IngestReceiptFoldersAsync(CancellationToken token) {
    var session = ActiveSession;
    if (session is null || SelectedMailbox is null || !TracksCertifiedReceipts(SelectedMailbox))
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
    if (SelectedMailbox is null || !TracksCertifiedReceipts(SelectedMailbox))
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
    if (TracksCertifiedReceipts(SelectedMailbox)
        && header.EnvelopeKind is EnvelopeKind.PecReceipt or EnvelopeKind.EidasRem)
      _receipts.ApplyEvidence(
        SelectedMailbox.Id,
        header.InReplyTo,
        null,
        header.Subject,
        header.EnvelopeTipo);
    if (TracksCertifiedReceipts(SelectedMailbox) && MailFolderRole.Kind(null, folder) == "sent")
      header.DeliveryStatus = _receipts.StatusFor(SelectedMailbox.Id, header.MessageId, header.Subject);
    else if (string.IsNullOrWhiteSpace(header.DeliveryStatus))
      header.DeliveryStatus = ReceiptStatus.FromTipo(header.EnvelopeTipo);
  }

  private void RebuildVisible() {
    if (SelectedMailbox is null || SelectedFolder is null) {
      VisibleMessages.Clear();
      SelectedMessages.Clear();
      SelectedMessage = null;
      NotifyMessageCommands();
      RestoreMessageGridSelection();
      RefreshFolderStats();
      return;
    }

    if (MessageFilter.Trim().Length == 0)
      ApplyVisibleRows([.. Messages]);
    else
      ScheduleSearch(immediate: true);
  }

  private void ScheduleSearch(bool immediate) {
    _search?.Cancel();
    _search?.Dispose();
    var cts = new CancellationTokenSource();
    _search = cts;
    var gen = Interlocked.Increment(ref _searchGen);
    var query = MessageFilter ?? "";
    var mailboxId = SelectedMailbox?.Id;
    var folder = SelectedFolder?.FullName;
    _ = RunSearchAsync(query, mailboxId, folder, gen, immediate, cts.Token);
  }

  private async Task RunSearchAsync(
    string query,
    string? mailboxId,
    string? folder,
    int gen,
    bool immediate,
    CancellationToken token) {
    try {
      var trimmed = query.Trim();
      if (!immediate && trimmed.Length > 0)
        await Task.Delay(280, token).ConfigureAwait(false);
      if (token.IsCancellationRequested || gen != _searchGen)
        return;

      if (trimmed.Length == 0) {
        if (string.IsNullOrWhiteSpace(mailboxId) || string.IsNullOrWhiteSpace(folder)) {
          await Dispatcher.UIThread.InvokeAsync(() => {
            if (gen != _searchGen)
              return;
            VisibleMessages.Clear();
            RefreshFolderStats();
          });
          return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => {
          if (gen != _searchGen)
            return;
          ApplyVisibleRows([.. Messages]);
        });
        return;
      }

      var hits = await Task.Run(() => {
        float[]? vector = null;
        if (_semantic.IsReady && trimmed.Length >= 2)
          vector = _semantic.EmbedQuery(trimmed);
        return _archive.SearchAll(trimmed, vector);
      }, token).ConfigureAwait(false);

      if (token.IsCancellationRequested || gen != _searchGen)
        return;

      await Dispatcher.UIThread.InvokeAsync(() => {
        if (gen != _searchGen)
          return;
        if (!string.Equals(MessageFilter.Trim(), trimmed, StringComparison.Ordinal))
          return;
        var rows = hits
          .Select(hit => new MessageRowViewModel {
            MailboxId = hit.MailboxId,
            Header = MailArchiveMap.ToHeader(hit)
          })
          .ToList();
        ApplyVisibleRows(rows);
      });
    }
    catch (OperationCanceledException) {
    }
  }

  private void ApplyVisibleRows(List<MessageRowViewModel> rows) {
    var keepKeys = SelectedMessages
      .Select(r => (r.MailboxId, r.Header.Folder, r.Header.Id))
      .ToHashSet();
    var keepOneKey = SelectedMessage is not null
      ? (SelectedMessage.MailboxId, SelectedMessage.Header.Folder, SelectedMessage.Header.Id)
      : ((string MailboxId, string Folder, uint Id)?)null;
    _syncingList = true;
    VisibleMessages.Clear();
    try {
      var searching = MessageFilter.Trim().Length > 0;
      if (!searching && (SelectedMailbox is null || SelectedFolder is null))
        return;

      if (!GroupConversations || searching) {
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

      var keep = VisibleMessages
        .Where(r => keepKeys.Contains((r.MailboxId, r.Header.Folder, r.Header.Id)))
        .ToList();
      SetSelectedMessages(keep);
      SelectedMessage = keepOneKey is { } key
        ? VisibleMessages.FirstOrDefault(r =>
          r.MailboxId == key.MailboxId && r.Header.Folder == key.Folder && r.Header.Id == key.Id)
        : keep.FirstOrDefault();
    }
    finally {
      _syncingList = false;
      RefreshFolderStats();
      NotifyMessageCommands();
      RestoreMessageGridSelection();
    }
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

  private async Task LoadBodyAsync(MailMessageHeader header, CancellationToken token, string? mailboxId = null) {
    var box = Mailboxes.FirstOrDefault(m =>
        m.Id.Equals(mailboxId, StringComparison.OrdinalIgnoreCase))
      ?? SelectedMailbox;
    if (box is null)
      return;
    var eml = _archive.EmlPath(box.Id, header.Folder, header.Id)
      ?? MailArchiveLayout.EmlPath(box, Mailboxes, header.Folder, header.Id);
    var body = await MimeBody.TryFromEmlAsync(header.Folder, header.Id, eml, token).ConfigureAwait(false);
    if (body is null && SessionFor(box) is { IsConnected: true } session) {
      PauseIndexing();
      try {
        var result = await session.GetMessageAsync(header.Folder, header.Id, token).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null) {
          await UiAsync(() => Status = string.Join(" ", result.Messages)).ConfigureAwait(false);
          return;
        }

        body = result.Value;
      }
      finally {
        ResumeIndexing();
      }
    }

    if (body is null) {
      await UiAsync(() =>
        Status = "Message is not in the local archive yet. Connect and Get Messages.")
        .ConfigureAwait(false);
      return;
    }

    StoreArchivedBody(box.Id, header, body);
    await Dispatcher.UIThread.InvokeAsync(async () => {
      if (SelectedMessage is null
          || SelectedMessage.Header.Id != header.Id
          || !SelectedMessage.Header.Folder.Equals(header.Folder, StringComparison.OrdinalIgnoreCase))
        return;

      _reading = body;
      ApplyReading(body);
      if (TracksCertifiedReceipts(box)
          && (body.Envelope.Kind == EnvelopeKind.PecReceipt || body.Envelope.Kind == EnvelopeKind.EidasRem)) {
        _receipts.ApplyEvidence(
          box.Id,
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

      await MarkOpenedReadAsync(header, token).ConfigureAwait(true);
      if (_pendingWindow is { } pending
          && pending.Header.Id == header.Id
          && pending.Header.Folder.Equals(header.Folder, StringComparison.OrdinalIgnoreCase)) {
        _pendingWindow = null;
        ShowMessageWindow(body);
      }
    });
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
      ApplyBodyKind(fattura is not null && UseFatturaPa
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
      var box = Mailboxes.FirstOrDefault(m => m.Id.Equals(mailboxId, StringComparison.OrdinalIgnoreCase));
      var path = box is not null
        ? MailArchiveLayout.EmlPath(box, Mailboxes, header.Folder, header.Id)
        : ArchiveFiles.EmlPath(mailboxId, header.Folder, header.Id);
      File.WriteAllBytes(path, body.RawEml);
      _archive.UpsertBody(
        mailboxId,
        MailArchiveMap.FromHeader(header),
        path,
        MailArchiveMap.BodyText(body, UnwrapEnvelope),
        MailArchiveMap.AttachmentIndex(body, UnwrapEnvelope));
      if (IsInitialSyncComplete(mailboxId))
        _semantic.Wake();
    }
    catch {
    }
  }

  private static void ShowArchiveHint() {
  }

  private async Task ShowArchiveAsync(string mailboxId, string folder, CancellationToken token) {
    var rows = await Task.Run(() => _archive.ListFolder(mailboxId, folder), token).ConfigureAwait(false);
    token.ThrowIfCancellationRequested();
    await UiAsync(() => {
      if (SelectedMailbox?.Id != mailboxId
          || SelectedFolder is null
          || !SelectedFolder.FullName.Equals(folder, StringComparison.OrdinalIgnoreCase))
        return;
      BindArchive(mailboxId, folder, rows);
    }).ConfigureAwait(false);
  }

  private void ShowArchive(string mailboxId, string folder) =>
    BindArchive(mailboxId, folder, _archive.ListFolder(mailboxId, folder));

  private void BindArchive(string mailboxId, string folder, IReadOnlyList<MailArchiveHeader> rows) {
    Messages.Clear();
    SelectedMessages.Clear();
    SelectedMessage = null;
    ClearReading();
    foreach (var row in rows) {
      var header = MailArchiveMap.ToHeader(row);
      ReconcileHeader(header, folder);
      Messages.Add(new MessageRowViewModel { MailboxId = mailboxId, Header = header });
    }

    if (TracksCertifiedReceipts(mailboxId) && MailFolderRole.Kind(null, folder) == "sent")
      OverlaySentMarks();
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
    _catalogFolders.Clear();
    _backfill?.Cancel();
    _backfill?.Dispose();
    _backfill = null;
    _indexHold.ClearNow();
  }

  private void EnsureIndexing(string mailboxId) {
    if (!string.IsNullOrWhiteSpace(mailboxId))
      _indexMailboxes.Add(mailboxId);
    TrackConnectedMailboxes();
    if (_backfill is not null || _indexMailboxes.Count == 0)
      return;
    StartIndexing();
  }

  private void EnsureBodyBackfill(string mailboxId) {
    if (string.IsNullOrWhiteSpace(mailboxId) || _backfill is not null)
      return;
    if (_archive.MissingBodyCount([mailboxId]) == 0)
      return;
    EnsureIndexing(mailboxId);
  }

  private void StopIndexingMailbox(string mailboxId) {
    _indexMailboxes.Remove(mailboxId);
    _catalogDone.Remove(mailboxId);
    _catalogFolders.RemoveWhere(key =>
      key.StartsWith(mailboxId + "\n", StringComparison.OrdinalIgnoreCase));
    if (_indexMailboxes.Count > 0)
      return;
    _backfill?.Cancel();
    _backfill?.Dispose();
    _backfill = null;
    _indexHold.ClearNow();
  }

  private void StartIndexing() {
    if (_backfill is not null || _indexMailboxes.Count == 0)
      return;
    var cts = new CancellationTokenSource();
    _backfill = cts;
    _ = IndexAllAsync(cts);
  }

  private async Task IndexAllAsync(CancellationTokenSource cts) {
    var token = cts.Token;
    await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
    try {
      while (!token.IsCancellationRequested) {
        await WaitIfIndexingPausedAsync(token).ConfigureAwait(false);
        var (snapshot, readyIds) = await Dispatcher.UIThread.InvokeAsync(() => {
          TrackConnectedMailboxes();
          return (CaptureIndexSnapshot(), ReadyMailboxIds());
        });
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        if (snapshot.Count == 0)
          break;

        var sessions = snapshot.ToDictionary(
          s => s.Id,
          s => s.Session,
          StringComparer.OrdinalIgnoreCase);
        var cataloged = false;
        foreach (var box in snapshot) {
          if (box.CatalogDone
              || box.Session is not { IsConnected: true }
              || !box.Session.SupportsFolders)
            continue;
          var pendingFolders = false;
          foreach (var folder in box.Folders) {
            token.ThrowIfCancellationRequested();
            var open = box.OpenFolder is not null
              && folder.FullName.Equals(box.OpenFolder, StringComparison.OrdinalIgnoreCase);
            if (open || box.CatalogedFolders.Contains(folder.FullName))
              continue;
            pendingFolders = true;
            cataloged = true;
            await WaitIfIndexingPausedAsync(token).ConfigureAwait(false);
            var known = _archive.Uids(box.Id, folder.FullName);
            var listed = await box.Session
              .ListMessagesAsync(folder.FullName, known, token, IndexCatalogChunk)
              .ConfigureAwait(false);
            if (!listed.IsSuccess)
              continue;
            var sync = listed.Value ?? new MailFolderSync();
            ApplyFolderSync(box.Id, folder.FullName, sync);
            await SetIndexLineAsync(GlobalIndexLine(), token)
              .ConfigureAwait(false);
            var counts = _archive.FolderCounts(box.Id, folder.FullName);
            await UiAsync(() => RefreshIndexedFolderCounts(box.Id, folder.FullName, counts))
              .ConfigureAwait(false);
            if (!sync.Incomplete) {
              await UiAsync(() => {
                _catalogFolders.Add(CatalogKey(box.Id, folder.FullName));
                TryCompleteInitialSync(MailboxById(box.Id));
              }).ConfigureAwait(false);
            }

            await Task.Delay(15, token).ConfigureAwait(false);
          }

          if (!pendingFolders && box.Folders.All(f => box.CatalogedFolders.Contains(f.FullName)))
            await UiAsync(() => TryCompleteInitialSync(MailboxById(box.Id))).ConfigureAwait(false);
        }

        await WaitIfIndexingPausedAsync(token).ConfigureAwait(false);
        var pending = readyIds.Count == 0 ? [] : _archive.MissingBodies(readyIds, 16);
        if (pending.Count == 0) {
          if (!cataloged)
            break;
          continue;
        }

        await SetIndexLineAsync(GlobalIndexLine(), token)
          .ConfigureAwait(false);
        var fetched = 0;
        foreach (var item in pending) {
          token.ThrowIfCancellationRequested();
          await WaitIfIndexingPausedAsync(token).ConfigureAwait(false);
          if (!sessions.TryGetValue(item.MailboxId, out var session) || session is not { IsConnected: true })
            continue;
          try {
            var result = await session.GetMessageAsync(item.Folder, item.Uid, token).ConfigureAwait(false);
            if (result.IsSuccess && result.Value is not null)
              StoreArchivedBody(item.MailboxId, result.Value.Header, result.Value);
            fetched++;
          }
          catch (OperationCanceledException) {
            throw;
          }
          catch {
          }
        }

        if (fetched == 0 && !cataloged) {
          await Task.Delay(500, token).ConfigureAwait(false);
          continue;
        }
      }
    }
    catch (OperationCanceledException) {
      return;
    }
    finally {
      await Dispatcher.UIThread.InvokeAsync(() => {
        if (_backfill != cts)
          return;
        _backfill.Dispose();
        _backfill = null;
        if (token.IsCancellationRequested)
          return;
        foreach (var id in _indexMailboxes.ToList()) {
          if (_catalogDone.Contains(id) && _archive.MissingBodyCount([id]) == 0)
            _indexMailboxes.Remove(id);
        }

        var again = _indexMailboxes.Any(id => SessionFor(MailboxById(id)) is { IsConnected: true });
        if (!again) {
          _indexHold.Set("");
          return;
        }

        StartIndexing();
      });
    }
  }

  private List<IndexMailboxSnap> CaptureIndexSnapshot() {
    var rows = new List<IndexMailboxSnap>();
    foreach (var id in _indexMailboxes.ToList()) {
      var box = MailboxById(id);
      var session = SessionFor(box);
      var cataloged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var folder in FoldersFor(box)) {
        if (_catalogFolders.Contains(CatalogKey(id, folder.FullName)))
          cataloged.Add(folder.FullName);
      }

      rows.Add(new IndexMailboxSnap(
        id,
        session,
        FoldersFor(box).Select(f => (f.FullName, f.Name)).ToList(),
        SelectedMailbox?.Id == id ? SelectedFolder?.FullName : null,
        _catalogDone.Contains(id),
        cataloged));
    }

    return rows;
  }

  private readonly record struct IndexMailboxSnap(
    string Id,
    IMailSession? Session,
    IReadOnlyList<(string FullName, string Name)> Folders,
    string? OpenFolder,
    bool CatalogDone,
    IReadOnlySet<string> CatalogedFolders);

  private async Task SetIndexLineAsync(string line, CancellationToken token) =>
    await UiAsync(() => {
      if (!token.IsCancellationRequested)
        _indexHold.Set(line);
    }).ConfigureAwait(false);

  private void TrackConnectedMailboxes() {
    foreach (var box in Mailboxes) {
      if (SessionFor(box) is { IsConnected: true })
        _indexMailboxes.Add(box.Id);
    }
  }

  private List<string> ReadyMailboxIds() {
    var ids = new List<string>();
    foreach (var box in Mailboxes) {
      if (IsInitialSyncComplete(box.Id))
        ids.Add(box.Id);
    }

    return ids;
  }

  private string GlobalIndexLine() {
    var total = _archive.TotalMessageCount();
    var missing = _archive.MissingBodyCount();
    return MailIndexProgress.Line(Math.Max(0, total - missing), total);
  }

  private MailboxAccount? MailboxById(string mailboxId) =>
    Mailboxes.FirstOrDefault(m => m.Id.Equals(mailboxId, StringComparison.OrdinalIgnoreCase))
    ?? _files.Current.FindMailbox(mailboxId);

  private void RefreshIndexedFolderCounts(string mailboxId, string folder, (int Total, int Unread) counts) {
    var view = FoldersFor(MailboxById(mailboxId))
      .FirstOrDefault(f => f.FullName.Equals(folder, StringComparison.OrdinalIgnoreCase));
    if (view is null)
      return;
    view.Total = counts.Total;
    view.Unread = counts.Unread;
    NotifyFolderCounts();
  }

  private async Task DisconnectMailboxAsync(string mailboxId) {
    StopIndexingMailbox(mailboxId);
    IMailSession? session;
    lock (_connections)
      _connections.Remove(mailboxId, out session);
    if (session is not null)
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
    StartComposeCommand.NotifyCanExecuteChanged();
    GetMessagesCommand.NotifyCanExecuteChanged();
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
    DetachPstCommand.NotifyCanExecuteChanged();
    OnPropertyChanged(nameof(ShowFolderContext));
    OnPropertyChanged(nameof(ShowDetachPst));
    OnPropertyChanged(nameof(EmptyFolderLabel));
  }

  partial void OnHasReadingFilesChanged(bool value) =>
    SaveAttachmentsZipCommand.NotifyCanExecuteChanged();

  private IReadOnlyList<MessageRowViewModel> TargetRows() =>
    SelectedMessages.Count > 0
      ? SelectedMessages.ToList()
      : SelectedMessage is { } one ? [one] : [];

  private IReadOnlyList<FolderNodeViewModel> TargetFolderNodes() {
    if (SelectedFolderNodes.Count > 0)
      return SelectedFolderNodes.Where(node => node is { IsAccount: false, Folder: not null }).ToList();
    return SelectedFolderNode is { IsAccount: false, Folder: not null } node ? [node] : [];
  }

  private IReadOnlyList<(MailboxAccount Box, FolderRowViewModel Folder)> TargetCustomFolderRoots() {
    var picks = new List<(MailboxAccount Box, FolderRowViewModel Folder)>();
    foreach (var node in TargetFolderNodes()) {
      if (node.Mailbox is not { } box || node.Folder is not { } folder)
        continue;
      if (!MailFolderRole.IsCustom(folder.Name, folder.FullName))
        continue;
      picks.Add((box, folder));
    }

    var roots = MailSelection.Roots(picks.Select(pick => (pick.Box.Id, pick.Folder.FullName)));
    return picks
      .Where(pick => roots.Any(root =>
        root.MailboxId.Equals(pick.Box.Id, StringComparison.OrdinalIgnoreCase)
        && root.Folder.Equals(pick.Folder.FullName, StringComparison.OrdinalIgnoreCase)))
      .ToList();
  }

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
    IReadOnlyList<MessageRowViewModel>? rows = null,
    string? destMailboxId = null,
    string? sourceMailboxId = null) {
    var sourceBox = MailboxById(sourceMailboxId ?? SelectedMailbox?.Id ?? "")
      ?? SelectedMailbox;
    var destBox = MailboxById(destMailboxId ?? sourceBox?.Id ?? "")
      ?? sourceBox;
    if (sourceBox is null || destBox is null || ids.Count == 0)
      return;
    var source = fromFolder ?? SelectedFolder?.FullName;
    if (string.IsNullOrWhiteSpace(source))
      return;
    await RunAsync(Copy.Moving, async token => {
      var sourceSession = await EnsureMailboxReadyAsync(sourceBox, token);
      var destSession = destBox.Id.Equals(sourceBox.Id, StringComparison.OrdinalIgnoreCase)
        ? sourceSession
        : await EnsureMailboxReadyAsync(destBox, token);
      var dest = destFolder;
      if (string.IsNullOrWhiteSpace(dest))
        dest = InboxOf(destBox)?.FullName ?? "";
      if (string.IsNullOrWhiteSpace(dest)
          || destSession is not { IsConnected: true, SupportsFolders: true }) {
        Status = Copy.CannotMoveSystemFolder;
        return;
      }

      if (sourceBox.Id.Equals(destBox.Id, StringComparison.OrdinalIgnoreCase)
          && source.Equals(dest, StringComparison.OrdinalIgnoreCase)) {
        Status = Copy.AlreadyInFolder;
        return;
      }

      if (sourceBox.Id.Equals(destBox.Id, StringComparison.OrdinalIgnoreCase)) {
        if (sourceSession is not { IsConnected: true, SupportsFolders: true }) {
          Status = Copy.CannotMoveSystemFolder;
          return;
        }

        var result = await sourceSession.MoveMessagesAsync(source, ids, dest, token);
        if (!result.IsSuccess) {
          Status = string.Join(" ", result.Messages);
          return;
        }

        ApplyFolderMoveCounts(sourceBox, destBox, source, dest, ids.Count, rows);
        DropArchived(sourceBox.Id, source, ids.ToList());
        RemoveDroppedRows(sourceBox.Id, source, ids);
        if (SelectedMailbox?.Id == destBox.Id && SelectedFolder is not null)
          await LoadMessagesAsync(SelectedFolder.FullName, token);
        Status = string.Format(Copy.MovedMessages, ids.Count);
        return;
      }

      if (sourceSession is null) {
        Status = Copy.CannotMoveSystemFolder;
        return;
      }

      var copied = new List<uint>();
      foreach (var id in ids) {
        if (await CopyUidToMailboxAsync(sourceBox, sourceSession, destBox, source, dest, id, token))
          copied.Add(id);
      }

      if (copied.Count == 0) {
        Status = string.Format(Copy.MovedMessages, 0);
        return;
      }

      await RemoveCopiedUidsAsync(sourceBox, sourceSession, source, copied, token);
      ApplyFolderMoveCounts(sourceBox, destBox, source, dest, copied.Count, rows);
      RemoveDroppedRows(sourceBox.Id, source, copied);
      await LoadFoldersAsync(destBox, destSession, token);
      if (SelectedMailbox?.Id == destBox.Id && SelectedFolder is not null)
        await LoadMessagesAsync(SelectedFolder.FullName, token);
      else if (SelectedMailbox?.Id == sourceBox.Id && SelectedFolder is not null)
        await LoadMessagesAsync(SelectedFolder.FullName, token);
      Status = string.Format(Copy.MovedMessages, copied.Count);
    });
  }

  private async Task<IMailSession?> EnsureMailboxReadyAsync(MailboxAccount box, CancellationToken token) {
    await ConnectMailboxCoreAsync(box, PasswordFor(box), token);
    var session = SessionFor(box);
    if (session is not { IsConnected: true })
      return session;
    if (FoldersFor(box).Count == 0)
      await LoadFoldersAsync(box, session, token);
    return session;
  }

  private async Task<List<uint>> FolderUidsAsync(
    MailboxAccount box,
    IMailSession session,
    string folder,
    CancellationToken token) {
    var ids = new HashSet<uint>(_archive.Uids(box.Id, folder));
    var listed = await session.ListMessagesAsync(folder, ids.Count == 0 ? null : ids, token);
    if (listed.IsSuccess && listed.Value?.Present is { } present) {
      foreach (var id in present)
        ids.Add(id);
    }

    return ids.ToList();
  }

  private IReadOnlyList<FolderRowViewModel> FolderSlice(MailboxAccount box, string root) =>
    FoldersFor(box)
      .Where(folder => MailFolderPath.IsSelfOrUnder(folder.FullName, root))
      .OrderBy(folder => folder.FullName.Length)
      .ThenBy(folder => folder.FullName, StringComparer.OrdinalIgnoreCase)
      .ToList();

  private IEnumerable<string> SiblingNames(MailboxAccount box, string? destParent, string? exceptFolder) {
    foreach (var folder in FoldersFor(box)) {
      if (exceptFolder is not null
          && folder.FullName.Equals(exceptFolder, StringComparison.OrdinalIgnoreCase))
        continue;
      if (MailFolderPath.SameParent(folder.FullName, destParent))
        yield return folder.Name;
    }
  }

  private string? FindMovedFolder(MailboxAccount box, string? destParent, string leaf) =>
    FoldersFor(box).FirstOrDefault(folder =>
      MailFolderPath.Leaf(folder.FullName).Equals(leaf, StringComparison.OrdinalIgnoreCase)
      && MailFolderPath.SameParent(folder.FullName, destParent))?.FullName;

  private void RememberMovedFolder(string sourceMailboxId, string from, string to, string? destMailboxId) {
    _archive.RewriteFolderPrefix(sourceMailboxId, from, to);
    if (MailRuleEngine.RetargetFolders(ActiveRules(), sourceMailboxId, from, to, destMailboxId) > 0)
      _files.Save(_files.Current);
  }

  private async Task SelectAfterFolderMoveAsync(
    MailboxAccount source,
    string from,
    string to,
    string? openFolder,
    CancellationToken token,
    MailboxAccount? destBox = null) {
    if (SelectedMailbox?.Id != source.Id)
      return;
    if (string.IsNullOrWhiteSpace(openFolder) || !MailFolderPath.IsSelfOrUnder(openFolder, from))
      return;
    FolderRowViewModel? next = null;
    if (destBox is null || destBox.Id.Equals(source.Id, StringComparison.OrdinalIgnoreCase)) {
      var path = MailFolderPath.Rewrite(openFolder, from, to);
      next = FoldersFor(source).FirstOrDefault(f =>
        path is not null && f.FullName.Equals(path, StringComparison.OrdinalIgnoreCase))
        ?? FoldersFor(source).FirstOrDefault(f =>
          f.FullName.Equals(to, StringComparison.OrdinalIgnoreCase));
    }

    SelectedFolder = next ?? InboxOf(source) ?? FoldersFor(source).FirstOrDefault();
    if (SelectedFolder is not null)
      await LoadMessagesAsync(SelectedFolder.FullName, token);
  }

  private async Task RemoveCopiedUidsAsync(
    MailboxAccount source,
    IMailSession? session,
    string sourceFolder,
    IReadOnlyList<uint> ids,
    CancellationToken token) {
    if (ids.Count == 0)
      return;
    if (session is { IsConnected: true, SupportsFolders: true }) {
      var trash = MailRetention.ResolveTrash(
        FoldersFor(source).Select(f => (f.Name, f.FullName)).ToList());
      if (!string.IsNullOrWhiteSpace(trash)
          && !trash.Equals(sourceFolder, StringComparison.OrdinalIgnoreCase))
        await session.MoveMessagesAsync(sourceFolder, ids, trash, token);
      else
        await session.SetMessageFlagsAsync(sourceFolder, ids, new MailFlagUpdate { Deleted = true }, token);
    }

    DropArchived(source.Id, sourceFolder, ids.ToList());
  }

  private void RemoveDroppedRows(string mailboxId, string folder, IReadOnlyList<uint> ids) {
    if (SelectedMailbox?.Id != mailboxId
        || SelectedFolder is null
        || !SelectedFolder.FullName.Equals(folder, StringComparison.OrdinalIgnoreCase))
      return;
    var rows = Messages.Where(row => ids.Contains(row.Header.Id)).ToList();
    if (rows.Count > 0)
      RemoveRows(rows);
  }

  private async Task<bool> ApplyFlagsAsync(
    MailFlagUpdate update,
    Action<MessageRowViewModel>? apply,
    IReadOnlyList<MessageRowViewModel>? rows = null) {
    var session = ActiveSession;
    if (session is not { IsConnected: true, SupportsFolders: true } || SelectedFolder is null) {
      Status = Copy.Connecting;
      return false;
    }
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
    NotifyMessageCommands();
    RestoreMessageGridSelection();
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
    MailboxAccount sourceBox,
    MailboxAccount destBox,
    string source,
    string destFolder,
    int count,
    IReadOnlyList<MessageRowViewModel>? rows) {
    var unread = rows?.Count(r => !r.Header.IsSeen) ?? 0;
    var from = FolderRow(sourceBox, source);
    var dest = FolderRow(destBox, destFolder);
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

  private FolderRowViewModel? FolderRow(MailboxAccount? box, string fullName) =>
    FoldersFor(box).FirstOrDefault(f =>
      f.FullName.Equals(fullName, StringComparison.OrdinalIgnoreCase));

  private void NotifyFolderCounts() {
    foreach (var account in FolderTree) {
      ApplyRetentionBadge(account);
      foreach (var child in account.Children)
        child.NotifyCounts();
      account.NotifyCounts();
    }
  }

  public void RefreshRetentionBadges() =>
    NotifyFolderCounts();

  private void ApplyRetentionBadge(FolderNodeViewModel node) {
    if (node.Mailbox is { } box && node.Folder is { } folder)
      node.RetentionDays = RetentionDaysFor(box.Id, folder.FullName);
    else
      node.RetentionDays = 0;
    foreach (var child in node.Children)
      ApplyRetentionBadge(child);
  }

  private int RetentionDaysFor(string mailboxId, string folder) {
    foreach (var row in _files.Current.Retention ?? []) {
      if (row.MailboxId.Equals(mailboxId, StringComparison.OrdinalIgnoreCase)
          && row.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase))
        return row.Days;
    }

    return 0;
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

  private void KickFolderLoad(string folder) {
    if (!Dispatcher.UIThread.CheckAccess()) {
      Dispatcher.UIThread.Post(() => KickFolderLoad(folder));
      return;
    }

    _folderLoad?.Cancel();
    _folderLoad?.Dispose();
    var cts = new CancellationTokenSource();
    _folderLoad = cts;
    Status = Copy.OpeningFolder;
    _ = OpenFolderAsync(folder, cts.Token);
  }

  private async Task OpenFolderAsync(string folder, CancellationToken token) {
    try {
      await LoadMessagesAsync(folder, token).ConfigureAwait(false);
    }
    catch (OperationCanceledException) {
    }
  }

  private void PauseIndexing() =>
    Interlocked.Increment(ref _indexPause);

  private void ResumeIndexing() =>
    Interlocked.Decrement(ref _indexPause);

  private async Task WaitIfIndexingPausedAsync(CancellationToken token) {
    while (Volatile.Read(ref _indexPause) > 0) {
      token.ThrowIfCancellationRequested();
      await Task.Delay(40, token).ConfigureAwait(false);
    }
  }

  private static string CatalogKey(string mailboxId, string folder) =>
    mailboxId + "\n" + folder;

  private bool IsInitialSyncComplete(string mailboxId) {
    var box = MailboxById(mailboxId);
    return box?.InitialSyncCompleted == true || _catalogDone.Contains(mailboxId);
  }

  private void MarkExistingSyncComplete() {
    var changed = false;
    foreach (var box in _files.Current.Mailboxes) {
      if (box.InitialSyncCompleted)
        continue;
      if (_archive.MessageCount(box.Id) <= 0)
        continue;
      box.InitialSyncCompleted = true;
      changed = true;
    }

    foreach (var box in _files.Current.Mailboxes)
      _archive.SetKeywordIndexEnabled(box.Id, box.InitialSyncCompleted);
    if (changed)
      _files.Save(_files.Current);
  }

  private void MarkFolderCataloged(MailboxAccount box, string folder, IMailSession? session) {
    _catalogFolders.Add(CatalogKey(box.Id, folder));
    if (session is not { SupportsFolders: true } || AllFoldersCataloged(box))
      TryCompleteInitialSync(box);
  }

  private bool AllFoldersCataloged(MailboxAccount box) {
    var folders = FoldersFor(box);
    if (folders.Count == 0)
      return false;
    return folders.All(folder => _catalogFolders.Contains(CatalogKey(box.Id, folder.FullName)));
  }

  private void TryCompleteInitialSync(MailboxAccount? box) {
    if (box is null || box.InitialSyncCompleted)
      return;
    if (FoldersFor(box).Count > 0 && !AllFoldersCataloged(box))
      return;
    box.InitialSyncCompleted = true;
    _catalogDone.Add(box.Id);
    var stored = _files.Current.FindMailbox(box.Id);
    if (stored is not null)
      stored.InitialSyncCompleted = true;
    _files.Save(_files.Current);
    _archive.SetKeywordIndexEnabled(box.Id, true);
    _archive.RebuildKeywordIndex(box.Id);
    _semantic.NotifySettingsChanged();
  }

  private async Task UiAsync(Action work) {
    if (Dispatcher.UIThread.CheckAccess())
      work();
    else
      await Dispatcher.UIThread.InvokeAsync(work);

    await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
  }

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

  public string? ConfirmLabel { get; init; }
}
