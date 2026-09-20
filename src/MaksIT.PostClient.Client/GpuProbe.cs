using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public readonly record struct GpuAdapter(
  int DeviceId,
  string Name,
  uint VendorId,
  long DedicatedBytes,
  bool Software);


public static class GpuProbe {
  private const uint Nvidia = 0x10DE;
  private const uint Amd = 0x1002;
  private const uint Intel = 0x8086;
  private const uint Microsoft = 0x1414;
  private const uint DxgiAdapterFlagSoftware = 2;

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

  public static IReadOnlyList<GpuAdapter> DirectMlAdapters() {
    if (!OperatingSystem.IsWindows())
      return [];

    try {
      var preferred = PreferForDirectMl(ListFromDxgi());
      if (preferred.Count > 0)
        return preferred;
    }
    catch {
    }

    return [
      new GpuAdapter(0, "DXGI 0", 0, 0, false),
      new GpuAdapter(1, "DXGI 1", 0, 0, false),
      new GpuAdapter(2, "DXGI 2", 0, 0, false),
      new GpuAdapter(3, "DXGI 3", 0, 0, false)
    ];
  }

  public static IReadOnlyList<GpuAdapter> PreferForDirectMl(IEnumerable<GpuAdapter> adapters) {
    ArgumentNullException.ThrowIfNull(adapters);
    return adapters
      .Where(static adapter => !adapter.Software && !IsSoftwareAdapter(adapter.Name, 0, adapter.VendorId))
      .OrderBy(Rank)
      .ThenByDescending(static adapter => adapter.DedicatedBytes)
      .ThenBy(static adapter => adapter.DeviceId)
      .DistinctBy(static adapter => (adapter.Name, adapter.VendorId, adapter.DedicatedBytes))
      .ToArray();
  }

  public static bool IsSoftwareAdapter(string? name, uint flags, uint vendorId) {
    if ((flags & DxgiAdapterFlagSoftware) != 0)
      return true;
    if (vendorId == Microsoft)
      return true;
    if (string.IsNullOrWhiteSpace(name))
      return false;
    return name.Contains("Basic Render", StringComparison.OrdinalIgnoreCase)
      || name.Contains("Remote Display Adapter", StringComparison.OrdinalIgnoreCase);
  }

  private static int Rank(GpuAdapter adapter) {
    if (adapter.VendorId == Nvidia)
      return 0;
    if (adapter.VendorId == Amd)
      return 1;
    if (adapter.VendorId == Intel)
      return 2;
    return 3;
  }

  [SupportedOSPlatform("windows")]
  private static List<GpuAdapter> ListFromDxgi() {
    var iid = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
    var hr = Native.CreateDXGIFactory1(ref iid, out var factory);
    if (hr < 0 || factory == IntPtr.Zero)
      throw new InvalidOperationException($"DXGI factory failed ({hr:X8}).");

    try {
      var enumAdapters = Native.Vtbl<Native.EnumAdapters1>(factory, 12);
      var adapters = new List<GpuAdapter>();
      for (uint index = 0; index < 16; index++) {
        hr = enumAdapters(factory, index, out var adapter);
        if (hr < 0 || adapter == IntPtr.Zero)
          break;

        try {
          var getDesc = Native.Vtbl<Native.GetDesc1>(adapter, 10);
          var descPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Native.AdapterDesc1>());
          try {
            hr = getDesc(adapter, descPtr);
            if (hr < 0)
              continue;
            var desc = Marshal.PtrToStructure<Native.AdapterDesc1>(descPtr);
            var software = IsSoftwareAdapter(desc.Description, desc.Flags, desc.VendorId);
            adapters.Add(
              new GpuAdapter(
                (int)index,
                string.IsNullOrWhiteSpace(desc.Description) ? "DXGI " + index : desc.Description.Trim(),
                desc.VendorId,
                desc.DedicatedVideoMemory.ToInt64(),
                software));
          }
          finally {
            Marshal.FreeHGlobal(descPtr);
          }
        }
        finally {
          Native.Vtbl<Native.Release>(adapter, 2)(adapter);
        }
      }

      return adapters;
    }
    finally {
      Native.Vtbl<Native.Release>(factory, 2)(factory);
    }
  }

  private static class Native {
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int EnumAdapters1(IntPtr factory, uint index, out IntPtr adapter);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int GetDesc1(IntPtr adapter, IntPtr desc);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate uint Release(IntPtr unknown);

    [DllImport("dxgi.dll", ExactSpelling = true)]
    public static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr factory);

    public static T Vtbl<T>(IntPtr com, int index) where T : Delegate {
      var table = Marshal.ReadIntPtr(com);
      return Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(table, index * IntPtr.Size));
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct AdapterDesc1 {
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
      public string Description;
      public uint VendorId;
      public uint DeviceId;
      public uint SubSysId;
      public uint Revision;
      public IntPtr DedicatedVideoMemory;
      public IntPtr DedicatedSystemMemory;
      public IntPtr SharedSystemMemory;
      public uint LuidLow;
      public int LuidHigh;
      public uint Flags;
    }
  }
}
