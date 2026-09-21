namespace MaksIT.PostClient.Shared.Mail;


public static class MailMessageList {
  public static void Merge<T>(
    IList<T> target,
    IReadOnlyList<T> incoming,
    Func<T, uint> uid,
    Action<T, T> patch) {
    ArgumentNullException.ThrowIfNull(target);
    ArgumentNullException.ThrowIfNull(incoming);
    ArgumentNullException.ThrowIfNull(uid);
    ArgumentNullException.ThrowIfNull(patch);
    var byUid = new Dictionary<uint, T>();
    foreach (var row in target)
      byUid[uid(row)] = row;
    var keep = new HashSet<uint>();
    foreach (var row in incoming) {
      var id = uid(row);
      keep.Add(id);
      if (byUid.TryGetValue(id, out var existing))
        patch(existing, row);
      else {
        target.Add(row);
        byUid[id] = row;
      }
    }

    for (var i = target.Count - 1; i >= 0; i--) {
      if (!keep.Contains(uid(target[i])))
        target.RemoveAt(i);
    }
  }

  public static bool CoversAll<T>(
    IReadOnlyCollection<T> selected,
    IReadOnlyCollection<T> visible,
    Func<T, MailMessageKey> key) {
    ArgumentNullException.ThrowIfNull(selected);
    ArgumentNullException.ThrowIfNull(visible);
    ArgumentNullException.ThrowIfNull(key);
    if (visible.Count == 0 || selected.Count < visible.Count)
      return false;
    var vis = visible.Select(key).ToHashSet();
    var sel = selected.Select(key).ToHashSet();
    return vis.SetEquals(sel);
  }

  public static IReadOnlyList<T> Resolve<T>(
    IEnumerable<MailMessageKey> keys,
    IEnumerable<T> rows,
    Func<T, MailMessageKey> key) {
    ArgumentNullException.ThrowIfNull(keys);
    ArgumentNullException.ThrowIfNull(rows);
    ArgumentNullException.ThrowIfNull(key);
    var map = new Dictionary<MailMessageKey, T>();
    foreach (var row in rows)
      map.TryAdd(key(row), row);
    var list = new List<T>();
    var seen = new HashSet<MailMessageKey>();
    foreach (var item in keys) {
      if (!seen.Add(item))
        continue;
      if (map.TryGetValue(item, out var row))
        list.Add(row);
    }

    return list;
  }

  public static T? Live<T>(
    IReadOnlyList<T> selected,
    T? current,
    Func<T, MailMessageKey> key) {
    ArgumentNullException.ThrowIfNull(selected);
    ArgumentNullException.ThrowIfNull(key);
    if (selected.Count == 0)
      return default;
    if (current is not null) {
      var currentKey = key(current);
      foreach (var row in selected) {
        if (key(row).Equals(currentKey))
          return row;
      }
    }

    return selected[0];
  }
}
