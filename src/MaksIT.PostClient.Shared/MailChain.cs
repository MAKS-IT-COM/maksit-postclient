namespace MaksIT.PostClient.Shared;


public sealed class MailChainItem {
  public uint Uid { get; init; }

  public string MessageId { get; init; } = "";

  public string InReplyTo { get; init; } = "";

  public DateTimeOffset Date { get; init; }
}


public sealed class MailChainLink {
  public uint Uid { get; init; }

  public int Depth { get; init; }

  public int Size { get; init; } = 1;
}


public static class MailChain {
  public const int MaxDepth = 8;

  public static IReadOnlyList<MailChainLink> Order(IReadOnlyList<MailChainItem> items, bool group) {
    ArgumentNullException.ThrowIfNull(items);
    if (items.Count == 0)
      return [];
    if (!group)
      return Flat(items);

    var byUid = items.GroupBy(i => i.Uid).ToDictionary(g => g.Key, g => g.First());
    var byId = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
    foreach (var item in items) {
      var id = MailId.Normalize(item.MessageId);
      if (id.Length > 0)
        byId.TryAdd(id, item.Uid);
    }

    var parentOf = new Dictionary<uint, uint>();
    var ghosts = new Dictionary<string, List<uint>>(StringComparer.OrdinalIgnoreCase);
    foreach (var item in items) {
      var parentId = MailId.Normalize(item.InReplyTo);
      if (parentId.Length == 0)
        continue;
      if (byId.TryGetValue(parentId, out var parentUid) && parentUid != item.Uid)
        parentOf[item.Uid] = parentUid;
      else
        Ghost(ghosts, parentId).Add(item.Uid);
    }

    BreakCycles(parentOf);
    var ghostMembers = new HashSet<uint>(ghosts.Values.SelectMany(v => v));
    foreach (var uid in ghostMembers)
      parentOf.Remove(uid);

    var children = new Dictionary<uint, List<uint>>();
    foreach (var pair in parentOf)
      Kids(children, pair.Value).Add(pair.Key);
    foreach (var list in children.Values)
      list.Sort((a, b) => CompareDate(byUid[a], byUid[b]));

    var threads = new List<ChainThread>();
    foreach (var item in items) {
      if (parentOf.ContainsKey(item.Uid) || ghostMembers.Contains(item.Uid))
        continue;
      threads.Add(new ChainThread(MaxDate(item.Uid, children, byUid), [item.Uid], true));
    }

    foreach (var ghost in ghosts.Values) {
      if (ghost.Count == 0)
        continue;
      ghost.Sort((a, b) => CompareDate(byUid[a], byUid[b]));
      var latest = ghost.Max(uid => byUid[uid].Date);
      threads.Add(new ChainThread(latest, ghost, false));
    }

    threads.Sort((a, b) => b.Latest.CompareTo(a.Latest));
    var rows = new List<MailChainLink>(items.Count);
    foreach (var thread in threads) {
      if (thread.Rooted)
        Walk(thread.Uids[0], 0, children, byUid, rows);
      else {
        for (var i = 0; i < thread.Uids.Count; i++)
          rows.Add(new MailChainLink {
            Uid = thread.Uids[i],
            Depth = 0,
            Size = i == 0 ? thread.Uids.Count : 1
          });
      }
    }

    return rows;
  }

  private static IReadOnlyList<MailChainLink> Flat(IReadOnlyList<MailChainItem> items) =>
    items
      .OrderByDescending(i => i.Date)
      .ThenByDescending(i => i.Uid)
      .Select(i => new MailChainLink { Uid = i.Uid, Depth = 0, Size = 1 })
      .ToList();

  private static void Walk(
    uint uid,
    int depth,
    Dictionary<uint, List<uint>> children,
    Dictionary<uint, MailChainItem> byUid,
    List<MailChainLink> rows) {
    rows.Add(new MailChainLink {
      Uid = uid,
      Depth = depth,
      Size = 1 + Count(uid, children)
    });
    if (!children.TryGetValue(uid, out var kids))
      return;
    var next = Math.Min(depth + 1, MaxDepth);
    foreach (var child in kids)
      Walk(child, next, children, byUid, rows);
  }

  private static int Count(uint uid, Dictionary<uint, List<uint>> children) {
    if (!children.TryGetValue(uid, out var kids))
      return 0;
    var n = kids.Count;
    foreach (var child in kids)
      n += Count(child, children);
    return n;
  }

  private static DateTimeOffset MaxDate(
    uint uid,
    Dictionary<uint, List<uint>> children,
    Dictionary<uint, MailChainItem> byUid) {
    var max = byUid[uid].Date;
    if (!children.TryGetValue(uid, out var kids))
      return max;
    foreach (var child in kids) {
      var nested = MaxDate(child, children, byUid);
      if (nested > max)
        max = nested;
    }

    return max;
  }

  private static void BreakCycles(Dictionary<uint, uint> parentOf) {
    foreach (var start in parentOf.Keys.ToList()) {
      var seen = new HashSet<uint> { start };
      var current = start;
      while (parentOf.TryGetValue(current, out var parent)) {
        if (!seen.Add(parent)) {
          parentOf.Remove(start);
          break;
        }

        current = parent;
      }
    }
  }

  private static List<uint> Kids(Dictionary<uint, List<uint>> map, uint uid) {
    if (!map.TryGetValue(uid, out var list)) {
      list = [];
      map[uid] = list;
    }

    return list;
  }

  private static List<uint> Ghost(Dictionary<string, List<uint>> map, string key) {
    if (!map.TryGetValue(key, out var list)) {
      list = [];
      map[key] = list;
    }

    return list;
  }

  private static int CompareDate(MailChainItem a, MailChainItem b) {
    var cmp = a.Date.CompareTo(b.Date);
    return cmp != 0 ? cmp : a.Uid.CompareTo(b.Uid);
  }

  private sealed record ChainThread(DateTimeOffset Latest, List<uint> Uids, bool Rooted);
}
