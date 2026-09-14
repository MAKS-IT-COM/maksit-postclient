using Avalonia.Controls;
using Avalonia.Interactivity;


namespace MaksIT.PostClient.UI.Windows;


public partial class FeatureSettingsWindow : Window {
  public FeatureSettingsWindow() {
    InitializeComponent();
  }

  private void OnCloseClick(object? sender, RoutedEventArgs e) =>
    Close();
}
