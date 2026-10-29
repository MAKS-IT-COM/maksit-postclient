namespace MaksIT.PostClient.Shared.App;

public static class ReleaseNotes {
  public const string ResourceName = "MaksIT.PostClient.Shared.WHATSNEW.md";

  public static string Text() {
    using var stream = typeof(ReleaseNotes).Assembly.GetManifestResourceStream(ResourceName);

    if (stream is null)
      return "";

    using var reader = new StreamReader(stream);

    return reader.ReadToEnd();
  }
}
