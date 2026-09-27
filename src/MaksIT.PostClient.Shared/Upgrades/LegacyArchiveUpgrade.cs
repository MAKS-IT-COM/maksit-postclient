namespace MaksIT.PostClient.Shared.Upgrades;


/// <summary>
/// Splits the single profile <c>mail.db</c> file into one archive file per mailbox.
/// <see cref="InstallsOver"/> is the last release that kept every mailbox in that file.
/// Delete this type once installs of that version are no longer supported.
/// </summary>
public static class LegacyArchiveUpgrade {
  public static Version InstallsOver => new(0, 2, 0);

  public static void Apply(IReadOnlyList<MailboxAccount> mailboxes) {
    var legacy = AppPaths.ArchiveDatabase();
    if (!File.Exists(legacy))
      return;
    using var source = new MailArchiveStore(legacy);
    var ids = source.DistinctMailboxIds();
    if (ids.Count == 0)
      return;
    foreach (var id in ids) {
      var box = mailboxes.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
        ?? new MailboxAccount { Id = id };
      var destPath = MailArchiveLayout.DatabasePath(box, mailboxes);
      if (destPath.Equals(legacy, StringComparison.OrdinalIgnoreCase))
        continue;
      using var dest = new MailArchiveStore(destPath);
      source.CopyMailboxInto(id, dest);
    }

    source.Dispose();
    try {
      File.Move(legacy, legacy + ".migrated", overwrite: true);
    }
    catch {
    }
  }
}
