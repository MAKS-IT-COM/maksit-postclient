using MimeKit;
using MaksIT.PostClient.Shared;
using MaksIT.Results;


namespace MaksIT.PostClient.Client;


public sealed class LocalMailImport {
  private readonly MailArchiveStore _archive;

  public LocalMailImport(MailArchiveStore archive) {
    _archive = archive;
  }

  public async Task<int> ImportEmlFilesAsync(
    string mailboxId,
    string folder,
    IEnumerable<string> paths,
    IMailSession? session,
    bool unwrap,
    CancellationToken cancellationToken,
    IReadOnlyList<MailRule>? rules = null,
    IReadOnlyList<(string Name, string FullName)>? folders = null) {
    var count = 0;
    foreach (var path in paths) {
      cancellationToken.ThrowIfCancellationRequested();
      if (!File.Exists(path))
        continue;
      var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
      if (await IngestAsync(mailboxId, folder, bytes, session, unwrap, cancellationToken, rules, folders).ConfigureAwait(false))
        count++;
    }

    return count;
  }

  public async Task<int> ImportMboxAsync(
    string mailboxId,
    string folder,
    string path,
    IMailSession? session,
    bool unwrap,
    CancellationToken cancellationToken,
    IReadOnlyList<MailRule>? rules = null,
    IReadOnlyList<(string Name, string FullName)>? folders = null) {
    var count = 0;
    foreach (var bytes in MboxReader.Messages(path)) {
      cancellationToken.ThrowIfCancellationRequested();
      if (await IngestAsync(mailboxId, folder, bytes, session, unwrap, cancellationToken, rules, folders).ConfigureAwait(false))
        count++;
    }

    return count;
  }

  public async Task<int> ImportThunderbirdAsync(
    string mailboxId,
    IMailSession? session,
    bool unwrap,
    CancellationToken cancellationToken,
    IReadOnlyList<MailRule>? rules = null,
    IReadOnlyList<(string Name, string FullName)>? folders = null) {
    var count = 0;
    foreach (var store in ThunderbirdProfiles.MailStores()) {
      foreach (var file in ThunderbirdProfiles.MboxFiles(store)) {
        var folder = Path.GetFileName(file);
        if (string.IsNullOrWhiteSpace(folder))
          folder = "Imported";
        count += await ImportMboxAsync(
          mailboxId, folder, file, session, unwrap, cancellationToken, rules, folders)
          .ConfigureAwait(false);
      }
    }

    return count;
  }

  public async Task<bool> IngestAsync(
    string mailboxId,
    string folder,
    byte[] eml,
    IMailSession? session,
    bool unwrap,
    CancellationToken cancellationToken,
    IReadOnlyList<MailRule>? rules = null,
    IReadOnlyList<(string Name, string FullName)>? folders = null) {
    if (eml.Length == 0)
      return false;
    await using var stream = new MemoryStream(eml, writable: false);
    var mime = await MimeMessage.LoadAsync(stream, cancellationToken).ConfigureAwait(false);
    var seen = true;
    var flagged = false;
    string? label = null;
    folder = ApplyRules(mime, mailboxId, folder, rules, folders, ref seen, ref flagged, ref label);
    uint uid;
    if (session is { IsConnected: true }) {
      var appended = await session.AppendAsync(folder, eml, cancellationToken).ConfigureAwait(false);
      uid = appended.IsSuccess && appended.Value is uint remote
        ? remote
        : _archive.NextUid(mailboxId, folder);
    }
    else {
      uid = _archive.NextUid(mailboxId, folder);
    }

    var body = await MimeBody.FromMimeAsync(folder, uid, mime, cancellationToken, seen: seen, flagged: flagged)
      .ConfigureAwait(false);
    var path = ArchiveFiles.EmlPath(mailboxId, folder, uid);
    await File.WriteAllBytesAsync(path, eml, cancellationToken).ConfigureAwait(false);
    _archive.UpsertBody(
      mailboxId,
      MailArchiveMap.FromHeader(body.Header),
      path,
      MailArchiveMap.BodyText(body, unwrap),
      MailArchiveMap.AttachmentIndex(body, unwrap));
    if (!string.IsNullOrWhiteSpace(label))
      _archive.AddLabel(mailboxId, folder, uid, label);
    return true;
  }

  private static string ApplyRules(
    MimeMessage mime,
    string mailboxId,
    string folder,
    IReadOnlyList<MailRule>? rules,
    IReadOnlyList<(string Name, string FullName)>? folders,
    ref bool seen,
    ref bool flagged,
    ref string? label) {
    var from = mime.From?.ToString() ?? "";
    var to = mime.To?.ToString() ?? "";
    var subject = mime.Subject ?? "";
    var text = mime.TextBody ?? mime.HtmlBody ?? "";
    var hasAttachment = mime.Attachments.Any();
    var catalog = folders ?? [];
    foreach (var rule in MailRuleEngine.Ready(rules)) {
      if (!MailRuleEngine.CanApply(rule, mailboxId, catalog))
        continue;
      if (!MailRuleEngine.Matches(rule, from, to, subject, text, hasAttachment))
        continue;
      if (rule.Action == MailRuleAction.Delete)
        folder = MailRuleEngine.ResolveFolder("Trash", catalog) ?? "Trash";
      else if (rule.Action == MailRuleAction.Move)
        folder = MailRuleEngine.ExactFolder(rule.Folder, catalog) ?? folder;
      if (rule.Action == MailRuleAction.MarkRead)
        seen = true;
      if (rule.Action == MailRuleAction.Flag)
        flagged = true;
      if (rule.Action == MailRuleAction.Label && !string.IsNullOrWhiteSpace(rule.Label))
        label = rule.Label;
      if (rule.Stop)
        break;
    }

    return folder;
  }
}
