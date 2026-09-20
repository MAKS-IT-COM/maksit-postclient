using MimeKit;
using MaksIT.Results;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public interface IFileMailStore {
  string MessagePath(string folder, uint uid);
}


public sealed class LocalStoreSession : IMailSession, IFileMailStore {
  private readonly Lock _gate = new();
  private readonly MailSessionGate _io = new();
  private string _root = "";
  private bool _connected;

  public string StorePath {
    get {
      lock (_gate)
        return _root;
    }
  }

  public bool IsConnected {
    get {
      lock (_gate)
        return _connected;
    }
  }

  public bool SupportsFolders =>
    true;

  public Task<Result> ConnectAsync(
    MailboxAccount account,
    string password,
    CancellationToken cancellationToken = default) {
    _ = password;
    ArgumentNullException.ThrowIfNull(account);
    var path = account.StorePath;
    if (string.IsNullOrWhiteSpace(path))
      return Task.FromResult(Result.BadRequest("Store folder is required."));
    if (!Directory.Exists(path))
      return Task.FromResult(Result.BadRequest("Store folder not found: " + path));
    if (LocalStoreSidecar.TryRead(path) is null)
      return Task.FromResult(Result.BadRequest("Folder is not a Postclient store (missing postclient.store.json)."));

    return _io.RunAsync(() => {
      cancellationToken.ThrowIfCancellationRequested();
      MailArchiveLayout.EnsureSystemFolders(path);
      lock (_gate) {
        _root = path;
        _connected = true;
      }

      return Task.FromResult(Result.Ok());
    }, cancellationToken);
  }

  public string MessagePath(string folder, uint uid) {
    lock (_gate)
      return ArchiveFiles.StoreEmlPath(_root, folder, uid);
  }

  public Task<Result<IReadOnlyList<MailFolderInfo>>> ListFoldersAsync(CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => {
      cancellationToken.ThrowIfCancellationRequested();
      var root = Root();
      var folders = new List<MailFolderInfo>();
      foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)) {
        var rel = Path.GetRelativePath(root, dir);
        if (rel.Equals("accounts", StringComparison.OrdinalIgnoreCase)
            || rel.StartsWith("accounts" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
          continue;
        if (string.Equals(Path.GetFileName(dir), "mail.db", StringComparison.OrdinalIgnoreCase))
          continue;
        var name = rel.Replace(Path.DirectorySeparatorChar, '/');
        var total = Directory.EnumerateFiles(dir, "*.eml").Count();
        folders.Add(new MailFolderInfo {
          FullName = name,
          Name = Path.GetFileName(dir),
          Total = total
        });
      }

      if (folders.Count == 0) {
        foreach (var system in MailArchiveLayout.SystemFolders) {
          folders.Add(new MailFolderInfo {
            FullName = system,
            Name = system
          });
        }
      }

      return Task.FromResult(Result<IReadOnlyList<MailFolderInfo>>.Ok(folders));
    }, cancellationToken);

  public Task<Result<MailFolderSync>> ListMessagesAsync(
    string folder,
    IReadOnlySet<uint>? knownIds = null,
    CancellationToken cancellationToken = default,
    int itemBudget = 0) =>
    _io.RunAsync(async () => {
      var dir = FolderDir(folder);
      if (!Directory.Exists(dir))
        return Result<MailFolderSync>.Ok(new MailFolderSync());
      var headers = new List<MailMessageHeader>();
      var present = new List<uint>();
      var budget = itemBudget <= 0 ? int.MaxValue : itemBudget;
      foreach (var file in Directory.EnumerateFiles(dir, "*.eml")) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!uint.TryParse(Path.GetFileNameWithoutExtension(file), out var uid))
          continue;
        present.Add(uid);
        if (knownIds is not null && knownIds.Contains(uid))
          continue;
        if (headers.Count >= budget)
          return Result<MailFolderSync>.Ok(new MailFolderSync {
            Headers = headers,
            Present = present,
            Incomplete = true
          });
        var body = await MimeBody.TryFromEmlAsync(folder, uid, file, cancellationToken).ConfigureAwait(false);
        if (body is not null)
          headers.Add(body.Header);
      }

      return Result<MailFolderSync>.Ok(new MailFolderSync {
        Headers = headers,
        Present = present
      });
    }, cancellationToken);

  public Task<Result<MailMessageBody>> GetMessageAsync(
    string folder,
    uint id,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(async () => {
      var path = MessagePath(folder, id);
      var body = await MimeBody.TryFromEmlAsync(folder, id, path, cancellationToken).ConfigureAwait(false);
      if (body is null)
        return Result<MailMessageBody>.NotFound(null, "Message file is missing.");
      return Result<MailMessageBody>.Ok(body);
    }, cancellationToken, interactive: true);

  public Task<Result<MailboxQuota?>> GetQuotaAsync(CancellationToken cancellationToken = default) {
    _ = cancellationToken;
    return Task.FromResult(Result<MailboxQuota?>.Ok(null));
  }

  public Task<Result<uint?>> AppendAsync(
    string folder,
    byte[] eml,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(async () => {
      var dir = FolderDir(folder);
      Directory.CreateDirectory(dir);
      uint uid = 1;
      foreach (var file in Directory.EnumerateFiles(dir, "*.eml")) {
        if (uint.TryParse(Path.GetFileNameWithoutExtension(file), out var found) && found >= uid)
          uid = found + 1;
      }

      var path = Path.Combine(dir, uid + ".eml");
      await File.WriteAllBytesAsync(path, eml, cancellationToken).ConfigureAwait(false);
      return Result<uint?>.Ok(uid);
    }, cancellationToken);

  public Task<Result<string>> SendAsync(MailSendRequest request, CancellationToken cancellationToken = default) {
    _ = request;
    _ = cancellationToken;
    return Task.FromResult(Result<string>.UnprocessableEntity(null, "Local stores cannot send mail."));
  }

  public Task<Result> MoveMessagesAsync(
    string fromFolder,
    IReadOnlyList<uint> ids,
    string toFolder,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => {
      Directory.CreateDirectory(FolderDir(toFolder));
      foreach (var id in ids) {
        cancellationToken.ThrowIfCancellationRequested();
        var source = MessagePath(fromFolder, id);
        if (!File.Exists(source))
          continue;
        var dest = MessagePath(toFolder, NextUid(toFolder));
        File.Copy(source, dest, overwrite: false);
        File.Delete(source);
      }

      return Task.FromResult(Result.Ok());
    }, cancellationToken, interactive: true);

  public Task<Result> SetMessageFlagsAsync(
    string folder,
    IReadOnlyList<uint> ids,
    MailFlagUpdate update,
    CancellationToken cancellationToken = default) {
    if (update.Deleted is not true)
      return Task.FromResult(Result.Ok());
    return _io.RunAsync(() => {
      foreach (var id in ids) {
        cancellationToken.ThrowIfCancellationRequested();
        var path = MessagePath(folder, id);
        if (File.Exists(path))
          File.Delete(path);
      }

      return Task.FromResult(Result.Ok());
    }, cancellationToken, interactive: true);
  }

  public Task<Result> CreateFolderAsync(
    string name,
    string? parentFolder,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => {
      _ = cancellationToken;
      var full = string.IsNullOrWhiteSpace(parentFolder) ? name : parentFolder + "/" + name;
      Directory.CreateDirectory(FolderDir(full));
      return Task.FromResult(Result.Ok());
    }, cancellationToken, interactive: true);

  public Task<Result> RenameFolderAsync(
    string folder,
    string? parentFolder,
    string name,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => {
      _ = cancellationToken;
      var dest = string.IsNullOrWhiteSpace(parentFolder) ? name : parentFolder + "/" + name;
      var from = FolderDir(folder);
      var to = FolderDir(dest);
      if (Directory.Exists(from))
        Directory.Move(from, to);
      return Task.FromResult(Result.Ok());
    }, cancellationToken);

  public Task<Result> DeleteFolderAsync(string folder, CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => {
      _ = cancellationToken;
      var dir = FolderDir(folder);
      if (Directory.Exists(dir))
        Directory.Delete(dir, recursive: true);
      return Task.FromResult(Result.Ok());
    }, cancellationToken);

  public Task<Result> EmptyFolderAsync(
    string folder,
    string? trashFolder,
    CancellationToken cancellationToken = default) =>
    _io.RunAsync(() => {
      var dir = FolderDir(folder);
      if (!Directory.Exists(dir))
        return Task.FromResult(Result.Ok());
      var files = Directory.EnumerateFiles(dir, "*.eml").ToList();
      var toTrash = !string.IsNullOrWhiteSpace(trashFolder)
        && !folder.Equals(trashFolder, StringComparison.OrdinalIgnoreCase);
      if (toTrash)
        Directory.CreateDirectory(FolderDir(trashFolder!));
      foreach (var file in files) {
        cancellationToken.ThrowIfCancellationRequested();
        if (toTrash) {
          var dest = MessagePath(trashFolder!, NextUid(trashFolder!));
          File.Copy(file, dest, overwrite: false);
        }

        File.Delete(file);
      }

      return Task.FromResult(Result.Ok());
    }, cancellationToken, interactive: true);

  public Task<Result> SetFolderSeenAsync(
    string folder,
    bool seen,
    CancellationToken cancellationToken = default) {
    _ = folder;
    _ = seen;
    _ = cancellationToken;
    return Task.FromResult(Result.Ok());
  }

  public ValueTask DisposeAsync() {
    _io.Dispose();
    return ValueTask.CompletedTask;
  }

  private string Root() {
    lock (_gate) {
      if (!_connected)
        throw new InvalidOperationException("Store is not connected.");
      return _root;
    }
  }

  private string FolderDir(string folder) =>
    MailArchiveLayout.FolderDirectory(Root(), folder);

  private uint NextUid(string folder) {
    var dir = FolderDir(folder);
    uint uid = 1;
    if (!Directory.Exists(dir))
      return uid;
    foreach (var file in Directory.EnumerateFiles(dir, "*.eml")) {
      if (uint.TryParse(Path.GetFileNameWithoutExtension(file), out var found) && found >= uid)
        uid = found + 1;
    }

    return uid;
  }
}
