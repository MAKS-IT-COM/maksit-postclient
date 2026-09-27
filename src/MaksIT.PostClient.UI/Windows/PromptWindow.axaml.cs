using Avalonia.Controls;
using Avalonia.Interactivity;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI.Windows;


public partial class PromptWindow : Window {
  public PromptWindow() {
    InitializeComponent();
  }

  public static async Task<string?> ShowAsync(Window owner, PromptRequest request, string ok, string cancel) {
    var window = new PromptWindow {
      Title = request.Title
    };
    window.MessageText.Text = request.Message;
    window.OkButton.Content = ok;
    window.CancelButton.Content = cancel;
    window.ValueBox.PlaceholderText = request.Placeholder;
    window.ValueBox.PasswordChar = request.Password ? '●' : '\0';
    window.ValueBox.IsVisible = !request.ConfirmOnly;
    window._repeat = request.RepeatPassword;
    window._requiredMessage = request.RequiredMessage;
    window._mismatchMessage = request.MismatchMessage;
    window.RepeatLabel.Text = request.RepeatMessage;
    window.RepeatLabel.IsVisible = request.RepeatPassword;
    window.RepeatBox.IsVisible = request.RepeatPassword;
    if (!request.ConfirmOnly)
      window.ValueBox.Focus();
    await window.ShowDialog(owner);
    return window._accepted ? window._value : null;
  }

  private bool _accepted;
  private bool _repeat;
  private string? _value;
  private string _requiredMessage = "";
  private string _mismatchMessage = "";

  private void OnOkClick(object? sender, RoutedEventArgs e) {
    var value = ValueBox.IsVisible ? ValueBox.Text ?? "" : "";
    if (_repeat) {
      if (string.IsNullOrWhiteSpace(value)) {
        ShowError(_requiredMessage);
        ValueBox.Focus();
        return;
      }

      if (!string.Equals(value, RepeatBox.Text ?? "", StringComparison.Ordinal)) {
        ShowError(_mismatchMessage);
        RepeatBox.Focus();
        return;
      }
    }

    _accepted = true;
    _value = value;
    Close();
  }

  private void OnValueChanged(object? sender, TextChangedEventArgs e) =>
    ErrorText.IsVisible = false;

  private void ShowError(string message) {
    ErrorText.Text = message;
    ErrorText.IsVisible = !string.IsNullOrWhiteSpace(message);
  }

  private void OnCancelClick(object? sender, RoutedEventArgs e) {
    _accepted = false;
    Close();
  }
}
