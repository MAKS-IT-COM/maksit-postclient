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
    if (!request.ConfirmOnly)
      window.ValueBox.Focus();
    await window.ShowDialog(owner);
    return window._accepted ? window._value : null;
  }

  private bool _accepted;
  private string? _value;

  private void OnOkClick(object? sender, RoutedEventArgs e) {
    _accepted = true;
    _value = ValueBox.IsVisible ? ValueBox.Text ?? "" : "";
    Close();
  }

  private void OnCancelClick(object? sender, RoutedEventArgs e) {
    _accepted = false;
    Close();
  }
}
