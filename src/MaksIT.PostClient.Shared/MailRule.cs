namespace MaksIT.PostClient.Shared;


public sealed class MailRule {
  public string Id { get; set; } = Guid.NewGuid().ToString("N");

  public string Name { get; set; } = "";

  public bool Enabled { get; set; } = true;

  public int Sequence { get; set; }

  public string Logic { get; set; } = "and";

  public List<MailRuleCondition> Conditions { get; set; } = [];

  public string Action { get; set; } = "";

  public string MailboxId { get; set; } = "";

  public string FolderMailboxId { get; set; } = "";

  public string Folder { get; set; } = "";

  public string Label { get; set; } = "";

  public bool Stop { get; set; }

  public bool HasWork =>
    !string.IsNullOrWhiteSpace(Action)
    && Conditions.Any(c =>
      c.Op.Equals("exists", StringComparison.OrdinalIgnoreCase)
      || !string.IsNullOrWhiteSpace(c.Value));
}


public sealed class MailRuleCondition {
  public string Field { get; set; } = "";

  public string Op { get; set; } = "contains";

  public string Value { get; set; } = "";
}


public static class MailRuleAction {
  public const string Move = "move";
  public const string Delete = "delete";
  public const string MarkRead = "mark-read";
  public const string Flag = "flag";
  public const string Label = "label";
}


public static class MailRuleEngine {
  public static bool Matches(
    MailRule rule,
    string from,
    string to,
    string subject,
    string body,
    bool hasAttachment) {
    if (!rule.Enabled || rule.Conditions.Count == 0)
      return false;
    var or = rule.Logic.Equals("or", StringComparison.OrdinalIgnoreCase);
    foreach (var condition in rule.Conditions) {
      var hit = Hit(condition, from, to, subject, body, hasAttachment);
      if (or) {
        if (hit)
          return true;
      }
      else if (!hit) {
        return false;
      }
    }

    return !or;
  }

  public static IEnumerable<MailRule> Ready(IEnumerable<MailRule>? rules) =>
    (rules ?? []).Where(r => r.HasWork && r.Enabled).OrderBy(r => r.Sequence).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase);

  public static bool TargetsMailbox(MailRule rule, string? mailboxId) =>
    !string.IsNullOrWhiteSpace(rule.MailboxId)
    && !string.IsNullOrWhiteSpace(mailboxId)
    && rule.MailboxId.Equals(mailboxId, StringComparison.OrdinalIgnoreCase);

  public static string FolderMailbox(MailRule rule) {
    if (!string.IsNullOrWhiteSpace(rule.FolderMailboxId))
      return rule.FolderMailboxId.Trim();
    return (rule.MailboxId ?? "").Trim();
  }

  public static bool CanApply(
    MailRule rule,
    string? mailboxId,
    IReadOnlyList<(string Name, string FullName)> folders) {
    if (!rule.Enabled || !rule.HasWork || !TargetsMailbox(rule, mailboxId))
      return false;
    if (rule.Action == MailRuleAction.Move)
      return ExactFolder(rule.Folder, folders) is not null;
    return true;
  }

  public static string? BindFolder(string? wanted, IReadOnlyList<(string Name, string FullName)> folders) =>
    ExactFolder(wanted, folders) ?? (string.IsNullOrWhiteSpace(wanted) ? null : wanted.Trim());

  public static int RetargetFolders(
    IEnumerable<MailRule>? rules,
    string mailboxId,
    string from,
    string to,
    string? toMailboxId = null) {
    if (string.IsNullOrWhiteSpace(mailboxId) || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
      return 0;
    var count = 0;
    foreach (var rule in rules ?? []) {
      var owner = FolderMailbox(rule);
      if (!owner.Equals(mailboxId, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(rule.Folder))
        continue;
      var next = MailFolderPath.Rewrite(rule.Folder, from, to);
      if (next is null || next.Equals(rule.Folder, StringComparison.Ordinal))
        continue;
      rule.Folder = next;
      if (!string.IsNullOrWhiteSpace(toMailboxId)
          && !toMailboxId.Equals(owner, StringComparison.OrdinalIgnoreCase))
        rule.FolderMailboxId = toMailboxId;
      count++;
    }

    return count;
  }

  public static string? ExactFolder(string? wanted, IReadOnlyList<(string Name, string FullName)> folders) {
    if (string.IsNullOrWhiteSpace(wanted) || folders.Count == 0)
      return null;
    var value = wanted.Trim();
    var wantedLeaf = Leaf(value);
    string? leafHit = null;
    foreach (var folder in folders) {
      if (Same(folder.FullName, value) || Same(folder.Name, value))
        return folder.FullName;
      if (leafHit is not null)
        continue;
      if (HitsLeaf(folder, value) || (!Same(wantedLeaf, value) && HitsName(folder, wantedLeaf)))
        leafHit = folder.FullName;
    }

    return leafHit;
  }

  private static bool HitsName((string Name, string FullName) folder, string value) =>
    Same(folder.FullName, value) || Same(folder.Name, value) || HitsLeaf(folder, value);

  private static bool HitsLeaf((string Name, string FullName) folder, string value) =>
    Same(Leaf(folder.FullName), value) || Same(Leaf(folder.Name), value);

  private static bool Same(string? left, string right) =>
    !string.IsNullOrWhiteSpace(left)
    && left.Trim().Equals(right, StringComparison.OrdinalIgnoreCase);

  private static string Leaf(string path) {
    var value = path.Replace('\\', '/').Trim().TrimEnd('/');
    var i = value.LastIndexOf('/');
    return i < 0 ? value : value[(i + 1)..];
  }

  public static string? ResolveFolder(string? wanted, IReadOnlyList<(string Name, string FullName)> folders) {
    if (string.IsNullOrWhiteSpace(wanted) || folders.Count == 0)
      return null;
    var kind = MailFolderRole.Kind(wanted, wanted);
    if (!string.IsNullOrWhiteSpace(kind)) {
      var mapped = folders.FirstOrDefault(f => MailFolderRole.Kind(f.Name, f.FullName) == kind);
      if (!string.IsNullOrWhiteSpace(mapped.FullName))
        return mapped.FullName;
    }

    foreach (var folder in folders) {
      if (folder.FullName.Equals(wanted, StringComparison.OrdinalIgnoreCase)
          || folder.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase))
        return folder.FullName;
    }

    var leaf = wanted.Contains('/') ? wanted[(wanted.LastIndexOf('/') + 1)..] : wanted;
    foreach (var folder in folders) {
      if (folder.Name.Equals(leaf, StringComparison.OrdinalIgnoreCase))
        return folder.FullName;
    }

    return wanted;
  }

  public static int Merge(List<MailRule> into, IEnumerable<MailRule> extra) {
    into ??= [];
    var added = 0;
    foreach (var rule in extra) {
      if (!rule.HasWork)
        continue;
      var name = string.IsNullOrWhiteSpace(rule.Name) ? rule.Id : rule.Name.Trim();
      var existing = into.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
      if (existing is null) {
        rule.Name = name;
        if (string.IsNullOrWhiteSpace(rule.Id))
          rule.Id = Guid.NewGuid().ToString("N");
        into.Add(rule);
        added++;
        continue;
      }

      existing.Enabled = rule.Enabled;
      existing.Sequence = rule.Sequence;
      existing.Logic = rule.Logic;
      existing.Conditions = rule.Conditions;
      existing.Action = rule.Action;
      existing.MailboxId = rule.MailboxId;
      existing.FolderMailboxId = rule.FolderMailboxId;
      existing.Folder = rule.Folder;
      existing.Label = rule.Label;
      existing.Stop = rule.Stop;
      added++;
    }

    return added;
  }

  private static bool Hit(
    MailRuleCondition condition,
    string from,
    string to,
    string subject,
    string body,
    bool hasAttachment) {
    var field = (condition.Field ?? "").Trim().ToLowerInvariant();
    if (field is "attachment" or "has-attachment") {
      if (condition.Op.Equals("exists", StringComparison.OrdinalIgnoreCase)
          || string.IsNullOrWhiteSpace(condition.Value))
        return hasAttachment;
      return hasAttachment == !condition.Value.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    var text = field switch {
      "from" => from,
      "to" => to,
      "subject" => subject,
      "body" => body,
      _ => string.Join('\n', [from, to, subject, body])
    };
    var value = condition.Value ?? "";
    if (condition.Op.Equals("exists", StringComparison.OrdinalIgnoreCase))
      return !string.IsNullOrWhiteSpace(text);
    if (condition.Op.Equals("is", StringComparison.OrdinalIgnoreCase)
        || condition.Op.Equals("equals", StringComparison.OrdinalIgnoreCase))
      return text.Equals(value, StringComparison.OrdinalIgnoreCase);
    if (condition.Op.Equals("prefix", StringComparison.OrdinalIgnoreCase)
        || condition.Op.Equals("starts", StringComparison.OrdinalIgnoreCase))
      return text.StartsWith(value, StringComparison.OrdinalIgnoreCase);
    return text.Contains(value, StringComparison.OrdinalIgnoreCase);
  }
}
