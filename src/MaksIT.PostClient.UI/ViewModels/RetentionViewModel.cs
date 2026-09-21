using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace MaksIT.PostClient.UI.ViewModels;


public sealed class RetentionRowViewModel : ObservableObject {
  public required string MailboxId { get; init; }

  public required string MailboxLabel { get; init; }

  public required string Folder { get; init; }

  public required bool IsTrash { get; init; }

  public string ActionLabel =>
    IsTrash ? UiLocale.Copy.RetentionPurge : UiLocale.Copy.RetentionToTrash;

  private int _days;

  public int Days {
    get => _days;
    set => SetProperty(ref _days, Math.Max(0, value));
  }
}


public sealed partial class RetentionViewModel : ObservableObject {
  private readonly ConfigurationFileService _files;
  private readonly Func<Task> _run;

  public RetentionViewModel(
    ConfigurationFileService files,
    IEnumerable<MailboxAccount> mailboxes,
    Func<string, IReadOnlyList<(string Name, string FullName)>> folders,
    Func<Task> run) {
    _files = files;
    _run = run;
    var saved = files.Current.Retention ?? [];
    foreach (var box in mailboxes) {
      var listed = folders(box.Id);
      var names = listed.Count > 0
        ? listed.Select(f => f.FullName).ToList()
        : MailArchiveLayout.SystemFolders.ToList();
      if (!names.Any(folder => MailRetention.IsTrash(folder)))
        names.Add(MailRetention.TrashFolder);
      foreach (var folder in names.Distinct(StringComparer.OrdinalIgnoreCase)) {
        var rule = saved.FirstOrDefault(r =>
          r.MailboxId.Equals(box.Id, StringComparison.OrdinalIgnoreCase)
          && r.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase));
        Rows.Add(new RetentionRowViewModel {
          MailboxId = box.Id,
          MailboxLabel = box.Label,
          Folder = folder,
          IsTrash = MailRetention.IsTrash(folder),
          Days = rule?.Days ?? 0
        });
      }
    }
  }

  public UiCopy Copy =>
    UiLocale.Copy;

  public ObservableCollection<RetentionRowViewModel> Rows { get; } = [];

  public void Persist() {
    var configuration = _files.Current;
    configuration.Retention = Rows
      .Select(r => new FolderRetention {
        MailboxId = r.MailboxId,
        Folder = r.Folder,
        Days = r.Days
      })
      .ToList();
    _files.Save(configuration);
  }

  [RelayCommand]
  private async Task RunAsync() {
    Persist();
    await _run();
  }
}
