using MaksIT.Core.Extensions;


namespace MaksIT.PostClient.Shared;


public sealed class LocalStoreSidecar {
  public const string FileName = "postclient.store.json";

  public string Id { get; set; } = "";

  public string Name { get; set; } = "";

  public static string PathFor(string storeDirectory) =>
    Path.Combine(storeDirectory, FileName);

  public static LocalStoreSidecar? TryRead(string storeDirectory) {
    var path = PathFor(storeDirectory);
    if (!File.Exists(path))
      return null;
    try {
      var sidecar = File.ReadAllText(path).ToObject<LocalStoreSidecar>();
      if (sidecar is null || string.IsNullOrWhiteSpace(sidecar.Id))
        return null;
      return sidecar;
    }
    catch {
      return null;
    }
  }

  public static LocalStoreSidecar Write(string storeDirectory, string id, string name) {
    Directory.CreateDirectory(storeDirectory);
    var sidecar = new LocalStoreSidecar {
      Id = id,
      Name = name
    };
    File.WriteAllText(PathFor(storeDirectory), sidecar.ToJson());
    return sidecar;
  }
}
