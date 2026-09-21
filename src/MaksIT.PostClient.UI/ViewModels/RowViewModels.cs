using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;


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

  public static IReadOnlyList<ChoiceRow> CertifiedKinds =>
    [
      new() { Id = MailCertifiedKind.Ordinary, Title = UiLocale.Copy.CertifiedOrdinary },
      new() { Id = MailCertifiedKind.Pec, Title = UiLocale.Copy.CertifiedPec },
      new() { Id = MailCertifiedKind.Rem, Title = UiLocale.Copy.CertifiedRem }
    ];

  public static ChoiceRow Certified(string? id) =>
    CertifiedKinds.FirstOrDefault(p => p.Id == MailCertifiedKind.Normalize(id))
    ?? CertifiedKinds[0];

  public static IReadOnlyList<ChoiceRow> ProvidersFor(FeatureSettings features, string? keepId = null) {
    features ??= new FeatureSettings();
    var rows = Providers.Where(p => {
      if (p.Id is MailProvider.Imap or MailProvider.Gmail or MailProvider.Outlook)
        return true;
      if (MailProvider.IsRemPreset(p.Id))
        return features.IsEnabled(AppFeature.RemPresets);
      if (MailProvider.IsItalianPec(p.Id))
        return features.IsEnabled(AppFeature.PecPresets);
      return true;
    }).ToList();
    var keep = Provider(keepId);
    if (rows.All(p => p.Id != keep.Id))
      rows.Add(keep);
    return rows;
  }
}


public sealed partial class FolderRowViewModel : ObservableObject {
  public required string FullName { get; init; }

  public char Delimiter { get; init; }

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


public sealed partial class FolderNodeViewModel : ObservableObject {
  public bool IsAccount { get; init; }

  public MailboxAccount? Mailbox { get; init; }

  public FolderRowViewModel? Folder { get; init; }

  [ObservableProperty]
  private bool isExpanded = true;

  public ObservableCollection<FolderNodeViewModel> Children { get; } = [];

  public string Title =>
    IsAccount ? Mailbox?.Label ?? UiLocale.Copy.Mailbox : Folder?.Name ?? "";

  public string Glyph =>
    IsAccount ? "✉" : MailFolderRole.Glyph(Folder?.Name, Folder?.FullName);

  public int Unread =>
    IsAccount ? ChildUnread() : Folder?.Unread ?? 0;

  private int ChildUnread() {
    var n = 0;
    foreach (var child in Children) {
      n += child.Folder?.Unread ?? 0;
      n += child.ChildUnread();
    }

    return n;
  }

  public string UnreadLabel =>
    Unread > 0 ? Unread.ToString() : "";

  public bool HasUnread =>
    Unread > 0;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(RetentionLabel))]
  [NotifyPropertyChangedFor(nameof(HasRetention))]
  private int retentionDays;

  public string RetentionLabel =>
    UiLocale.Copy.RetentionBadge(RetentionDays);

  public bool HasRetention =>
    RetentionDays > 0;

  public MailboxQuota? Quota { get; set; }

  public bool ShowQuota =>
    IsAccount && Quota?.Percent is int;

  public double QuotaPercent =>
    Quota?.Percent ?? 0;

  public string QuotaTip =>
    Quota?.Line() ?? "";

  public FontWeight Weight =>
    HasUnread ? FontWeight.SemiBold : FontWeight.Normal;

  public void NotifyCounts() {
    OnPropertyChanged(nameof(Title));
    OnPropertyChanged(nameof(Unread));
    OnPropertyChanged(nameof(UnreadLabel));
    OnPropertyChanged(nameof(HasUnread));
    OnPropertyChanged(nameof(Weight));
    OnPropertyChanged(nameof(RetentionLabel));
    OnPropertyChanged(nameof(HasRetention));
  }

  public void NotifyQuota() {
    OnPropertyChanged(nameof(Quota));
    OnPropertyChanged(nameof(ShowQuota));
    OnPropertyChanged(nameof(QuotaPercent));
    OnPropertyChanged(nameof(QuotaTip));
  }

  public static FolderNodeViewModel Account(MailboxAccount box, bool expanded = true) =>
    new() {
      IsAccount = true,
      Mailbox = box,
      IsExpanded = expanded
    };

  public static FolderNodeViewModel ForFolder(MailboxAccount box, FolderRowViewModel folder, bool expanded = true) {
    var node = new FolderNodeViewModel {
      Mailbox = box,
      Folder = folder,
      IsExpanded = expanded
    };
    folder.PropertyChanged += (_, _) => node.NotifyCounts();
    return node;
  }
}


public sealed class MessageRowViewModel : ObservableObject {
  public string MailboxId { get; set; } = "";

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

  public long Size =>
    Header.Size;

  public string SizeLine =>
    MailSize.Line(Header.Size);

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

  public MailMessageKey Key =>
    MailMessageKey.Of(MailboxId, Header.Folder, Header.Id);

  public void ApplyHeader(MailMessageHeader header) {
    ArgumentNullException.ThrowIfNull(header);
    Header = header;
    RefreshMarks();
  }

  public void RefreshMarks() {
    OnPropertyChanged(nameof(Subject));
    OnPropertyChanged(nameof(ChainSubject));
    OnPropertyChanged(nameof(From));
    OnPropertyChanged(nameof(When));
    OnPropertyChanged(nameof(SortDate));
    OnPropertyChanged(nameof(UnreadSort));
    OnPropertyChanged(nameof(FlagSort));
    OnPropertyChanged(nameof(PrioritySort));
    OnPropertyChanged(nameof(AttachmentSort));
    OnPropertyChanged(nameof(DeliverySort));
    OnPropertyChanged(nameof(Badge));
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
    OnPropertyChanged(nameof(BadgeTip));
    OnPropertyChanged(nameof(Labels));
    OnPropertyChanged(nameof(Weight));
  }
}
