using System.Text.Json;
using System.Text.Json.Nodes;


namespace MaksIT.PostClient.Shared;


public sealed class ConfigurationFileService {
  public const string SeedFileName = "appsettings.json";

  private static readonly JsonSerializerOptions SerializerOptions = new() {
    WriteIndented = true,
    PropertyNamingPolicy = null,
    PropertyNameCaseInsensitive = true
  };

  private readonly string? _seedPath;
  private Configuration _current;

  public string FilePath { get; }

  public Configuration Current => _current;

  public ConfigurationFileService(string? configurationPath = null, string? seedPath = null) {
    FilePath = string.IsNullOrWhiteSpace(configurationPath)
      ? AppPaths.SettingsFile()
      : configurationPath;
    _seedPath = !string.IsNullOrWhiteSpace(seedPath)
      ? seedPath
      : Path.Combine(AppContext.BaseDirectory, SeedFileName);
    _current = LoadFromDisk();
    _current.EnsureDefaults();
    CopySeedIfNeeded();
  }

  public Configuration Reload() {
    _current = LoadFromDisk();
    _current.EnsureDefaults();
    return _current;
  }

  public void Save(Configuration configuration) {
    ArgumentNullException.ThrowIfNull(configuration);
    configuration.EnsureDefaults();
    var dir = Path.GetDirectoryName(FilePath);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);

    var root = ReadRoot(File.Exists(FilePath) ? FilePath : null) ?? [];
    root["Configuration"] = JsonSerializer.SerializeToNode(configuration, SerializerOptions);
    File.WriteAllText(FilePath, root.ToJsonString(SerializerOptions));
    _current = configuration;
  }

  private Configuration LoadFromDisk() {
    var path = File.Exists(FilePath) ? FilePath : _seedPath;
    if (path is null || !File.Exists(path))
      return new Configuration();

    using var document = JsonDocument.Parse(File.ReadAllText(path));
    if (!document.RootElement.TryGetProperty("Configuration", out var value))
      return new Configuration();

    return JsonSerializer.Deserialize<Configuration>(value.GetRawText(), SerializerOptions)
      ?? new Configuration();
  }

  private static JsonObject? ReadRoot(string? path) {
    if (path is null || !File.Exists(path))
      return null;
    return JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
  }

  private void CopySeedIfNeeded() {
    if (File.Exists(FilePath) || _seedPath is null || !File.Exists(_seedPath))
      return;
    Save(_current);
  }
}
