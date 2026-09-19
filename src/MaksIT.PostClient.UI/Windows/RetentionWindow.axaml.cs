using Avalonia.Controls;
using Avalonia.Interactivity;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI.Windows;


public partial class RetentionWindow : Window {
  public RetentionWindow() {
    InitializeComponent();
    Closed += (_, _) => {
      if (DataContext is RetentionViewModel vm)
        vm.Persist();
    };
  }

  private void OnCloseClick(object? sender, RoutedEventArgs e) =>
    Close();
}
