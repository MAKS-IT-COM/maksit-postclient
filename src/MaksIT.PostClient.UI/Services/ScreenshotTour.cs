using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MaksIT.Core.UI.Snapshots;
using MaksIT.PostClient.UI.Windows;
using MaksIT.PostClient.UI.ViewModels;
using MaksIT.PostClient.UI.Views.Mail;


namespace MaksIT.PostClient.UI.Services;


internal static class ScreenshotTour {
  private static readonly JsonSerializerOptions Json = new() {
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
  };

  public static async Task RunAsync(
    Window window,
    MainViewModel model,
    ConfigurationFileService files,
    ScreenshotTourOptions options,
    Action<int> shutdown) {
    var manifest = new Manifest();
    var code = 0;

    try {
      Directory.CreateDirectory(options.Directory);

      await WaitUntilIdleAsync(model);

      foreach (var view in options.Views)
        await CaptureViewAsync(window, model, files, options, manifest, view);

      if (options.AllFeatures) {
        foreach (var view in ScreenshotTourOptions.ToolViews)
          await CaptureViewAsync(window, model, files, options, manifest, view);
      }
    }
    catch (Exception ex) {
      manifest.Error ??= ex.Message;
      code = 1;
    }

    try {
      var path = Path.Combine(options.Directory, "manifest.json");
      await File.WriteAllTextAsync(path, JsonSerializer.Serialize(manifest, Json) + Environment.NewLine);
    }
    catch (Exception ex) {
      manifest.Error ??= ex.Message;
      code = 1;
    }

    shutdown(code);
  }

  private static async Task CaptureViewAsync(
    Window window,
    MainViewModel model,
    ConfigurationFileService files,
    ScreenshotTourOptions options,
    Manifest manifest,
    string view) {
    switch (view.Trim().ToLowerInvariant()) {
      case "inbox":
        await CaptureInboxAsync(window, model, options, manifest);
        break;
      case "compose":
        await CaptureToolAsync(window, model, options, manifest, view, () => SampleCompose(model));
        break;
      case "message":
        await CaptureMessageAsync(window, model, options, manifest);
        break;
      case "accounts":
        await CaptureToolAsync(window, model, options, manifest, view, () =>
          new AccountSettingsWindow { DataContext = model });
        break;
      case "features":
        await CaptureToolAsync(
          window,
          model,
          options,
          manifest,
          view,
          () => new FeatureSettingsWindow { DataContext = new FeatureMatrixViewModel(files, model.NotifyFeatures) });
        break;
      case "rules":
        await CaptureToolAsync(window, model, options, manifest, view, () => CreateRules(model));
        break;
      case "search":
        await CaptureToolAsync(window, model, options, manifest, view, () => CreateSearch(model, files));
        break;
      case "retention":
        await CaptureToolAsync(window, model, options, manifest, view, () => CreateRetention(model));
        break;
      case "spam":
        await CaptureToolAsync(window, model, options, manifest, view, () => CreateSpam(model, files));
        break;
      case "about":
        await CaptureToolAsync(window, model, options, manifest, view, () => AboutWindow.Create(window));
        break;
      case "logs":
        await CaptureToolAsync(window, model, options, manifest, view, () => LogWindow.Create(window, SampleLogDirectory()));
        break;
      default:
        manifest.Skipped.Add(new SkippedRecord { Id = view, Reason = "Unknown view." });
        break;
    }
  }

  private static async Task CaptureInboxAsync(
    Window window,
    MainViewModel model,
    ScreenshotTourOptions options,
    Manifest manifest) {
    await Dispatcher.UIThread.InvokeAsync(() => {
      model.RestoreFolderTreeSelection();
      model.RestoreMessageGridSelection();
    });
    await AppViewSnapshot.SettleAsync(options.SettleMilliseconds);
    manifest.Shots.Add(CaptureWindow(window, options, ScreenshotTourOptions.InboxView));
    SaveFolders(window, options, manifest);
  }

  private static async Task CaptureMessageAsync(
    Window window,
    MainViewModel model,
    ScreenshotTourOptions options,
    Manifest manifest) {
    var dialog = await Dispatcher.UIThread.InvokeAsync(() => SampleMessage(model));
    if (dialog is null) {
      manifest.Skipped.Add(new SkippedRecord { Id = "message", Reason = "Sample message is not ready." });

      return;
    }

    await CaptureToolAsync(window, model, options, manifest, "message", () => dialog);
  }

  private static Window SampleCompose(MainViewModel model) {
    var identities = Identities(model);
    var compose = new ComposeViewModel(identities, identities[0], model.Copy.Write);
    compose.ToField.AddFromText("Grace Hopper <grace@example.com>");
    compose.CcField.AddFromText("City Library <desk@library.example>");
    compose.Subject = "Workshop notes for Friday";
    compose.Body = "Hello Grace,\n\nRoom 12 is booked. I will bring the draft notes.\n\nAda";

    return new ComposeWindow(compose) {
      WindowStartupLocation = WindowStartupLocation.CenterOwner
    };
  }

  private static Window? SampleMessage(MainViewModel model) {
    if (model.ScreenshotBody is not { } body || model.Mailboxes.Count == 0)
      return null;

    var identities = Identities(model);

    return new MessageWindow(new MessageWindowViewModel(
      body,
      identities,
      identities[0],
      unwrap: false,
      preference: MailBodyKind.Text,
      htmlReady: false)) {
      WindowStartupLocation = WindowStartupLocation.CenterOwner
    };
  }

  private static List<ComposeIdentity> Identities(MainViewModel model) =>
    model.Mailboxes.Select(box => new ComposeIdentity { Account = box }).ToList();

  private static RulesWindow CreateRules(MainViewModel model) =>
    new() {
      DataContext = new RulesViewModel(
        SampleFiles(model),
        model.Mailboxes,
        model.FoldersForRules,
        _ => Task.CompletedTask,
        model.SelectedMailbox?.Id,
        () => Task.FromResult(""))
    };

  private static SemanticSearchWindow CreateSearch(MainViewModel model, ConfigurationFileService files) =>
    new() { DataContext = SearchModel(model, files) };

  private static SpamMarksWindow CreateSpam(MainViewModel model, ConfigurationFileService files) =>
    new() { DataContext = SpamModel(model, files) };

  private static RetentionWindow CreateRetention(MainViewModel model) =>
    new() {
      DataContext = new RetentionViewModel(
        SampleFiles(model),
        model.Mailboxes,
        model.FoldersForRules,
        () => Task.CompletedTask)
    };

  private static SemanticSearchViewModel SearchModel(MainViewModel model, ConfigurationFileService files) =>
    new(
      files,
      model.SemanticSearch,
      model.SpamExamples,
      model.DeleteSpamExample,
      model.NotSpamExampleAsync);

  private static SemanticSearchViewModel SpamModel(MainViewModel model, ConfigurationFileService files) =>
    new(
      files,
      model.SemanticSearch,
      SampleSpam,
      static _ => { },
      static _ => Task.CompletedTask);

  private static IReadOnlyList<SpamListItem> SampleSpam() {
    var when = DateTimeOffset.Now.AddDays(-1);

    return [
      new SpamListItem {
        MailboxId = "sample-ada",
        Fingerprint = "sample-prize",
        From = "deals@example.net",
        Subject = "You have won a prize",
        When = MailWhen.Line(when),
        SortDate = when
      },
      new SpamListItem {
        MailboxId = "sample-ada",
        Fingerprint = "sample-signin",
        From = "alerts@example.org",
        Subject = "Unusual sign-in from a new device",
        When = MailWhen.Line(when.AddDays(-2)),
        SortDate = when.AddDays(-2),
        Gone = true
      }
    ];
  }

  private static ConfigurationFileService SampleFiles(MainViewModel model) {
    var dir = Path.Combine(Path.GetTempPath(), "postclient-screenshot-sample");
    Directory.CreateDirectory(dir);
    var files = new ConfigurationFileService(Path.Combine(dir, "settings.json"));
    var boxId = model.SelectedMailbox?.Id ?? model.Mailboxes.FirstOrDefault()?.Id ?? "";
    var configuration = files.Current;
    configuration.Language = UiLocale.Id;
    configuration.Rules = [
      new MailRule {
        Name = "Newsletters",
        Enabled = true,
        Sequence = 1,
        Action = MailRuleAction.Move,
        MailboxId = boxId,
        Folder = "Newsletters",
        Conditions = [new MailRuleCondition { Field = "from", Op = "contains", Value = "news@example.com" }]
      },
      new MailRule {
        Name = "Receipts",
        Enabled = true,
        Sequence = 2,
        Action = MailRuleAction.Label,
        MailboxId = boxId,
        Label = "Receipt",
        Conditions = [new MailRuleCondition { Field = "subject", Op = "contains", Value = "invoice" }]
      }
    ];
    configuration.Retention = [
      new FolderRetention { MailboxId = boxId, Folder = "Trash", Days = 30 },
      new FolderRetention { MailboxId = boxId, Folder = "Newsletters", Days = 90 }
    ];
    files.Save(configuration);

    return files;
  }

  private static string SampleLogDirectory() {
    var dir = Path.Combine(Path.GetTempPath(), "postclient-screenshot-sample", "logs");
    Directory.CreateDirectory(dir);
    var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    File.WriteAllText(
      Path.Combine(dir, "app.log"),
      stamp + " info  Postclient started.\n"
      + stamp + " info  Sample mailbox ready. No server was contacted.\n"
      + stamp + " info  Folder Inbox listed 5 messages.\n");

    return dir;
  }

  private static async Task CaptureToolAsync(
    Window owner,
    MainViewModel model,
    ScreenshotTourOptions options,
    Manifest manifest,
    string id,
    Func<Window> create) {
    Window dialog = null!;
    await Dispatcher.UIThread.InvokeAsync(() => {
      dialog = create();
      dialog.Show(owner);
    });
    await AppViewSnapshot.SettleAsync(options.SettleMilliseconds);
    manifest.Shots.Add(CaptureWindow(dialog, options, id));
    await Dispatcher.UIThread.InvokeAsync(dialog.Close);
  }

  private static async Task WaitUntilIdleAsync(MainViewModel model) {
    await Task.Delay(400);
    var start = Environment.TickCount64;

    while (Environment.TickCount64 - start < 20_000) {
      var busy = await Dispatcher.UIThread.InvokeAsync(() => model.IsBusy);

      if (!busy)
        break;

      await Task.Delay(100);
    }
  }

  private static void SaveFolders(Window window, ScreenshotTourOptions options, Manifest manifest) {
    if (manifest.Folders is not null)
      return;

    var pane = window.GetVisualDescendants().OfType<FolderPaneView>().FirstOrDefault();
    manifest.Folders = AppViewSnapshot.Save(pane, Path.Combine(options.Directory, "folders.png"));
  }

  private static ShotRecord CaptureWindow(Window window, ScreenshotTourOptions options, string id) =>
    new() {
      Id = id,
      Window = AppViewSnapshot.Save(window, Path.Combine(options.Directory, AppViewSnapshot.FileToken(id) + ".png"))
    };

  private sealed class Manifest {
    public string? Folders { get; set; }

    public List<ShotRecord> Shots { get; set; } = [];

    public List<SkippedRecord> Skipped { get; set; } = [];

    public string? Error { get; set; }
  }

  private sealed class ShotRecord {
    public string Id { get; set; } = "";

    public string? Window { get; set; }
  }

  private sealed class SkippedRecord {
    public string Id { get; set; } = "";

    public string Reason { get; set; } = "";
  }
}
