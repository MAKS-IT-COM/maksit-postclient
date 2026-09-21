using Avalonia.Controls;
using Avalonia.Interactivity;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI.Windows;


public partial class AboutWindow : Window {
  public AboutWindow() {
    InitializeComponent();
    DataContext ??= new AboutViewModel();
  }

  public static Task ShowAsync(Window owner) {
    var copy = UiLocale.Copy;
    var window = new AboutWindow {
      Title = copy.AboutTitle,
      DataContext = new AboutViewModel()
    };
    return window.ShowDialog(owner);
  }

  private void OnCloseClick(object? sender, RoutedEventArgs e) =>
    Close();
}
