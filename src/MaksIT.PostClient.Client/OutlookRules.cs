using System.Text.Json;
using XstReader;
using XstReader.ElementProperties;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public static class OutlookRules {
  public static IReadOnlyList<MailRule> FromFile(string path) {
    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
      return [];
    var bytes = File.ReadAllBytes(path);
    return FromBytes(bytes, path);
  }

  public static IReadOnlyList<MailRule> FromBytes(byte[] bytes, string? name = null) {
    if (bytes.Length == 0)
      return [];
    if (LooksJson(bytes))
      return FromJson(bytes);
    if (PstFile.IsName(name) || PstFile.IsStore(bytes))
      return FromPstBytes(bytes, name);
    if (OleCompound.IsOle(bytes))
      return FromRwz(bytes);
    return OutlookRwz.Parse(bytes);
  }

  public static byte[] ToJson(IEnumerable<MailRule> rules) {
    var list = (rules ?? []).Where(r => r.HasWork).ToList();
    return JsonSerializer.SerializeToUtf8Bytes(list, new JsonSerializerOptions {
      WriteIndented = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });
  }

  public static IReadOnlyList<MailRule> FromPst(string path) {
    using var file = new XstFile(path);
    var folders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    CollectFolders(file.RootFolder, folders);
    return Walk(file.RootFolder, folders);
  }

  private static IReadOnlyList<MailRule> FromPstBytes(byte[] bytes, string? name) {
    var path = PstFile.TempPath(name);
    try {
      File.WriteAllBytes(path, bytes);
      return FromPst(path);
    }
    catch {
      return [];
    }
    finally {
      if (File.Exists(path))
        File.Delete(path);
    }
  }

  private static List<MailRule> Walk(XstFolder folder, Dictionary<string, string> folders) {
    var rules = new List<MailRule>();
    foreach (var message in folder.Messages) {
      var parsed = FromMessage(message, folders);
      if (parsed is not null)
        rules.Add(parsed);
    }

    foreach (var child in folder.Folders)
      rules.AddRange(Walk(child, folders));
    return rules;
  }

  private static MailRule? FromMessage(XstMessage message, Dictionary<string, string> folders) {
    var cls = Text(message, PropertyCanonicalName.PidTagMessageClass);
    var name = Text(message, PropertyCanonicalName.PidTagRuleName)
      ?? Text(message, PropertyCanonicalName.PidTagRuleMessageName)
      ?? message.Subject;
    var isRule = (cls ?? "").Contains("rule", StringComparison.OrdinalIgnoreCase)
      || message.Properties.Contains(PropertyCanonicalName.PidTagRuleCondition)
      || message.Properties.Contains(PropertyCanonicalName.PidTagRuleMessageName)
      || message.Properties.Contains(PropertyCanonicalName.PidTagExtendedRuleMessageCondition);
    if (!isRule)
      return null;
    var condition = Bytes(message, PropertyCanonicalName.PidTagRuleCondition)
      ?? Bytes(message, PropertyCanonicalName.PidTagExtendedRuleMessageCondition);
    var actions = Bytes(message, PropertyCanonicalName.PidTagRuleActions)
      ?? Bytes(message, PropertyCanonicalName.PidTagExtendedRuleMessageActions);
    var sequence = Number(message, PropertyCanonicalName.PidTagRuleSequence)
      ?? Number(message, PropertyCanonicalName.PidTagRuleMessageSequence)
      ?? 0;
    var rule = MapiRestriction.Parse(name ?? "Outlook rule", condition, actions, sequence);
    if (rule is null)
      return null;
    if (string.IsNullOrWhiteSpace(rule.Folder))
      rule.Folder = Text(message, PropertyCanonicalName.PidTagRuleFolderEntryId) ?? "";
    if (!string.IsNullOrWhiteSpace(rule.Folder) && folders.TryGetValue(rule.Folder, out var mapped))
      rule.Folder = mapped;
    var state = Number(message, PropertyCanonicalName.PidTagRuleState)
      ?? Number(message, PropertyCanonicalName.PidTagRuleMessageState);
    if (state is int flags)
      rule.Enabled = (flags & 0x01) != 0;
    return rule.HasWork ? rule : null;
  }

  private static void CollectFolders(XstFolder folder, Dictionary<string, string> map) {
    var name = folder.DisplayName ?? "";
    TryMap(folder, PropertyCanonicalName.PidTagEntryId, name, map);
    TryMap(folder, PropertyCanonicalName.PidTagFolderId, name, map);
    foreach (var child in folder.Folders)
      CollectFolders(child, map);
  }

  private static void TryMap(
    XstFolder folder,
    PropertyCanonicalName tag,
    string name,
    Dictionary<string, string> map) {
    var value = folder.PropertyValue(tag);
    if (value is byte[] bytes && bytes.Length > 0)
      map[Convert.ToHexString(bytes)] = name;
    else if (value is not null)
      map[value.ToString() ?? ""] = name;
  }

  private static IReadOnlyList<MailRule> FromRwz(byte[] bytes) {
    var rules = new List<MailRule>();
    var index = 1;
    foreach (var stream in OleCompound.StreamBytes(bytes)) {
      foreach (var rule in MapiRestriction.Scan(stream, "Outlook rule " + index)) {
        rules.Add(rule);
        index++;
      }
    }

    if (rules.Count == 0) {
      foreach (var rule in MapiRestriction.Scan(bytes, "Outlook rule"))
        rules.Add(rule);
    }

    return rules;
  }

  private static IReadOnlyList<MailRule> FromJson(byte[] bytes) {
    try {
      var json = JsonSerializer.Deserialize<List<MailRule>>(bytes, new JsonSerializerOptions {
        PropertyNameCaseInsensitive = true
      });
      return json?.Where(r => r.HasWork).ToList() ?? [];
    }
    catch {
      return [];
    }
  }

  private static bool LooksJson(byte[] bytes) {
    foreach (var b in bytes) {
      if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        continue;
      return b is (byte)'[' or (byte)'{';
    }

    return false;
  }

  private static string? Text(XstElement element, PropertyCanonicalName tag) {
    var value = element.PropertyValue(tag);
    return value switch {
      string text when !string.IsNullOrWhiteSpace(text) => text.Trim(),
      byte[] raw when raw.Length > 0 => Convert.ToHexString(raw),
      _ => value?.ToString()
    };
  }

  private static byte[]? Bytes(XstElement element, PropertyCanonicalName tag) {
    return element.PropertyValue(tag) as byte[];
  }

  private static int? Number(XstElement element, PropertyCanonicalName tag) {
    return element.PropertyValue(tag) switch {
      int n => n,
      uint n => (int)n,
      short n => n,
      long n => (int)n,
      _ => null
    };
  }
}
