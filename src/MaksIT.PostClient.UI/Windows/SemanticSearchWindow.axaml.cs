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
}
