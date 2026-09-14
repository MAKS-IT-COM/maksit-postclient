using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public static class GpuProbe {
  public static bool SupportsBoost =>
    OperatingSystem.IsWindows();

  public static bool UseGpu(string device) {
    var kind = SemanticDevice.Normalize(device);
    if (kind == SemanticDevice.Cpu)
      return false;
    if (!SupportsBoost)
      return false;
    return kind is SemanticDevice.Gpu or SemanticDevice.Auto;
  }
}
