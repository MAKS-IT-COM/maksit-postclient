namespace MaksIT.PostClient.Shared.Search;


public sealed class SemanticSearchSettings {
  public bool Enabled { get; set; } = true;

  public string Device { get; set; } = SemanticDevice.Auto;

  public bool SpamFilter { get; set; }

  public void Normalize() =>
    Device = SemanticDevice.Normalize(Device);
}
