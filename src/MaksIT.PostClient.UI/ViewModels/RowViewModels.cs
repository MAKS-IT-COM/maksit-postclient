using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MaksIT.PostClient.Client;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.UI.ViewModels;


public sealed class ChoiceRow {
  public required string Id { get; init; }

  public required string Title { get; init; }

  public override string ToString() =>
    Title;

  public static IReadOnlyList<ChoiceRow> IncomingProtocols { get; } = [
    new() { Id = MailProtocol.Imap, Title = "IMAP" },
    new() { Id = MailProtocol.Pop3, Title = "POP3" }
  ];

  public static IReadOnlyList<ChoiceRow> SecurityModes =>
    [
      new() { Id = MailSecurity.Auto, Title = UiLocale.Copy.SecurityAuto },
      new() { Id = MailSecurity.Ssl, Title = "SSL/TLS" },
      new() { Id = MailSecurity.StartTls, Title = "STARTTLS" },
      new() { Id = MailSecurity.StartTlsWhenAvailable, Title = UiLocale.Copy.SecurityStartTlsIfAvailable },
      new() { Id = MailSecurity.None, Title = UiLocale.Copy.SecurityNone }
    ];

  public static IReadOnlyList<ChoiceRow> Providers { get; } = [
    new() { Id = MailProvider.Imap, Title = "IMAP / POP3" },
    new() { Id = MailProvider.Gmail, Title = "Gmail" },
    new() { Id = MailProvider.Outlook, Title = "Outlook / Microsoft 365" },
    new() { Id = MailProvider.Aruba, Title = "IT PEC — Aruba" },
    new() { Id = MailProvider.Legalmail, Title = "IT PEC — InfoCert Legalmail" },
    new() { Id = MailProvider.Namirial, Title = "IT/EU PEC — Namirial" },
    new() { Id = MailProvider.Postecert, Title = "IT PEC — Poste Italiane" },
    new() { Id = MailProvider.Register, Title = "IT PEC — Register.it" },
    new() { Id = MailProvider.Libero, Title = "IT PEC — Libero" },
    new() { Id = MailProvider.Intesi, Title = "EU — Intesi Group" }
  ];

  public static ChoiceRow Incoming(string? id) =>
    IncomingProtocols.FirstOrDefault(p => p.Id == MailProtocol.NormalizeIncoming(id))
    ?? IncomingProtocols[0];

  public static ChoiceRow Security(string? id, bool legacySsl = true) =>
    SecurityModes.FirstOrDefault(p => p.Id == MailSecurity.Normalize(id, legacySsl))
    ?? SecurityModes[1];

  public static ChoiceRow Provider(string? id) =>
    Providers.FirstOrDefault(p => p.Id == MailProvider.Normalize(id))
    ?? Providers[0];
}


public sealed partial class FolderRowViewModel : ObservableObject {
  public required string FullName { get; init; }

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(Line))]
  private string name = "";

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(Line))]
  [NotifyPropertyChangedFor(nameof(UnreadLabel))]
  [NotifyPropertyChangedFor(nameof(HasUnread))]
  private int unread;

  [ObservableProperty]
  private int total;

  public string Line =>
    HasUnread ? $"{Name} ({Unread})" : Name;

  public string UnreadLabel =>
    Unread > 0 ? Unread.ToString() : "";

  public bool HasUnread =>
    Unread > 0;
}


public sealed class FolderNodeViewModel : ObservableObject {
  public bool IsAccount { get; init; }

  public MailboxAccount? Mailbox { get; init; }

  public FolderRowViewModel? Folder { get; init; }

  public bool IsExpanded { get; set; } = true;

  public ObservableCollection<FolderNodeViewModel> Children { get; } = [];

  public string Title =>
    IsAccount ? Mailbox?.Label ?? UiLocale.Copy.Mailbox : Folder?.Name ?? "";

  public string Glyph =>
    IsAccount ? "✉" : MailFolderRole.Glyph(Folder?.Name, Folder?.FullName);

  public int Unread =>
    IsAccount ? Children.Sum(c => c.Unread) : Folder?.Unread ?? 0;

  public string UnreadLabel =>
    Unread > 0 ? Unread.ToString() : "";

  public bool HasUnread =>
    Unread > 0;

  public FontWeight Weight =>
    HasUnread ? FontWeight.SemiBold : FontWeight.Normal;

  public void NotifyCounts() {
    OnPropertyChanged(nameof(Title));
    OnPropertyChanged(nameof(Unread));
    OnPropertyChanged(nameof(UnreadLabel));
    OnPropertyChanged(nameof(HasUnread));
    OnPropertyChanged(nameof(Weight));
  }

  public static FolderNodeViewModel Account(MailboxAccount box) =>
    new() {
      IsAccount = true,
      Mailbox = box,
      IsExpanded = true
    };

  public static FolderNodeViewModel ForFolder(MailboxAccount box, FolderRowViewModel folder) {
    var node = new FolderNodeViewModel {
      Mailbox = box,
      Folder = folder
    };
    folder.PropertyChanged += (_, _) => node.NotifyCounts();
    return node;
  }
}


public sealed class MessageRowViewModel : ObservableObject {
  public required MailMessageHeader Header { get; set; }

  public string Subject =>
    string.IsNullOrWhiteSpace(Header.Subject) ? UiLocale.Copy.NoSubject : Header.Subject;

  public int ChainDepth { get; private set; }

  public int ChainSize { get; private set; } = 1;

  public string ChainSubject {
    get {
      if (ChainDepth > 0)
        return "↳ " + Subject;
      if (ChainSize > 1)
        return Subject + "  ·  " + ChainSize;
      return Subject;
    }
  }

  public Thickness SubjectPad =>
    new(6 + ChainDepth * 12, 0, 6, 0);

  public void SetChain(int depth, int size) {
    ChainDepth = Math.Max(0, depth);
    ChainSize = Math.Max(1, size);
    OnPropertyChanged(nameof(ChainSubject));
    OnPropertyChanged(nameof(SubjectPad));
  }

  public string From =>
    Header.From;

  public string When =>
    MailWhen.Line(Header.Date);

  public DateTimeOffset SortDate =>
    Header.Date;

  public int UnreadSort =>
    Header.IsSeen ? 1 : 0;

  public int FlagSort =>
    Header.IsFlagged ? 0 : 1;

  public int PrioritySort =>
    Header.Priority == MailPriority.High ? 0 : Header.Priority == MailPriority.Low ? 2 : 1;

  public int AttachmentSort =>
    Header.HasAttachments ? 0 : 1;

  public int DeliverySort =>
    ReceiptStatus.Rank(Header.DeliveryStatus);

  public string Badge =>
    Header.EnvelopeBadge;

  public string UnreadMark =>
    Header.IsSeen ? "" : "●";

  public string UnreadTip =>
    Header.IsSeen ? UiLocale.Copy.Read : UiLocale.Copy.Unread;

  public string AttachmentMark =>
    Header.HasAttachments ? "📎" : "";

  public string AttachmentTip =>
    Header.HasAttachments ? UiLocale.Copy.HasAttachments : UiLocale.Copy.NoAttachments;

  public string FlagMark =>
    Header.IsFlagged ? "★" : "☆";

  public string FlagTip =>
    Header.IsFlagged ? UiLocale.Copy.Flagged : UiLocale.Copy.NotFlagged;

  public string DeliveryMark =>
    ReceiptStatus.Mark(Header.DeliveryStatus);

  public string DeliveryLabel =>
    ReceiptStatus.Label(Header.DeliveryStatus);

  public string DeliveryTip =>
    string.IsNullOrWhiteSpace(DeliveryLabel) ? UiLocale.Copy.NoDelivery : DeliveryLabel;

  public string PriorityMark =>
    MailPriority.Mark(Header.Priority);

  public string PriorityTip =>
    MailPriority.Label(Header.Priority);

  public string BadgeTip =>
    EnvelopeKind.Label(Header.EnvelopeKind);

  public string Labels =>
    Header.Labels;

  public FontWeight Weight =>
    Header.IsSeen ? FontWeight.Normal : FontWeight.SemiBold;

  public void RefreshMarks() {
    OnPropertyChanged(nameof(Subject));
    OnPropertyChanged(nameof(ChainSubject));
    OnPropertyChanged(nameof(UnreadMark));
    OnPropertyChanged(nameof(UnreadTip));
    OnPropertyChanged(nameof(FlagMark));
    OnPropertyChanged(nameof(FlagTip));
    OnPropertyChanged(nameof(PriorityMark));
    OnPropertyChanged(nameof(PriorityTip));
    OnPropertyChanged(nameof(AttachmentMark));
    OnPropertyChanged(nameof(AttachmentTip));
    OnPropertyChanged(nameof(DeliveryMark));
    OnPropertyChanged(nameof(DeliveryLabel));
    OnPropertyChanged(nameof(DeliveryTip));
    OnPropertyChanged(nameof(Labels));
    OnPropertyChanged(nameof(Weight));
  }
}
