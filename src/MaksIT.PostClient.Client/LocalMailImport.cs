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
    CancellationToken cancellationToken) {
    var count = 0;
    foreach (var path in paths) {
      cancellationToken.ThrowIfCancellationRequested();
      if (!File.Exists(path))
        continue;
      var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
      if (await IngestAsync(mailboxId, folder, bytes, session, unwrap, cancellationToken).ConfigureAwait(false))
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
    CancellationToken cancellationToken) {
    var count = 0;
    foreach (var bytes in MboxReader.Messages(path)) {
      cancellationToken.ThrowIfCancellationRequested();
      if (await IngestAsync(mailboxId, folder, bytes, session, unwrap, cancellationToken).ConfigureAwait(false))
        count++;
    }

    return count;
  }

  public async Task<int> ImportThunderbirdAsync(
    string mailboxId,
    IMailSession? session,
    bool unwrap,
    CancellationToken cancellationToken) {
    var count = 0;
    foreach (var store in ThunderbirdProfiles.MailStores()) {
      foreach (var file in ThunderbirdProfiles.MboxFiles(store)) {
        var folder = Path.GetFileName(file);
        if (string.IsNullOrWhiteSpace(folder))
          folder = "Imported";
        count += await ImportMboxAsync(mailboxId, folder, file, session, unwrap, cancellationToken)
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
    CancellationToken cancellationToken) {
    if (eml.Length == 0)
      return false;
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

    await using var stream = new MemoryStream(eml, writable: false);
    var mime = await MimeMessage.LoadAsync(stream, cancellationToken).ConfigureAwait(false);
    var body = await MimeBody.FromMimeAsync(folder, uid, mime, cancellationToken, seen: true)
      .ConfigureAwait(false);
    var path = ArchiveFiles.EmlPath(mailboxId, folder, uid);
    await File.WriteAllBytesAsync(path, eml, cancellationToken).ConfigureAwait(false);
    _archive.UpsertBody(
      mailboxId,
      MailArchiveMap.FromHeader(body.Header),
      path,
      MailArchiveMap.BodyText(body, unwrap),
      MailArchiveMap.AttachmentIndex(body, unwrap));
    return true;
  }
}
