using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace MaksIT.PostClient.UI.ViewModels;


public partial class RecipientFieldViewModel : ObservableObject {
  [ObservableProperty]
  private string draft = "";

  public ObservableCollection<MailRecipient> Tags { get; } = [];

  public void AddFromText(string? text) {
    Draft = text ?? "";
    CommitDraft();
  }

  public void CommitDraft() {
    var parsed = RecipientParser.Parse(Draft);
    foreach (var tag in parsed)
      AddUnique(tag);
    Draft = "";
  }

  public bool HasValid =>
    Tags.Any(t => t.IsValid);

  public IReadOnlyList<MailRecipient> Snapshot() =>
    Tags.Where(t => t.IsValid).ToList();

  [RelayCommand]
  private void Remove(MailRecipient? tag) {
    if (tag is not null)
      Tags.Remove(tag);
  }

  public void RemoveLast() {
    if (Tags.Count == 0)
      return;
    Tags.RemoveAt(Tags.Count - 1);
  }

  private void AddUnique(MailRecipient tag) {
    if (Tags.Any(t => t.Address.Equals(tag.Address, StringComparison.OrdinalIgnoreCase)))
      return;
    Tags.Add(tag);
  }
}
