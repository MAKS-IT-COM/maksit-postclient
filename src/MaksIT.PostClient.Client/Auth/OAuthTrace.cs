

namespace MaksIT.PostClient.Client.Auth;


internal static class OAuthTrace {
  public static void Write(string line) {
    try {
      AppPaths.EnsureDirectories();
      File.AppendAllText(
        Path.Combine(AppPaths.LogsDirectory(), "oauth.log"),
        DateTimeOffset.Now.ToString("o") + " " + line + Environment.NewLine);
    }
    catch {
    }
  }
}
