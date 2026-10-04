namespace MaksIT.PostClient.UI.Layout;


/// <summary>
/// Originator in the Memento pattern. It applies and captures its own slice of <see cref="LayoutSettings"/>.
/// The control registers the originator. The caretaker stores the memento and does not know the control.
/// </summary>
internal interface ILayoutOriginator {
  bool DeferSave { get; }

  /// <summary>
  /// When true, <see cref="Apply"/> runs before the other originators so the shell structure exists for them.
  /// </summary>
  bool ApplyFirst => false;

  void Attach(Action changed);

  void Apply(LayoutSettings layout);

  void Capture(LayoutSettings layout);
}
