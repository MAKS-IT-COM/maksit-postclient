namespace MaksIT.PostClient.Tests.App;


public class ScreenshotTourOptionsTests {
  [Fact]
  public void Missing_flag_is_not_a_tour() {
    var parsed = ScreenshotTourOptions.TryParse(["--other"], out var options, out var error);

    Assert.True(parsed);
    Assert.Null(options);
    Assert.Null(error);
  }

  [Fact]
  public void Parses_directory_mailbox_language_views_and_settle() {
    var parsed = ScreenshotTourOptions.TryParse(
      [
        "--screenshots", @"D:\shots",
        "--mailbox", "ada@example.com",
        "--language", "it",
        "--views", "inbox, compose",
        "--settle-ms", "250"
      ],
      out var options,
      out var error);

    Assert.True(parsed);
    Assert.Null(error);
    Assert.NotNull(options);
    Assert.Equal(@"D:\shots", options.Directory);
    Assert.Equal("ada@example.com", options.Mailbox);
    Assert.Equal(UiLanguage.It, options.Language);
    Assert.Equal(["inbox", "compose"], options.Views);
    Assert.False(options.AllFeatures);
    Assert.Equal(250, options.SettleMilliseconds);
  }

  [Fact]
  public void Defaults_to_the_inbox_and_every_tool_window() {
    var parsed = ScreenshotTourOptions.TryParse(["--screenshots", "out"], out var options, out var error);

    Assert.True(parsed);
    Assert.Null(error);
    Assert.NotNull(options);
    Assert.Null(options.Mailbox);
    Assert.Equal(UiLanguage.En, options.Language);
    Assert.Equal([ScreenshotTourOptions.InboxView], options.Views);
    Assert.True(options.AllFeatures);
    Assert.Equal(600, options.SettleMilliseconds);
    Assert.Contains("message", ScreenshotTourOptions.ToolViews);
    Assert.Contains("accounts", ScreenshotTourOptions.ToolViews);
    Assert.Contains("logs", ScreenshotTourOptions.ToolViews);
  }

  [Theory]
  [InlineData("--screenshots")]
  [InlineData("--screenshots", "--mailbox")]
  [InlineData("--screenshots", "out", "--mailbox")]
  [InlineData("--screenshots", "out", "--language")]
  public void Rejects_a_missing_value(params string[] args) {
    var parsed = ScreenshotTourOptions.TryParse(args, out var options, out var error);

    Assert.False(parsed);
    Assert.Null(options);
    Assert.NotNull(error);
  }
}
