using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public sealed class MailFolderInfo {
  public required string FullName { get; init; }

  public required string Name { get; init; }

  public int Unread { get; init; }

  public int Total { get; init; }

  public string Kind { get; init; } = "";

  public char Delimiter { get; init; }
}


public sealed class MailMessageHeader {
  public required uint Id { get; init; }

  public required string Folder { get; init; }

  public string Subject { get; init; } = "";

  public string From { get; init; } = "";

  public DateTimeOffset Date { get; init; }

  public bool IsSeen { get; set; }

  public bool IsFlagged { get; set; }

  public bool HasAttachments { get; init; }

  public string Priority { get; set; } = MailPriority.Normal;

  public string EnvelopeKind { get; init; } = MaksIT.PostClient.Shared.EnvelopeKind.Ordinary;

  public string EnvelopeBadge { get; init; } = "";

  public string EnvelopeTipo { get; init; } = "";

  public string MessageId { get; init; } = "";

  public string InReplyTo { get; init; } = "";

  public string DeliveryStatus { get; set; } = "";

  public string Labels { get; set; } = "";
}


public sealed class MailMessageBody {
  public required MailMessageHeader Header { get; init; }

  public string Text { get; init; } = "";

  public string Html { get; init; } = "";

  public IReadOnlyList<string> Attachments { get; init; } = [];

  public byte[] RawEml { get; init; } = [];

  public EnvelopeInfo Envelope { get; init; } = new();

  public string InnerSubject { get; init; } = "";

  public string InnerFrom { get; init; } = "";

  public string To { get; init; } = "";

  public string Cc { get; init; } = "";

  public string InnerTo { get; init; } = "";

  public string InnerCc { get; init; } = "";

  public string InnerText { get; init; } = "";

  public string InnerHtml { get; init; } = "";

  public IReadOnlyList<string> InnerAttachments { get; init; } = [];

  public IReadOnlyList<MailFileAttachment> AttachmentFiles { get; init; } = [];

  public IReadOnlyList<MailFileAttachment> InnerAttachmentFiles { get; init; } = [];
}


public sealed class MailFileAttachment {
  public required string Name { get; init; }

  public required byte[] Bytes { get; init; }

  public string ContentType { get; init; } = "application/octet-stream";

  public string ContentId { get; init; } = "";

  public string Line {
    get {
      var size = Bytes.Length < 1024
        ? $"{Bytes.Length} B"
        : Bytes.Length < 1024 * 1024
          ? $"{Bytes.Length / 1024.0:0.#} KB"
          : $"{Bytes.Length / (1024.0 * 1024):0.#} MB";
      return $"{Name} ({size})";
    }
  }

  public string Glyph {
    get {
      var type = ContentType ?? "";
      if (type.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || HasExtension(".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".tif", ".tiff"))
        return "🖼";
      if (type.Contains("pdf", StringComparison.OrdinalIgnoreCase) || HasExtension(".pdf"))
        return "📄";
      if (type.Contains("xml", StringComparison.OrdinalIgnoreCase) || HasExtension(".xml"))
        return "📋";
      if (type.Contains("zip", StringComparison.OrdinalIgnoreCase) || HasExtension(".zip", ".7z", ".rar"))
        return "📦";
      if (type.Contains("rfc822", StringComparison.OrdinalIgnoreCase) || HasExtension(".eml", ".msg"))
        return "✉";
      if (type.Contains("pkcs7", StringComparison.OrdinalIgnoreCase) || HasExtension(".p7m", ".p7s"))
        return "🔏";
      if (IsPst)
        return "📫";
      if (IsFatturaPa)
        return "📋";
      return "📎";
    }
  }

  public bool IsFatturaPa =>
    FatturaPaDocument.TryParse(Bytes) is not null;

  public bool IsPst =>
    PstFile.IsName(Name)
    || (ContentType ?? "").Contains("ms-outlook", StringComparison.OrdinalIgnoreCase)
    || PstFile.IsStore(Bytes);

  private bool HasExtension(params string[] extensions) {
    var name = Name ?? "";
    foreach (var extension in extensions) {
      if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        return true;
    }

    return false;
  }
}


public sealed class MailSendRequest {
  public required string From { get; init; }

  public IReadOnlyList<MailRecipient> To { get; init; } = [];

  public IReadOnlyList<MailRecipient> Cc { get; init; } = [];

  public IReadOnlyList<MailRecipient> Bcc { get; init; } = [];

  public required string Subject { get; init; }

  public required string BodyText { get; init; }

  public string InReplyTo { get; init; } = "";

  public string References { get; init; } = "";

  public IReadOnlyList<MailFileAttachment> Attachments { get; init; } = [];
}


public sealed class MailFlagUpdate {
  public bool? Seen { get; init; }

  public bool? Flagged { get; init; }

  public bool Deleted { get; init; }

  public string? Priority { get; init; }
}


public sealed class MailFlagState {
  public uint Id { get; init; }

  public bool IsSeen { get; init; }

  public bool IsFlagged { get; init; }
}


public sealed class MailFolderSync {
  public IReadOnlyList<MailMessageHeader> Headers { get; init; } = [];

  public IReadOnlyList<MailFlagState> Flags { get; init; } = [];

  public IReadOnlyList<uint>? Present { get; init; }

  public bool Incomplete { get; init; }
}
