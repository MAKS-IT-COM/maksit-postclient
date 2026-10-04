using Avalonia.Input;


namespace MaksIT.PostClient.UI.Views.Mail;


static class MailDrag {
  public static readonly DataFormat<string> Messages =
    DataFormat.CreateInProcessFormat<string>("postclient-message-ids");

  public static readonly DataFormat<string> Folders =
    DataFormat.CreateInProcessFormat<string>("postclient-folder");

  public static bool HasSelectionModifier(KeyModifiers modifiers) =>
    modifiers.HasFlag(KeyModifiers.Control)
    || modifiers.HasFlag(KeyModifiers.Meta)
    || modifiers.HasFlag(KeyModifiers.Shift);
}
