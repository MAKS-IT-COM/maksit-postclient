using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace MaksIT.PostClient.UI.ViewModels;


public sealed partial class RuleEditViewModel : ObservableObject {
  public RuleEditViewModel() {
    Conditions.CollectionChanged += (_, _) => Refresh();
  }

  public static RuleEditViewModel FromModel(MailRule rule) {
    var row = new RuleEditViewModel {
      Id = string.IsNullOrWhiteSpace(rule.Id) ? Guid.NewGuid().ToString("N") : rule.Id,
      Name = rule.Name,
      Enabled = rule.Enabled,
      Sequence = rule.Sequence,
      Logic = string.IsNullOrWhiteSpace(rule.Logic) ? "and" : rule.Logic,
      Action = rule.Action,
      MailboxId = rule.MailboxId,
      FolderMailboxId = string.IsNullOrWhiteSpace(rule.FolderMailboxId) ? rule.MailboxId : rule.FolderMailboxId,
      Folder = rule.Folder,
      Label = rule.Label,
      Stop = rule.Stop
    };
    foreach (var condition in rule.Conditions)
      row.Conditions.Add(new MailRuleCondition {
        Field = condition.Field,
        Op = condition.Op,
        Value = condition.Value
      });
    if (row.Conditions.Count == 0)
      row.Conditions.Add(new MailRuleCondition { Field = "from", Op = "contains" });
    row.Refresh();
    return row;
  }

  public MailRule ToModel() =>
    new() {
      Id = Id,
      Name = Name.Trim(),
      Enabled = Enabled,
      Sequence = Sequence,
      Logic = Logic,
      Action = Action,
      MailboxId = MailboxId.Trim(),
      FolderMailboxId = FolderMailboxId.Trim(),
      Folder = Folder.Trim(),
      Label = Label.Trim(),
      Stop = Stop,
      Conditions = Conditions
        .Select(c => new MailRuleCondition {
          Field = c.Field,
          Op = string.IsNullOrWhiteSpace(c.Op) ? "contains" : c.Op,
          Value = c.Value ?? ""
        })
        .ToList()
    };

  public string Id { get; init; } = Guid.NewGuid().ToString("N");

  internal Func<string, IReadOnlyList<(string Name, string FullName)>>? FoldersOf { get; set; }

  internal Func<string, string>? MailboxTitleOf { get; set; }

  internal Func<string, Task>? EnsureFolders { get; set; }

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(When))]
  [NotifyPropertyChangedFor(nameof(Do))]
  private string name = "";

  [ObservableProperty]
  private bool enabled = true;

  [ObservableProperty]
  private int sequence;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(When))]
  private string logic = "and";

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(Do))]
  [NotifyPropertyChangedFor(nameof(NeedsFolder))]
  [NotifyPropertyChangedFor(nameof(FolderUnresolved))]
  private string action = MailRuleAction.Move;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(Do))]
  [NotifyPropertyChangedFor(nameof(FolderUnresolved))]
  private string mailboxId = "";

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(Do))]
  [NotifyPropertyChangedFor(nameof(FolderUnresolved))]
  private string folderMailboxId = "";

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(Do))]
  [NotifyPropertyChangedFor(nameof(FolderUnresolved))]
  private string folder = "";

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(Do))]
  private string label = "";

  [ObservableProperty]
  private bool stop;

  public ObservableCollection<MailRuleCondition> Conditions { get; } = [];

  public bool NeedsFolder =>
    Action == MailRuleAction.Move;

  public bool FolderUnresolved =>
    NeedsFolder && MailRuleEngine.ExactFolder(Folder, FoldersOf?.Invoke(FolderOwner) ?? []) is null;

  public string FolderOwner =>
    MailRuleEngine.FolderMailbox(new MailRule { MailboxId = MailboxId, FolderMailboxId = FolderMailboxId });

  public string When {
    get {
      var parts = Conditions
        .Where(c => !string.IsNullOrWhiteSpace(c.Value) || (c.Op ?? "").Equals("exists", StringComparison.OrdinalIgnoreCase))
        .Select(c => (c.Field ?? "from") + " " + (c.Op ?? "contains") + " " + c.Value)
        .ToList();
      return parts.Count == 0 ? "" : string.Join(" " + Logic + " ", parts);
    }
  }

  public string Do {
    get {
      var account = MailboxTitleOf?.Invoke(MailboxId) ?? "";
      var destAccount = MailboxTitleOf?.Invoke(FolderOwner) ?? "";
      string dest;
      if (Action == MailRuleAction.Move) {
        dest = string.IsNullOrWhiteSpace(Folder) ? Action : Action + " → " + Folder;
        if (!string.IsNullOrWhiteSpace(destAccount)
            && !destAccount.Equals(account, StringComparison.OrdinalIgnoreCase))
          dest += " · " + destAccount;
      }
      else if (Action == MailRuleAction.Label)
        dest = string.IsNullOrWhiteSpace(Label) ? Action : Action + " → " + Label;
      else
        dest = Action;
      return string.IsNullOrWhiteSpace(account) ? dest : dest + " · " + account;
    }
  }

  public void Refresh() {
    OnPropertyChanged(nameof(When));
    OnPropertyChanged(nameof(Do));
    OnPropertyChanged(nameof(NeedsFolder));
    OnPropertyChanged(nameof(FolderUnresolved));
    OnPropertyChanged(nameof(FolderOwner));
  }

  partial void OnMailboxIdChanged(string value) =>
    _ = EnsureThenRefreshAsync(value);

  partial void OnFolderMailboxIdChanged(string value) =>
    _ = EnsureThenRefreshAsync(value);

  private async Task EnsureThenRefreshAsync(string mailboxId) {
    if (EnsureFolders is not null && !string.IsNullOrWhiteSpace(mailboxId))
      await EnsureFolders(mailboxId).ConfigureAwait(true);
    Refresh();
  }
}


public sealed partial class RulesViewModel : ObservableObject {
  private readonly ConfigurationFileService _files;
  private readonly IReadOnlyList<MailboxAccount> _mailboxes;
  private readonly Func<string, IReadOnlyList<(string Name, string FullName)>> _foldersOf;
  private readonly Func<string, Task>? _ensureFolders;
  private readonly Func<Task<string>>? _runAll;
  private readonly string _preferredMailboxId;

  public RulesViewModel(
    ConfigurationFileService files,
    IEnumerable<MailboxAccount>? mailboxes = null,
    Func<string, IReadOnlyList<(string Name, string FullName)>>? foldersOf = null,
    Func<string, Task>? ensureFolders = null,
    string? preferredMailboxId = null,
    Func<Task<string>>? runAll = null) {
    _files = files;
    _mailboxes = (mailboxes ?? files.Current.Mailboxes ?? []).ToList();
    _foldersOf = foldersOf ?? (_ => []);
    _ensureFolders = ensureFolders;
    _runAll = runAll;
    _preferredMailboxId = preferredMailboxId ?? "";
    Accounts = _mailboxes
      .Select(box => new ChoiceRow { Id = box.Id, Title = box.Label })
      .ToList();
    foreach (var rule in files.Current.Rules ?? [])
      Rules.Add(Bind(RuleEditViewModel.FromModel(rule)));
    Selected = Rules.FirstOrDefault();
  }

  public UiCopy Copy =>
    UiLocale.Copy;

  public IReadOnlyList<ChoiceRow> Accounts { get; }

  public ObservableCollection<RuleEditViewModel> Rules { get; } = [];

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(HasSelected))]
  [NotifyPropertyChangedFor(nameof(SelectedAccount))]
  [NotifyPropertyChangedFor(nameof(SelectedFolderAccount))]
  private RuleEditViewModel? selected;

  public bool HasSelected =>
    Selected is not null;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(HasStatus))]
  [NotifyCanExecuteChangedFor(nameof(RunAllCommand))]
  private bool isRunning;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(HasStatus))]
  private string status = "";

  public bool HasStatus =>
    !string.IsNullOrWhiteSpace(Status);

  public ChoiceRow? SelectedAccount {
    get => Accounts.FirstOrDefault(a => a.Id.Equals(Selected?.MailboxId, StringComparison.OrdinalIgnoreCase));
    set {
      if (Selected is null)
        return;
      Selected.MailboxId = value?.Id ?? "";
      OnPropertyChanged();
      _ = EnsureThenRefreshAsync(Selected);
    }
  }

  public ChoiceRow? SelectedFolderAccount {
    get => Accounts.FirstOrDefault(a => a.Id.Equals(Selected?.FolderOwner, StringComparison.OrdinalIgnoreCase));
    set {
      if (Selected is null || !Selected.NeedsFolder)
        return;
      Selected.FolderMailboxId = value?.Id ?? Selected.MailboxId;
      OnPropertyChanged();
      _ = EnsureThenRefreshAsync(Selected);
    }
  }

  public IReadOnlyList<string> Logics { get; } = ["and", "or"];

  public IReadOnlyList<string> ActionIds { get; } = [
    MailRuleAction.Move,
    MailRuleAction.Delete,
    MailRuleAction.MarkRead,
    MailRuleAction.Flag,
    MailRuleAction.Label
  ];

  public void Persist() {
    var configuration = _files.Current;
    configuration.Rules = Rules.Select(r => r.ToModel()).ToList();
    _files.Save(configuration);
  }

  public int Import(string path) {
    var imported = OutlookRules.FromFile(path);
    foreach (var rule in imported) {
      rule.MailboxId = GuessMailbox(rule);
      rule.FolderMailboxId = GuessFolderMailbox(rule);
    }
    var merged = Rules.Select(r => r.ToModel()).ToList();
    MailRuleEngine.Merge(merged, imported);
    Rules.Clear();
    foreach (var rule in merged)
      Rules.Add(Bind(RuleEditViewModel.FromModel(rule)));
    Selected = Rules.FirstOrDefault();
    Persist();
    _ = PrepareAsync();
    RunAllCommand.NotifyCanExecuteChanged();
    return imported.Count(r => r.HasWork);
  }

  public void Export(string path) =>
    File.WriteAllBytes(path, OutlookRules.ToJson(Rules.Select(r => r.ToModel())));

  [RelayCommand]
  private void Add() {
    var row = Bind(new RuleEditViewModel {
      Name = "Rule " + (Rules.Count + 1),
      Action = MailRuleAction.Move,
      MailboxId = _preferredMailboxId,
      FolderMailboxId = _preferredMailboxId
    });
    row.Conditions.Add(new MailRuleCondition { Field = "from", Op = "contains" });
    row.Refresh();
    Rules.Add(row);
    Selected = row;
    Persist();
    RunAllCommand.NotifyCanExecuteChanged();
  }

  [RelayCommand]
  private void Delete() {
    if (Selected is null)
      return;
    Rules.Remove(Selected);
    Selected = Rules.FirstOrDefault();
    Persist();
    RunAllCommand.NotifyCanExecuteChanged();
  }

  [RelayCommand]
  private void AddCondition() {
    if (Selected is null)
      return;
    Selected.Conditions.Add(new MailRuleCondition { Field = "from", Op = "contains" });
    Selected.Refresh();
  }

  [RelayCommand(CanExecute = nameof(CanRunAll))]
  private async Task RunAllAsync() {
    Persist();
    if (_runAll is null)
      return;
    IsRunning = true;
    try {
      Status = await _runAll().ConfigureAwait(true);
    }
    catch (Exception ex) {
      Status = ex.Message;
    }
    finally {
      IsRunning = false;
    }
  }

  private bool CanRunAll() =>
    !IsRunning && _runAll is not null && Rules.Count > 0;

  [RelayCommand]
  private void RemoveCondition(MailRuleCondition? condition) {
    if (Selected is null || condition is null)
      return;
    Selected.Conditions.Remove(condition);
    if (Selected.Conditions.Count == 0)
      Selected.Conditions.Add(new MailRuleCondition { Field = "from", Op = "contains" });
    Selected.Refresh();
  }

  public async Task PrepareAsync() {
    var ids = Rules
      .SelectMany(r => new[] { r.MailboxId, r.FolderMailboxId })
      .Append(_preferredMailboxId)
      .Where(id => !string.IsNullOrWhiteSpace(id))
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToList();
    if (_ensureFolders is not null) {
      foreach (var id in ids)
        await _ensureFolders(id).ConfigureAwait(true);
    }

    foreach (var rule in Rules)
      rule.Refresh();
    OnPropertyChanged(nameof(SelectedAccount));
    OnPropertyChanged(nameof(SelectedFolderAccount));
  }

  private async Task EnsureThenRefreshAsync(RuleEditViewModel row) {
    if (_ensureFolders is not null) {
      foreach (var id in new[] { row.MailboxId, row.FolderMailboxId }.Distinct(StringComparer.OrdinalIgnoreCase)) {
        if (!string.IsNullOrWhiteSpace(id))
          await _ensureFolders(id).ConfigureAwait(true);
      }
    }
    if (Selected == row)
      row.Refresh();
  }

  private RuleEditViewModel Bind(RuleEditViewModel row) {
    row.FoldersOf = FoldersFor;
    row.MailboxTitleOf = MailboxTitle;
    row.EnsureFolders = _ensureFolders;
    row.Refresh();
    return row;
  }

  private IReadOnlyList<(string Name, string FullName)> FoldersFor(string mailboxId) =>
    string.IsNullOrWhiteSpace(mailboxId) ? [] : _foldersOf(mailboxId);

  private string MailboxTitle(string mailboxId) {
    if (string.IsNullOrWhiteSpace(mailboxId))
      return "";
    return Accounts.FirstOrDefault(a => a.Id.Equals(mailboxId, StringComparison.OrdinalIgnoreCase))?.Title
      ?? "";
  }

  private string GuessMailbox(MailRule rule) {
    if (!string.IsNullOrWhiteSpace(rule.MailboxId)
        && _mailboxes.Any(m => m.Id.Equals(rule.MailboxId, StringComparison.OrdinalIgnoreCase)))
      return rule.MailboxId;
    var hits = _mailboxes
      .Where(box => MailRuleEngine.ExactFolder(rule.Folder, FoldersFor(box.Id)) is not null)
      .Select(box => box.Id)
      .ToList();
    if (hits.Count == 1)
      return hits[0];
    if (_mailboxes.Count == 1)
      return _mailboxes[0].Id;
    if (!string.IsNullOrWhiteSpace(_preferredMailboxId)
        && _mailboxes.Any(m => m.Id.Equals(_preferredMailboxId, StringComparison.OrdinalIgnoreCase)))
      return _preferredMailboxId;
    return "";
  }

  private string GuessFolderMailbox(MailRule rule) {
    if (!string.IsNullOrWhiteSpace(rule.FolderMailboxId)
        && _mailboxes.Any(m => m.Id.Equals(rule.FolderMailboxId, StringComparison.OrdinalIgnoreCase)))
      return rule.FolderMailboxId;
    if (MailRuleEngine.ExactFolder(rule.Folder, FoldersFor(rule.MailboxId)) is not null)
      return rule.MailboxId;
    var hits = _mailboxes
      .Where(box => MailRuleEngine.ExactFolder(rule.Folder, FoldersFor(box.Id)) is not null)
      .Select(box => box.Id)
      .ToList();
    return hits.Count == 1 ? hits[0] : rule.MailboxId;
  }
}
