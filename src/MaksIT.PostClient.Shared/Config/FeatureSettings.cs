namespace MaksIT.PostClient.Shared.Config;


public sealed class FeatureSettings {
  public Dictionary<string, bool> Enabled { get; set; } = new(StringComparer.OrdinalIgnoreCase);

  public bool IsEnabled(string id) {
    if (Enabled is not null && Enabled.TryGetValue(id, out var value))
      return value;
    return FeatureCatalog.Find(id)?.DefaultOn ?? false;
  }

  public bool Any(params string[] ids) =>
    ids.Any(IsEnabled);

  public void Set(string id, bool on) {
    Enabled ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    Enabled[id] = on;
  }

  public void SetRegion(string region, bool on) {
    foreach (var feature in FeatureCatalog.ForRegion(region))
      Set(feature.Id, on);
  }

  public bool RegionEnabled(string region) {
    var items = FeatureCatalog.ForRegion(region).ToList();
    return items.Count > 0 && items.All(f => IsEnabled(f.Id));
  }

  public void Normalize() {
    var source = Enabled ?? [];
    Enabled = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    foreach (var pair in source)
      Enabled[pair.Key] = pair.Value;
  }
}


public static class FeatureGate {
  public static FeatureSettings Current { get; private set; } = new();

  public static void Use(FeatureSettings settings) =>
    Current = settings ?? new();

  public static bool On(string id) =>
    Current.IsEnabled(id);
}
