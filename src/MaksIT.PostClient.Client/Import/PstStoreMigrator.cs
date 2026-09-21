

namespace MaksIT.PostClient.Client.Import;


public static class PstStoreMigrator {
  public static int Migrate(ConfigurationFileService files, MailArchiveCatalog archive) {
    ArgumentNullException.ThrowIfNull(files);
    ArgumentNullException.ThrowIfNull(archive);
    var configuration = files.Current;
    configuration.EnsureDefaults();
    var count = 0;
    foreach (var box in configuration.Mailboxes.Where(m => m.IsPstStore).ToList()) {
      var stem = Path.GetFileNameWithoutExtension(box.DataFile);
      if (string.IsNullOrWhiteSpace(stem))
        stem = box.Label;
      var dest = AppPaths.ProposedStoreDirectory(stem);
      Directory.CreateDirectory(dest);
      LocalStoreSidecar.Write(dest, box.Id, box.Label);
      MailArchiveLayout.EnsureSystemFolders(dest);
      ExportFromArchive(archive, box.Id, dest);
      if (File.Exists(box.DataFile))
        ExportFromPst(box, dest);
      box.StorePath = dest;
      box.IncomingProtocol = MailProtocol.Store;
      box.Provider = MailProvider.Store;
      archive.Open(box.Id, MailArchiveLayout.DatabasePath(box, configuration.Mailboxes));
      count++;
    }

    if (count > 0)
      files.Save(configuration);
    return count;
  }

  private static void ExportFromArchive(MailArchiveCatalog archive, string mailboxId, string dest) {
    try {
      foreach (var folder in archive.ListFolders(mailboxId)) {
        foreach (var header in archive.ListFolder(mailboxId, folder)) {
          var source = archive.EmlPath(mailboxId, folder, header.Uid);
          if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
            continue;
          var path = ArchiveFiles.StoreEmlPath(dest, folder, header.Uid);
          Directory.CreateDirectory(Path.GetDirectoryName(path)!);
          File.Copy(source, path, overwrite: true);
        }
      }
    }
    catch {
    }
  }

  private static void ExportFromPst(MailboxAccount box, string dest) {
    try {
      var session = new PstMailSession();
      var connected = session.ConnectAsync(box, "", CancellationToken.None).GetAwaiter().GetResult();
      if (!connected.IsSuccess)
        return;
      var folders = session.ListFoldersAsync().GetAwaiter().GetResult();
      if (!folders.IsSuccess || folders.Value is null)
        return;
      foreach (var folder in folders.Value) {
        var listed = session.ListMessagesAsync(folder.FullName).GetAwaiter().GetResult();
        if (!listed.IsSuccess || listed.Value is null)
          continue;
        foreach (var header in listed.Value.Headers) {
          var body = session.GetMessageAsync(folder.FullName, header.Id).GetAwaiter().GetResult();
          if (!body.IsSuccess || body.Value is null || body.Value.RawEml.Length == 0)
            continue;
          var path = ArchiveFiles.StoreEmlPath(dest, folder.FullName, header.Id);
          Directory.CreateDirectory(Path.GetDirectoryName(path)!);
          if (!File.Exists(path))
            File.WriteAllBytes(path, body.Value.RawEml);
        }
      }
    }
    catch {
    }
  }
}
