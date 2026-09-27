using Avalonia.Controls;
using Avalonia.Interactivity;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI.Windows;


public partial class SemanticSearchWindow : Window {
  public SemanticSearchWindow() {
    InitializeComponent();
    Closed += (_, _) => {
      if (DataContext is SemanticSearchViewModel vm)
        vm.Detach();
    };
  }

  private void OnCloseClick(object? sender, RoutedEventArgs e) =>
    Close();

  private async void OnSpamMarksClick(object? sender, RoutedEventArgs e) {
    if (DataContext is not SemanticSearchViewModel vm)
      return;
    vm.ReloadExamples();
    var window = new SpamMarksWindow { DataContext = vm };
    await window.ShowDialog(this);
  }
}
