using System.IO.Pipes;
using System.Text;
using System.Text.Json;


namespace MaksIT.PostClient.Client.Mail;


public sealed class MailWorkerHost : IDisposable {
  public const string Switch = "--worker";

  private readonly MailArchiveCatalog _archive;
  private readonly ConfigurationFileService _files;
  private SemanticSearchService? _semantic;

  public MailWorkerHost(MailArchiveCatalog archive, ConfigurationFileService files) {
    _archive = archive;
    _files = files;
  }

  public static bool IsWorkerProcess(string[] args) =>
    args.Length > 0 && string.Equals(args[0], Switch, StringComparison.OrdinalIgnoreCase);

  public static int Run(string[] args) {
    AppPaths.EnsureDirectories();
    var files = new ConfigurationFileService();
    files.Current.EnsureDefaults();
    using var archive = new MailArchiveCatalog();
    MailArchiveCatalog.MigrateLegacy(files.Current.Mailboxes);
    archive.OpenAll(files.Current.Mailboxes);
    using var host = new MailWorkerHost(archive, files);
    var pipe = args.Length > 1 ? args[1] : "postclient-worker";
    host.Listen(pipe);
    return 0;
  }

  public void StartSemantic() {
    _semantic ??= new SemanticSearchService(_files, _archive);
    _semantic.Start();
  }

  public void Listen(string pipeName) {
    using var server = new NamedPipeServerStream(
      pipeName,
      PipeDirection.InOut,
      1,
      PipeTransmissionMode.Byte,
      PipeOptions.Asynchronous);
    server.WaitForConnection();
    using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
    using var writer = new StreamWriter(server, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
    string? line;
    while ((line = reader.ReadLine()) is not null) {
      WorkerResponse response;
      try {
        var request = JsonSerializer.Deserialize<WorkerRequest>(line) ?? new WorkerRequest();
        response = Handle(request);
      }
      catch (Exception ex) {
        response = new WorkerResponse { Ok = false, Error = ex.Message };
      }

      writer.WriteLine(JsonSerializer.Serialize(response));
    }
  }

  public WorkerResponse Handle(WorkerRequest request) {
    switch (request.Op) {
      case "ping":
        return Ok("pong");
      case "import-pst":
        return ImportPst(request);
      case "copy-store":
        return CopyStore(request);
      case "retention":
        return Retention(request);
      default:
        return new WorkerResponse { Ok = false, Error = "Unknown op " + request.Op };
    }
  }

  public WorkerResponse ImportPst(WorkerRequest request) {
    var mailboxId = request.MailboxId ?? "";
    var path = request.Path ?? "";
    var import = new LocalMailImport(_archive);
    var session = new LocalStoreSession();
    var box = _files.Current.FindMailbox(mailboxId);
    if (box is not null)
      session.ConnectAsync(box, "", CancellationToken.None).GetAwaiter().GetResult();
    var count = PstImport.ImportAsync(
      import,
      mailboxId,
      path,
      session,
      unwrap: _files.Current.UnwrapEnvelope,
      CancellationToken.None).GetAwaiter().GetResult();
    return Ok(count.ToString());
  }

  public WorkerResponse CopyStore(WorkerRequest request) {
    var source = request.Path ?? "";
    var dest = request.Dest ?? "";
    if (string.IsNullOrWhiteSpace(source) || !Directory.Exists(source))
      return new WorkerResponse { Ok = false, Error = "Source store is missing." };
    _archive.CopyDirectory(source, dest);
    return Ok(dest);
  }

  public WorkerResponse Retention(WorkerRequest request) {
    _ = request;
    var count = 0;
    var configuration = _files.Current;
    configuration.EnsureDefaults();
    foreach (var job in MailRetention.Jobs(configuration.Retention)) {
      var box = configuration.FindMailbox(job.MailboxId);
      var remote = box is { IsLocalStore: false };
      var known = _archive.ListFolders(job.MailboxId);
      var folder = MailRetention.BindFolder(job.Folder, known);
      if (string.IsNullOrWhiteSpace(folder))
        continue;
      var cutoff = DateTimeOffset.UtcNow.AddDays(-job.Days);
      var ids = _archive.UidsOlderThan(
        job.MailboxId,
        folder,
        cutoff,
        useReceivedDate: remote && MailRetention.IsTrash(folder));
      if (ids.Count == 0)
        continue;
      if (MailRetention.IsTrash(folder)) {
        if (remote)
          continue;
        DeleteFiles(_archive.RemoveUids(job.MailboxId, folder, ids));
        count += ids.Count;
        continue;
      }

      var trash = MailRetention.ResolveTrash(known);
      if (folder.Equals(trash, StringComparison.OrdinalIgnoreCase)) {
        DeleteFiles(_archive.RemoveUids(job.MailboxId, folder, ids));
        count += ids.Count;
        continue;
      }

      count += MoveToTrash(configuration, job.MailboxId, folder, trash, ids);
    }

    return Ok(count.ToString());
  }

  private int MoveToTrash(
    Configuration configuration,
    string mailboxId,
    string folder,
    string trash,
    IReadOnlyList<uint> ids) {
    var moved = new List<uint>();
    foreach (var uid in ids) {
      try {
        var source = _archive.EmlPath(mailboxId, folder, uid);
        var destUid = _archive.NextUid(mailboxId, trash);
        var dest = RetentionDestPath(configuration, mailboxId, trash, destUid);
        if (!string.IsNullOrWhiteSpace(source)
            && File.Exists(source)
            && !source.Equals(dest, StringComparison.OrdinalIgnoreCase)) {
          var dir = Path.GetDirectoryName(dest);
          if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
          File.Copy(source, dest, overwrite: false);
        }

        _archive.CopyIndexed(mailboxId, folder, uid, mailboxId, trash, dest, destUid);
        moved.Add(uid);
      }
      catch {
      }
    }

    if (moved.Count > 0)
      DeleteFiles(_archive.RemoveUids(mailboxId, folder, moved));
    return moved.Count;
  }

  private static string RetentionDestPath(
    Configuration configuration,
    string mailboxId,
    string folder,
    uint uid) {
    var box = configuration.FindMailbox(mailboxId);
    if (box is not null)
      return MailArchiveLayout.EmlPath(box, configuration.Mailboxes, folder, uid);
    return ArchiveFiles.EmlPath(mailboxId, folder, uid);
  }

  private static void DeleteFiles(IEnumerable<string> paths) {
    foreach (var path in paths) {
      try {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
          File.Delete(path);
      }
      catch {
      }
    }
  }

  public void Dispose() =>
    _semantic?.Dispose();

  private static WorkerResponse Ok(string value) =>
    new() { Ok = true, Value = value };
}


public sealed class WorkerRequest {
  public string Op { get; set; } = "";

  public string? MailboxId { get; set; }

  public string? Path { get; set; }

  public string? Dest { get; set; }
}


public sealed class WorkerResponse {
  public bool Ok { get; set; }

  public string? Value { get; set; }

  public string? Error { get; set; }
}


public sealed class MailWorkerClient : IDisposable {
  private readonly MailArchiveCatalog _archive;
  private readonly ConfigurationFileService _files;
  private readonly string? _processPath;
  private readonly Lock _gate = new();
  private NamedPipeClientStream? _pipe;
  private StreamWriter? _writer;
  private StreamReader? _reader;
  private MailWorkerHost? _inline;

  private MailWorkerClient(
    MailArchiveCatalog archive,
    ConfigurationFileService files,
    string? processPath) {
    _archive = archive;
    _files = files;
    _processPath = processPath;
  }

  public static MailWorkerClient Create(
    MailArchiveCatalog archive,
    ConfigurationFileService files,
    string? processPath) =>
    new(archive, files, processPath);

  public static MailWorkerClient StartInline(MailArchiveCatalog archive, ConfigurationFileService files) {
    var client = new MailWorkerClient(archive, files, processPath: null);
    client._inline = new MailWorkerHost(archive, files);
    return client;
  }

  public WorkerResponse Call(WorkerRequest request) {
    lock (_gate) {
      Ensure();
      if (_inline is not null)
        return _inline.Handle(request);
      _writer!.WriteLine(JsonSerializer.Serialize(request));
      var line = _reader!.ReadLine();
      if (string.IsNullOrWhiteSpace(line))
        return new WorkerResponse { Ok = false, Error = "Worker closed." };
      return JsonSerializer.Deserialize<WorkerResponse>(line)
        ?? new WorkerResponse { Ok = false, Error = "Empty response." };
    }
  }

  public void Dispose() {
    _inline?.Dispose();
    _writer?.Dispose();
    _reader?.Dispose();
    _pipe?.Dispose();
  }

  private void Ensure() {
    if (_inline is not null || _writer is not null)
      return;
    if (TryConnectProcess())
      return;
    _inline = new MailWorkerHost(_archive, _files);
  }

  private bool TryConnectProcess() {
    if (string.IsNullOrWhiteSpace(_processPath) || !File.Exists(_processPath))
      return false;
    try {
      var pipe = "postclient-" + Guid.NewGuid().ToString("N")[..12];
      var start = new System.Diagnostics.ProcessStartInfo(_processPath, MailWorkerHost.Switch + " " + pipe) {
        UseShellExecute = false,
        CreateNoWindow = true
      };
      System.Diagnostics.Process.Start(start);
      var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut);
      client.Connect(TimeSpan.FromSeconds(8));
      _pipe = client;
      _writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
      _reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);
      return true;
    }
    catch {
      _writer?.Dispose();
      _reader?.Dispose();
      _pipe?.Dispose();
      _writer = null;
      _reader = null;
      _pipe = null;
      return false;
    }
  }
}
