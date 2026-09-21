using Avalonia.Controls;
using Avalonia.Interactivity;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI.Windows;


public partial class ZipSendWindow : Window {
  public ZipSendWindow() {
    InitializeComponent();
  }

  public static async Task<ZipSendRequest?> ShowAsync(Window owner, ZipSendRequest request) {
    var copy = UiLocale.Copy;
    var window = new ZipSendWindow {
      Title = copy.ZipTitle,
      DataContext = request
    };
    window.HintText.Text = copy.ZipHint;
    window.NameLabel.Text = copy.ZipFileName;
    window.PasswordLabel.Text = copy.ZipPassword;
    window.OkButton.Content = copy.Ok;
    window.CancelButton.Content = copy.Close;
    window.NameBox.Focus();
    await window.ShowDialog(owner);
    if (!window._accepted)
      return null;
    request.FileName = window.NameBox.Text ?? request.FileName;
    request.Password = window.PasswordBox.Text ?? "";
    return request;
  }

  private bool _accepted;

  private void OnOkClick(object? sender, RoutedEventArgs e) {
    _accepted = true;
    Close();
  }

  private void OnCancelClick(object? sender, RoutedEventArgs e) {
    _accepted = false;
    Close();
  }
}
