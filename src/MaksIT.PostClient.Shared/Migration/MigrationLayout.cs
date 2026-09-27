namespace MaksIT.PostClient.Shared.Migration;


/// <summary>
/// Folders Easy Migration reads and writes. On Windows config and data are the same directory.
/// </summary>
public sealed class MigrationLayout {
  public required string ConfigDirectory { get; init; }

  public required string DataDirectory { get; init; }

  public required string SharedDirectory { get; init; }

  public static MigrationLayout Local() =>
    new() {
      ConfigDirectory = AppPaths.ConfigDirectory(),
      DataDirectory = AppPaths.DataDirectory(),
      SharedDirectory = SharedMailPaths.Root()
    };
}
