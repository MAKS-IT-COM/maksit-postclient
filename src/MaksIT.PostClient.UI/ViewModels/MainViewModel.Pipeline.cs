using Avalonia.Threading;


namespace MaksIT.PostClient.UI.ViewModels;


public partial class MainViewModel {
  private MailPipeline? _pipeline;
  private int _pipelineSlot;
  private string? _pipelineSticky;
  private readonly Dictionary<string, DateTimeOffset> _folderPollUtc = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, DateTimeOffset> _folderFailUtc = new(StringComparer.OrdinalIgnoreCase);
  private readonly Queue<FreshRuleBatch> _freshRules = new();
  private readonly object _freshGate = new();
  private TaskCompletionSource _indexHolding = new(TaskCreationOptions.RunContinuationsAsynchronously);

  private void StartPipeline() {
    if (_pipeline is not null)
      return;
    _pipeline = new MailPipeline(new MailPipelineHost(this));
    _pipeline.Start();
  }

  private void StopPipeline() {
    _pipeline?.Stop();
    _pipeline = null;
    ClearPipelineState();
  }

  private void InterruptPipeline() => _pipeline?.Interrupt();

  private Task PipelineHolding() {
    var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    _indexHolding = holding;
    return holding.Task;
  }

  private void ClearPipelineState() {
    lock (_freshGate)
      _freshRules.Clear();
    _folderPollUtc.Clear();
    _folderFailUtc.Clear();
    _pipelineSticky = null;
    _pipelineSlot = 0;
  }

  private void DropPipelineMailbox(string mailboxId) {
    var prefix = mailboxId + "\n";
    lock (_freshGate) {
      var keep = _freshRules.Where(batch =>
        !batch.MailboxId.Equals(mailboxId, StringComparison.OrdinalIgnoreCase)).ToList();
      _freshRules.Clear();
      foreach (var batch in keep)
        _freshRules.Enqueue(batch);
    }

    foreach (var key in _folderPollUtc.Keys.Where(key =>
               key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
      _folderPollUtc.Remove(key);
    foreach (var key in _folderFailUtc.Keys.Where(key =>
               key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
      _folderFailUtc.Remove(key);
    if (_pipelineSticky is not null && _pipelineSticky.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
      _pipelineSticky = null;
  }

  private async Task<bool> FetchNextPageAsync(CancellationToken token) {
    var snapshot = await Dispatcher.UIThread.InvokeAsync(() => {
      TrackConnectedMailboxes();
      return CaptureIndexSnapshot();
    });
    var slots = new List<PipeSlot>();
    foreach (var box in snapshot) {
      if (box.Session is not { IsConnected: true, SupportsFolders: true })
        continue;
      foreach (var folder in box.Folders)
        slots.Add(new PipeSlot(
          box.Id,
          folder.FullName,
          folder.Name,
          box.Session,
          box.CatalogedFolders.Contains(folder.FullName)));
    }

    if (slots.Count == 0)
      return false;
    var chosen = ChooseSlot(slots);
    if (chosen < 0)
      return false;
    var slot = slots[chosen];
    var key = CatalogKey(slot.MailboxId, slot.Folder);
    _folderPollUtc[key] = DateTimeOffset.UtcNow;
    var known = _archive.Uids(slot.MailboxId, slot.Folder);
    var listed = await slot.Session
      .ListMessagesAsync(slot.Folder, known, token, MailPipeline.HeaderPage)
      .ConfigureAwait(false);
    if (!listed.IsSuccess) {
      _pipelineSticky = null;
      _folderFailUtc[key] = DateTimeOffset.UtcNow;
      return true;
    }

    _folderFailUtc.Remove(key);

    var sync = listed.Value ?? new MailFolderSync();
    ApplyFolderSync(slot.MailboxId, slot.Folder, sync);
    var fresh = sync.Headers.Where(header => !known.Contains(header.Id)).ToList();
    if (fresh.Count > 0) {
      EnqueueFreshRules(slot.MailboxId, slot.Folder, fresh);
      await UiAsync(() => {
        if (SelectedMailbox?.Id.Equals(slot.MailboxId, StringComparison.OrdinalIgnoreCase) == true
            && SelectedFolder?.FullName.Equals(slot.Folder, StringComparison.OrdinalIgnoreCase) == true)
          NoticeNewMail(fresh);
      }).ConfigureAwait(false);
      await ShowArchiveAsync(slot.MailboxId, slot.Folder, token).ConfigureAwait(false);
    }

    await SetIndexLineAsync(GlobalIndexLine(), token).ConfigureAwait(false);
    var counts = _archive.FolderCounts(slot.MailboxId, slot.Folder);
    await UiAsync(() => RefreshIndexedFolderCounts(slot.MailboxId, slot.Folder, counts))
      .ConfigureAwait(false);
    if (sync.Incomplete) {
      _pipelineSticky = key;
      return true;
    }

    _pipelineSticky = null;
    await UiAsync(() => {
      _catalogFolders.Add(key);
      TryCompleteInitialSync(MailboxById(slot.MailboxId));
    }).ConfigureAwait(false);
    return true;
  }

  private int ChooseSlot(List<PipeSlot> slots) {
    if (!string.IsNullOrEmpty(_pipelineSticky)) {
      var sticky = slots.FindIndex(slot =>
        CatalogKey(slot.MailboxId, slot.Folder).Equals(_pipelineSticky, StringComparison.OrdinalIgnoreCase));
      if (sticky >= 0)
        return sticky;
      _pipelineSticky = null;
    }

    var start = slots.Count == 0 ? 0 : _pipelineSlot % slots.Count;
    var now = DateTimeOffset.UtcNow;
    var chosen = First(slot => Ready(slot) && !slot.Cataloged && IsInbox(slot));
    if (chosen < 0)
      chosen = First(slot => Ready(slot) && !slot.Cataloged);
    if (chosen < 0)
      chosen = First(slot => Ready(slot) && Due(slot) && IsInbox(slot));
    if (chosen < 0)
      chosen = First(slot => Ready(slot) && Due(slot));
    if (chosen >= 0)
      _pipelineSlot = (chosen + 1) % slots.Count;
    return chosen;

    int First(Func<PipeSlot, bool> match) {
      for (var n = 0; n < slots.Count; n++) {
        var index = (start + n) % slots.Count;
        if (match(slots[index]))
          return index;
      }

      return -1;
    }

    bool Due(PipeSlot slot) {
      if (!slot.Cataloged)
        return true;
      return !_folderPollUtc.TryGetValue(CatalogKey(slot.MailboxId, slot.Folder), out var last)
        || now - last >= MailPipeline.FolderPoll;
    }

    bool Ready(PipeSlot slot) =>
      !_folderFailUtc.TryGetValue(CatalogKey(slot.MailboxId, slot.Folder), out var failed)
      || now - failed >= TimeSpan.FromSeconds(20);

    static bool IsInbox(PipeSlot slot) =>
      MailFolderRole.Kind(slot.Name, slot.Folder) == "inbox";
  }

  private async Task<bool> ApplyPipelineRulesAsync(CancellationToken token) {
    FreshRuleBatch? job;
    lock (_freshGate)
      job = _freshRules.Count > 0 ? _freshRules.Dequeue() : null;
    if (job is null) {
      var snapshot = await Dispatcher.UIThread.InvokeAsync(CaptureIndexSnapshot);
      return await ApplyNextArchiveRulesAsync(snapshot, token).ConfigureAwait(false);
    }

    var slice = job.Headers.Skip(job.Offset).Take(MailPipeline.RulesBatch).ToList();
    job.Offset += slice.Count;
    if (job.Offset < job.Headers.Count) {
      lock (_freshGate)
        _freshRules.Enqueue(job);
    }

    if (slice.Count == 0)
      return false;
    var account = await Dispatcher.UIThread.InvokeAsync(() => MailboxById(job.MailboxId));
    if (account is null)
      return true;
    var session = await Dispatcher.UIThread.InvokeAsync(() => SessionFor(account));
    var names = await Dispatcher.UIThread.InvokeAsync(() =>
      FoldersFor(account).Select(folder => (folder.Name, folder.FullName)).ToList());
    if (session is not { IsConnected: true })
      return true;
    var touched = await ApplyRulesToFolderAsync(account, session, job.Folder, slice, names, token)
      .ConfigureAwait(false);
    if (touched > 0)
      await RefreshIfOpenAsync(account.Id, job.Folder, token).ConfigureAwait(false);
    return true;
  }

  private async Task<int> IndexPipelineBodiesAsync(int limit, CancellationToken token) {
    var ready = await Dispatcher.UIThread.InvokeAsync(() => {
      var sessions = new Dictionary<string, IMailSession>(StringComparer.OrdinalIgnoreCase);
      var ids = ReadyMailboxIds();
      foreach (var id in ids) {
        var session = SessionFor(MailboxById(id));
        if (session is { IsConnected: true })
          sessions[id] = session;
      }

      return (Ids: ids, Sessions: sessions);
    });
    var pending = NextMissingBodies(ready.Ids).Take(limit).ToList();
    if (pending.Count == 0) {
      await SetIndexLineAsync("", token).ConfigureAwait(false);
      return 0;
    }

    await SetIndexLineAsync(GlobalIndexLine(), token).ConfigureAwait(false);
    var stored = new List<(string MailboxId, string Folder, MailMessageHeader Header)>();
    var fetched = 0;
    foreach (var item in pending) {
      token.ThrowIfCancellationRequested();
      await WaitIfIndexingPausedAsync(token).ConfigureAwait(false);
      if (!ready.Sessions.TryGetValue(item.MailboxId, out var session))
        continue;
      var key = MailMessageKey.Of(item.MailboxId, item.Folder, item.Uid);
      try {
        var result = await session
          .GetMessageAsync(item.Folder, item.Uid, token, interactive: false)
          .ConfigureAwait(false);
        if (result.IsSuccess && result.Value is not null) {
          StoreArchivedBody(item.MailboxId, result.Value.Header, result.Value);
          _indexSkip.Remove(key);
          stored.Add((item.MailboxId, item.Folder, result.Value.Header));
          fetched++;
          continue;
        }

        if (MailFetch.IsGone(result.Messages)) {
          _archive.RemoveUids(item.MailboxId, item.Folder, [item.Uid]);
          _indexSkip.Remove(key);
          fetched++;
          continue;
        }

        _indexSkip.Add(key);
      }
      catch (OperationCanceledException) when (token.IsCancellationRequested) {
        throw;
      }
      catch (OperationCanceledException) {
        _indexSkip.Add(key);
      }
      catch {
        _indexSkip.Add(key);
      }
    }

    await ApplyContentRulesAsync(stored, ready.Sessions, token).ConfigureAwait(false);
    return fetched;
  }

  private async Task ApplyContentRulesAsync(
    List<(string MailboxId, string Folder, MailMessageHeader Header)> stored,
    IReadOnlyDictionary<string, IMailSession> sessions,
    CancellationToken token) {
    foreach (var group in stored.GroupBy(row => (row.MailboxId, row.Folder))) {
      if (!RulesWatchContent(group.Key.MailboxId))
        continue;
      if (!sessions.TryGetValue(group.Key.MailboxId, out var session))
        continue;
      var account = await Dispatcher.UIThread.InvokeAsync(() => MailboxById(group.Key.MailboxId));
      if (account is null)
        continue;
      var names = await Dispatcher.UIThread.InvokeAsync(() =>
        FoldersFor(account).Select(folder => (folder.Name, folder.FullName)).ToList());
      var touched = await ApplyRulesToFolderAsync(
        account,
        session,
        group.Key.Folder,
        group.Select(row => row.Header).ToList(),
        names,
        token).ConfigureAwait(false);
      if (touched > 0)
        await RefreshIfOpenAsync(account.Id, group.Key.Folder, token).ConfigureAwait(false);
    }
  }

  private async Task RefreshIfOpenAsync(string mailboxId, string folder, CancellationToken token) {
    var open = await Dispatcher.UIThread.InvokeAsync(() =>
      SelectedMailbox?.Id.Equals(mailboxId, StringComparison.OrdinalIgnoreCase) == true
      && SelectedFolder?.FullName.Equals(folder, StringComparison.OrdinalIgnoreCase) == true);
    if (open)
      await ShowArchiveAsync(mailboxId, folder, token).ConfigureAwait(false);
  }

  private void EnqueueFreshRules(string mailboxId, string folder, IReadOnlyList<MailMessageHeader> headers) {
    if (headers.Count == 0)
      return;
    lock (_freshGate)
      _freshRules.Enqueue(new FreshRuleBatch {
        MailboxId = mailboxId,
        Folder = folder,
        Headers = headers.ToList()
      });
  }

  private void WakeMeaningIndex() => _semantic.Wake();

  private bool RulesWatchContent(string mailboxId) {
    foreach (var rule in MailRuleEngine.Ready(ActiveRules())) {
      if (!MailRuleEngine.TargetsMailbox(rule, mailboxId))
        continue;
      foreach (var condition in rule.Conditions) {
        var field = (condition.Field ?? "").Trim();
        if (field.Equals("body", StringComparison.OrdinalIgnoreCase)
            || field.Equals("attachment", StringComparison.OrdinalIgnoreCase)
            || field.Equals("has-attachment", StringComparison.OrdinalIgnoreCase))
          return true;
      }
    }

    return false;
  }

  private readonly record struct PipeSlot(
    string MailboxId,
    string Folder,
    string Name,
    IMailSession Session,
    bool Cataloged);

  private sealed class FreshRuleBatch {
    public required string MailboxId { get; init; }

    public required string Folder { get; init; }

    public required List<MailMessageHeader> Headers { get; init; }

    public int Offset { get; set; }
  }

  private sealed class MailPipelineHost : IMailPipelineWork {
    private readonly MainViewModel _view;

    public MailPipelineHost(MainViewModel view) => _view = view;

    public Task WaitWhilePausedAsync(CancellationToken token) =>
      _view.WaitIfIndexingPausedAsync(token);

    public Task<bool> FetchNextPageAsync(CancellationToken token) =>
      _view.FetchNextPageAsync(token);

    public Task<bool> ApplyRulesAsync(CancellationToken token) =>
      _view.ApplyPipelineRulesAsync(token);

    public Task<int> IndexBodiesAsync(int limit, CancellationToken token) =>
      _view.IndexPipelineBodiesAsync(limit, token);

    public void WakeMeaningIndex() => _view.WakeMeaningIndex();
  }
}
