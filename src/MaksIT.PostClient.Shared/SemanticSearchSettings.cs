namespace MaksIT.PostClient.Shared;


public sealed class SemanticSearchSettings {
  public bool Enabled { get; set; } = true;

  public string Device { get; set; } = SemanticDevice.Auto;

  public void Normalize() =>
    Device = SemanticDevice.Normalize(Device);
}
