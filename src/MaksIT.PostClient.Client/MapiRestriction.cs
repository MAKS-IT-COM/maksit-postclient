using System.Text;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


internal static class MapiRestriction {
  public const int ResAnd = 0x00;
  public const int ResOr = 0x01;
  public const int ResNot = 0x02;
  public const int ResContent = 0x03;
  public const int ResProperty = 0x04;
  public const int ResExist = 0x08;

  public const int FlSubstring = 0x0001;
  public const int FlPrefix = 0x0002;

  public const ushort PrSubject = 0x0037;
  public const ushort PrSenderName = 0x0C1A;
  public const ushort PrSenderEmail = 0x0C1F;
  public const ushort PrSentRepresentingName = 0x0042;
  public const ushort PrSentRepresentingEmail = 0x0065;
  public const ushort PrDisplayTo = 0x0E04;
  public const ushort PrEmailAddress = 0x3003;
  public const ushort PrSmtpAddress = 0x39FE;
  public const ushort PrBody = 0x1000;
  public const ushort PrHasAttach = 0x0E1B;
  public const ushort PrDisplayName = 0x3001;

  public const byte OpMove = 0x01;
  public const byte OpCopy = 0x02;
  public const byte OpDelete = 0x0A;
  public const byte OpMarkRead = 0x0B;
  public const byte OpTag = 0x09;

  public static MailRule? Parse(string name, byte[]? condition, byte[]? actions, int sequence = 0) {
    var rule = new MailRule {
      Name = string.IsNullOrWhiteSpace(name) ? "Outlook rule" : name.Trim(),
      Sequence = sequence
    };
    if (condition is { Length: > 0 }) {
      var offset = 0;
      ParseRestriction(condition, ref offset, rule);
    }

    if (actions is { Length: > 0 })
      ParseActions(actions, rule);
    return rule.HasWork ? rule : null;
  }

  public static IReadOnlyList<MailRule> Scan(byte[] blob, string fallbackName) {
    var found = new List<MailRule>();
    if (blob is not { Length: >= 8 })
      return found;
    for (var i = 0; i < blob.Length - 4; i++) {
      if (blob[i] is not ResAnd and not ResOr and not ResNot and not ResContent and not ResProperty and not ResExist)
        continue;
      var offset = i;
      var rule = new MailRule { Name = fallbackName };
      if (!ParseRestriction(blob, ref offset, rule) || rule.Conditions.Count == 0)
        continue;
      ParseActions(blob.AsSpan(offset).ToArray(), rule);
      if (rule.HasWork)
        found.Add(rule);
      i = Math.Max(i, offset - 1);
    }

    return found;
  }

  private static bool ParseRestriction(byte[] data, ref int offset, MailRule rule) {
    if (offset >= data.Length)
      return false;
    var kind = data[offset++];
    switch (kind) {
      case ResAnd:
      case ResOr:
        rule.Logic = kind == ResOr ? "or" : "and";
        if (!TryReadUInt16(data, ref offset, out var count))
          return false;
        for (var i = 0; i < count; i++) {
          if (!ParseRestriction(data, ref offset, rule))
            return false;
        }

        return true;
      case ResNot:
        return ParseRestriction(data, ref offset, rule);
      case ResContent:
        if (!TryReadInt32(data, ref offset, out var fuzzy) || !TryReadUInt32(data, ref offset, out var tag))
          return false;
        var text = ReadTaggedString(data, ref offset, tag);
        if (string.IsNullOrWhiteSpace(text))
          return true;
        rule.Conditions.Add(new MailRuleCondition {
          Field = FieldFor(tag),
          Op = (fuzzy & FlPrefix) != 0 ? "prefix" : "contains",
          Value = text
        });
        return true;
      case ResProperty:
        if (!TryReadByte(data, ref offset, out _) || !TryReadUInt32(data, ref offset, out var prop))
          return false;
        var value = ReadTaggedString(data, ref offset, prop);
        if (FieldFor(prop) == "attachment") {
          rule.Conditions.Add(new MailRuleCondition { Field = "attachment", Op = "exists" });
          return true;
        }

        if (!string.IsNullOrWhiteSpace(value)) {
          rule.Conditions.Add(new MailRuleCondition {
            Field = FieldFor(prop),
            Op = "is",
            Value = value
          });
        }

        return true;
      case ResExist:
        if (!TryReadUInt32(data, ref offset, out var exist))
          return false;
        if ((ushort)exist == PrHasAttach)
          rule.Conditions.Add(new MailRuleCondition { Field = "attachment", Op = "exists" });
        return true;
      default:
        return false;
    }
  }

  private static void ParseActions(byte[] data, MailRule rule) {
    if (!TryActions(data, 0, rule) && data.Length > 2)
      TryActions(data, 2, rule);
  }

  private static bool TryActions(byte[] data, int offset, MailRule rule) {
    if (!TryReadUInt16(data, ref offset, out var count) || count == 0 || count > 32)
      return false;
    var applied = false;
    for (var i = 0; i < count && offset < data.Length; i++) {
      if (!TryReadByte(data, ref offset, out var type))
        return applied;
      offset = Math.Min(data.Length, offset + 8);
      switch (type) {
        case OpMove:
        case OpCopy:
          rule.Action = MailRuleAction.Move;
          var folder = ReadCountedUnicode(data, ref offset) ?? ReadLooseUnicode(data, offset);
          if (!string.IsNullOrWhiteSpace(folder))
            rule.Folder = folder;
          applied = true;
          break;
        case OpDelete:
          rule.Action = MailRuleAction.Delete;
          applied = true;
          break;
        case OpMarkRead:
          if (string.IsNullOrWhiteSpace(rule.Action))
            rule.Action = MailRuleAction.MarkRead;
          applied = true;
          break;
        case OpTag:
          if (string.IsNullOrWhiteSpace(rule.Action))
            rule.Action = MailRuleAction.Flag;
          applied = true;
          break;
        default:
          return applied;
      }
    }

    return applied;
  }

  private static string FieldFor(uint tag) {
    var id = (ushort)tag;
    return id switch {
      PrSubject => "subject",
      PrSenderName or PrSenderEmail or PrSentRepresentingName or PrSentRepresentingEmail
        or PrEmailAddress or PrSmtpAddress or PrDisplayName => "from",
      PrDisplayTo => "to",
      PrBody => "body",
      PrHasAttach => "attachment",
      _ => "any"
    };
  }

  private static string ReadTaggedString(byte[] data, ref int offset, uint tag) {
    var type = (ushort)(tag >> 16);
    if (type == 0x000B) {
      TryReadByte(data, ref offset, out _);
      return "";
    }

    if (type is 0x001F or 0x101F)
      return ReadUnicode(data, ref offset);
    if (type is 0x001E or 0x101E)
      return ReadAnsi(data, ref offset);
    if (type == 0x0003) {
      TryReadInt32(data, ref offset, out _);
      return "";
    }

    return ReadUnicode(data, ref offset);
  }

  private static string ReadUnicode(byte[] data, ref int offset) {
    var start = offset;
    while (offset + 1 < data.Length && (data[offset] != 0 || data[offset + 1] != 0))
      offset += 2;
    var text = Encoding.Unicode.GetString(data, start, Math.Max(0, offset - start)).Trim('\0').Trim();
    if (offset + 1 < data.Length)
      offset += 2;
    return text;
  }

  private static string ReadAnsi(byte[] data, ref int offset) {
    var start = offset;
    while (offset < data.Length && data[offset] != 0)
      offset++;
    var text = Encoding.Latin1.GetString(data, start, Math.Max(0, offset - start)).Trim();
    if (offset < data.Length)
      offset++;
    return text;
  }

  private static string? ReadCountedUnicode(byte[] data, ref int offset) {
    if (!TryReadInt32(data, ref offset, out var size) || size <= 0 || offset + size > data.Length)
      return null;
    var slice = data.AsSpan(offset, size);
    offset += size;
    if (size >= 2 && slice[1] == 0)
      return Encoding.Unicode.GetString(slice).Trim('\0').Trim();
    return Encoding.Latin1.GetString(slice).Trim('\0').Trim();
  }

  private static string? ReadLooseUnicode(byte[] data, int offset) {
    for (var i = offset; i + 5 < data.Length; i++) {
      if (data[i] == 0 || data[i + 1] != 0)
        continue;
      var end = i;
      while (end + 1 < data.Length && (data[end] != 0 || data[end + 1] != 0))
        end += 2;
      if (end - i < 4)
        continue;
      var text = Encoding.Unicode.GetString(data, i, end - i).Trim();
      if (text.Length >= 2 && text.Any(char.IsLetter))
        return text;
    }

    return null;
  }

  private static bool TryReadByte(byte[] data, ref int offset, out byte value) {
    value = 0;
    if (offset >= data.Length)
      return false;
    value = data[offset++];
    return true;
  }

  private static bool TryReadUInt16(byte[] data, ref int offset, out ushort value) {
    value = 0;
    if (offset + 1 >= data.Length)
      return false;
    value = BitConverter.ToUInt16(data, offset);
    offset += 2;
    return true;
  }

  private static bool TryReadInt32(byte[] data, ref int offset, out int value) {
    value = 0;
    if (offset + 3 >= data.Length)
      return false;
    value = BitConverter.ToInt32(data, offset);
    offset += 4;
    return true;
  }

  private static bool TryReadUInt32(byte[] data, ref int offset, out uint value) {
    value = 0;
    if (offset + 3 >= data.Length)
      return false;
    value = BitConverter.ToUInt32(data, offset);
    offset += 4;
    return true;
  }
}
