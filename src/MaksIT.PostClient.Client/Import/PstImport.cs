using XstReader;


namespace MaksIT.PostClient.Client.Import;


public static class PstImport {
  public static Task<int> ImportAsync(
    LocalMailImport import,
    string mailboxId,
    string path,
    IMailSession? session,
    bool unwrap,
    CancellationToken cancellationToken,
    IReadOnlyList<MailRule>? rules = null,
    IReadOnlyList<(string Name, string FullName)>? folders = null) =>
    ImportAsync(import, mailboxId, path, session, unwrap, cancellationToken, rules, folders, 0);

  public static async Task<int> ImportBytesAsync(
    LocalMailImport import,
    string mailboxId,
    byte[] bytes,
    string? name,
    IMailSession? session,
    bool unwrap,
    CancellationToken cancellationToken,
    IReadOnlyList<MailRule>? rules = null,
    IReadOnlyList<(string Name, string FullName)>? folders = null) {
    var path = PstFile.TempPath(name);
    try {
      await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
      return await ImportAsync(
        import, mailboxId, path, session, unwrap, cancellationToken, rules, folders)
        .ConfigureAwait(false);
    }
    finally {
      if (File.Exists(path))
        File.Delete(path);
    }
  }

  private static async Task<int> ImportAsync(
    LocalMailImport import,
    string mailboxId,
    string path,
    IMailSession? session,
    bool unwrap,
    CancellationToken cancellationToken,
    IReadOnlyList<MailRule>? rules,
    IReadOnlyList<(string Name, string FullName)>? folders,
    int depth) {
    using var file = new XstFile(path);
    return await WalkAsync(
      import, mailboxId, file.RootFolder, session, unwrap, cancellationToken, rules, folders, depth)
      .ConfigureAwait(false);
  }

  private static async Task<int> WalkAsync(
    LocalMailImport import,
    string mailboxId,
    XstFolder folder,
    IMailSession? session,
    bool unwrap,
    CancellationToken cancellationToken,
    IReadOnlyList<MailRule>? rules,
    IReadOnlyList<(string Name, string FullName)>? folders,
    int depth) {
    var count = 0;
    var name = string.IsNullOrWhiteSpace(folder.DisplayName) ? "Imported" : folder.DisplayName;
    foreach (var message in folder.Messages) {
      cancellationToken.ThrowIfCancellationRequested();
      var eml = PstMime.ToEml(message);
      if (eml.Length > 0
          && await import.IngestAsync(
            mailboxId, name, eml, session, unwrap, cancellationToken, rules, folders)
            .ConfigureAwait(false))
        count++;
      if (depth >= 3)
        continue;
      foreach (var attachment in message.Attachments) {
        if (!attachment.IsFile || !PstFile.IsName(attachment.FileName))
          continue;
        var nested = PstFile.TempPath(attachment.FileName);
        try {
          attachment.SaveToFile(nested);
          count += await ImportAsync(
            import, mailboxId, nested, session, unwrap, cancellationToken, rules, folders, depth + 1)
            .ConfigureAwait(false);
        }
        catch {
        }
        finally {
          if (File.Exists(nested))
            File.Delete(nested);
        }
      }
    }

    foreach (var child in folder.Folders) {
      count += await WalkAsync(
        import, mailboxId, child, session, unwrap, cancellationToken, rules, folders, depth)
        .ConfigureAwait(false);
    }

    return count;
  }

}
