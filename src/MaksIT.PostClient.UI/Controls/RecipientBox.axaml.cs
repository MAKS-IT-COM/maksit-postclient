using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using MaksIT.PostClient.UI.ViewModels;


namespace MaksIT.PostClient.UI.Controls;


public partial class RecipientBox : UserControl {
  public static readonly StyledProperty<string> HeaderProperty =
    AvaloniaProperty.Register<RecipientBox, string>(nameof(Header), "To");

  private RecipientFieldViewModel? _field;
  private TextBox? _draft;

  public string Header {
    get => GetValue(HeaderProperty);
    set => SetValue(HeaderProperty, value);
  }

  public RecipientBox() {
    InitializeComponent();
    DataContextChanged += (_, _) => BindField();
    Loaded += (_, _) => {
      if (this.FindControl<TextBlock>("HeaderBlock") is { } header)
        header.Text = Header;
      BindField();
      _draft?.Focus();
    };
  }

  protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
    base.OnApplyTemplate(e);
    BindField();
    if (this.FindControl<TextBlock>("HeaderBlock") is { } header)
      header.Text = Header;
  }

  protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
    base.OnPropertyChanged(change);
    if (change.Property == HeaderProperty && this.FindControl<TextBlock>("HeaderBlock") is { } header)
      header.Text = Header;
  }

  private void BindField() {
    if (_field is not null)
      _field.Tags.CollectionChanged -= OnTagsChanged;
    _field = DataContext as RecipientFieldViewModel;
    _draft = this.FindControl<TextBox>("DraftBox");
    if (_field is not null)
      _field.Tags.CollectionChanged += OnTagsChanged;
    RebuildChips();
  }

  private void OnTagsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
    RebuildChips();

  private void RebuildChips() {
    var host = this.FindControl<WrapPanel>("Host");
    if (host is null || _draft is null)
      return;
    host.Children.Clear();
    if (_field is not null) {
      foreach (var tag in _field.Tags)
        host.Children.Add(CreateChip(tag));
    }

    host.Children.Add(_draft);
    if (_draft.IsFocused || host.IsKeyboardFocusWithin)
      _draft.Focus();
  }

  private Control CreateChip(MailRecipient tag) {
    var label = new TextBlock {
      Text = tag.Display,
      VerticalAlignment = VerticalAlignment.Center,
      FontSize = 12
    };
    ToolTip.SetTip(label, tag.Tooltip);
    var remove = new Button {
      Content = "×",
      Classes = { "chip-remove" },
      Tag = tag
    };
    remove.Click += OnRemoveClick;
    var row = new StackPanel {
      Orientation = Orientation.Horizontal,
      Spacing = 4,
      Children = { label, remove }
    };
    return new Border {
      Classes = { tag.IsValid ? "chip" : "chip-invalid" },
      Child = row,
      Margin = new Thickness(0, 2, 4, 2),
      Padding = new Thickness(8, 2, 4, 2),
      CornerRadius = new CornerRadius(11),
      Background = tag.IsValid
        ? SolidColorBrush.Parse("#1a4a66")
        : SolidColorBrush.Parse("#5a2a2a")
    };
  }

  private void OnRemoveClick(object? sender, RoutedEventArgs e) {
    if (sender is Button { Tag: MailRecipient tag })
      _field?.RemoveCommand.Execute(tag);
  }

  private void OnDraftKeyDown(object? sender, KeyEventArgs e) {
    if (_field is null)
      return;
    if (e.Key is Key.Enter or Key.Tab) {
      _field.CommitDraft();
      e.Handled = e.Key == Key.Enter;
      return;
    }

    if (e.Key == Key.Back && string.IsNullOrEmpty(_field.Draft)) {
      _field.RemoveLast();
      e.Handled = true;
      return;
    }

    if (e.Key is Key.OemComma or Key.OemSemicolon) {
      _field.CommitDraft();
      e.Handled = true;
    }
  }

  private void OnDraftLostFocus(object? sender, RoutedEventArgs e) =>
    _field?.CommitDraft();

  private void OnDraftTextChanged(object? sender, TextChangedEventArgs e) {
    if (_field is null)
      return;
    var text = _field.Draft ?? "";
    if (text.Count(c => c == '"') % 2 == 1)
      return;
    if (text.EndsWith(',') || text.EndsWith(';'))
      _field.CommitDraft();
  }
}
