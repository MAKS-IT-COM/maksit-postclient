using Avalonia.Controls;
using Avalonia.Interactivity;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI.Windows;


public partial class SpamMarksWindow : Window {
  public SpamMarksWindow() {
    InitializeComponent();
  }

  private void OnCloseClick(object? sender, RoutedEventArgs e) =>
    Close();

  private void OnNotSpamClick(object? sender, RoutedEventArgs e) =>
    Run(sender, static (vm, item) => vm.NotSpamExampleCommand.Execute(item));

  private void OnDeleteClick(object? sender, RoutedEventArgs e) =>
    Run(sender, static (vm, item) => vm.DeleteExampleCommand.Execute(item));

  private void Run(object? sender, Action<SemanticSearchViewModel, SpamListItem> action) {
    if (sender is not Button { DataContext: SpamListItem item })
      return;
    if (DataContext is not SemanticSearchViewModel vm)
      return;
    action(vm, item);
  }
}
