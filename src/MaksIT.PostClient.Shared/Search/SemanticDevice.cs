namespace MaksIT.PostClient.Shared.Search;


public static class SemanticDevice {
  public const string Auto = "auto";
  public const string Cpu = "cpu";
  public const string Gpu = "gpu";

  public static string Normalize(string? value) {
    if (string.IsNullOrWhiteSpace(value))
      return Auto;
    return value.Trim().ToLowerInvariant() switch {
      "cpu" or "cpu-only" or "cpu_only" => Cpu,
      "gpu" or "directml" or "dml" or "boost" => Gpu,
      _ => Auto
    };
  }

  public static bool IsGpu(string? value) =>
    Normalize(value) == Gpu;

  public static bool IsCpu(string? value) =>
    Normalize(value) == Cpu;

  public static bool IsAuto(string? value) =>
    Normalize(value) == Auto;
}
