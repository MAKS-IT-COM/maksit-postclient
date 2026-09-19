using CommunityToolkit.Mvvm.ComponentModel;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.UI.ViewModels;


public sealed partial class ErrorReportViewModel : ObservableObject {
  public ErrorReportViewModel(string report) {
    Report = report ?? "";
  }

  public UiCopy Copy =>
    UiLocale.Copy;

  public string Report { get; }

  [ObservableProperty]
  private string copyStatus = "";

  public void MarkCopied() =>
    CopyStatus = Copy.Copied;
}
