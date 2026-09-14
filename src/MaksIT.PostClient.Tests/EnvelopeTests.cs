using MimeKit;
using MaksIT.PostClient.Client;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Tests;


public class EnvelopeClassifierTests {
  [Fact]
  public void TransportHeader_IsItalianPec() {
    var info = EnvelopeClassifier.Classify(
      [new KeyValuePair<string, string>("X-Trasporto", "posta-certificata")],
      ["daticert.xml", "postacert.eml"]);
    Assert.Equal(EnvelopeKind.PecTransport, info.Kind);
    Assert.Equal(EnvelopeRegion.Italy, info.Region);
    Assert.Equal("PEC", info.Badge);
    Assert.True(info.HasInnerMessage);
  }

  [Fact]
  public void RicevutaHeader_IsItalianReceipt() {
    var info = EnvelopeClassifier.Classify(
      [new KeyValuePair<string, string>("X-Ricevuta", "accettazione")],
      ["daticert.xml"]);
    Assert.Equal(EnvelopeKind.PecReceipt, info.Kind);
    Assert.Equal(EnvelopeRegion.Italy, info.Region);
    Assert.Equal("RIC", info.Badge);
    Assert.False(info.HasInnerMessage);
    Assert.Equal(ReceiptStatus.Accepted, info.EventStatus);
  }

  [Fact]
  public void RiferimentoHeader_IsOriginalMessageId() {
    var info = EnvelopeClassifier.Classify(
      [
        new KeyValuePair<string, string>("X-Ricevuta", "avvenuta-consegna"),
        new KeyValuePair<string, string>("X-Riferimento-Message-ID", "<orig@studio.it>")
      ],
      ["daticert.xml"]);
    Assert.Equal(EnvelopeKind.PecReceipt, info.Kind);
    Assert.Equal("<orig@studio.it>", info.Msgid);
    Assert.Equal(ReceiptStatus.Delivered, info.EventStatus);
  }

  [Fact]
  public void EtsiXml_IsEuropeanRem() {
    var info = EnvelopeClassifier.Classify(
      [],
      ["evidence.xml"],
      "<REMEvidence xmlns=\"http://uri.etsi.org/02640/v2#\"></REMEvidence>");
    Assert.Equal(EnvelopeKind.EidasRem, info.Kind);
    Assert.Equal(EnvelopeRegion.Europe, info.Region);
    Assert.Equal("REM", info.Badge);
  }

  [Fact]
  public void Ordinary_HasNoBadge() {
    var info = EnvelopeClassifier.Classify([], ["invoice.pdf"]);
    Assert.Equal(EnvelopeKind.Ordinary, info.Kind);
    Assert.Equal("", info.Badge);
  }

  [Fact]
  public void Label_ExplainsKind() {
    Assert.Equal("Certified mail (PEC)", EnvelopeKind.Label(EnvelopeKind.PecTransport));
    Assert.Equal("PEC receipt", EnvelopeKind.Label(EnvelopeKind.PecReceipt));
    Assert.Equal("Certified mail (REM)", EnvelopeKind.Label(EnvelopeKind.EidasRem));
    Assert.Equal("Digitally signed", EnvelopeKind.Label(EnvelopeKind.SmimeSigned));
    Assert.Equal("Ordinary mail", EnvelopeKind.Label(EnvelopeKind.Ordinary));
  }
}


public class DaticertParserTests {
  [Fact]
  public void Parse_ReadsTipoGestoreId() {
    var xml = """
      <postacert tipo="posta-certificata" errore="nessuno">
        <intestazione>
          <mittente>a@pec.example.it</mittente>
          <oggetto>Fattura</oggetto>
        </intestazione>
        <dati>
          <gestore-emittente>Example PEC</gestore-emittente>
          <identificativo>id-1</identificativo>
          <msgid>&lt;orig@studio.it&gt;</msgid>
        </dati>
      </postacert>
      """;
    var data = DaticertParser.Parse(xml);
    Assert.NotNull(data);
    Assert.Equal("posta-certificata", data.Tipo);
    Assert.Equal("Example PEC", data.Gestore);
    Assert.Equal("id-1", data.Identificativo);
    Assert.Equal("a@pec.example.it", data.Mittente);
    Assert.Equal("<orig@studio.it>", data.Msgid);
  }
}


public class EnvelopeUnwrapperTests {
  [Fact]
  public void Inspect_UnwrapsPostacert() {
    var xml = """
      <postacert tipo="posta-certificata" errore="nessuno">
        <dati>
          <gestore-emittente>Example PEC</gestore-emittente>
          <identificativo>id-1</identificativo>
        </dati>
      </postacert>
      """;
    var inner = new MimeMessage();
    inner.From.Add(new MailboxAddress("Studio", "studio@example.it"));
    inner.Subject = "Inner letter";
    inner.Body = new TextPart("plain") { Text = "hello inner" };

    var mixed = new Multipart("mixed");
    mixed.Add(new TextPart("plain") { Text = "wrapper text from gestore" });
    mixed.Add(XmlPart("daticert.xml", xml));
    mixed.Add(Rfc822Part(inner, "postacert.eml"));

    var outer = new MimeMessage();
    outer.Headers.Add("X-Trasporto", "posta-certificata");
    outer.Subject = "POSTA CERTIFICATA: Inner letter";
    outer.From.Add(new MailboxAddress("", "posta-certificata@pec.example.it"));
    outer.Body = mixed;

    var parsed = EnvelopeUnwrapper.Inspect(outer);
    Assert.Equal(EnvelopeKind.PecTransport, parsed.Info.Kind);
    Assert.Equal(EnvelopeRegion.Italy, parsed.Info.Region);
    Assert.Equal("Example PEC", parsed.Info.Gestore);
    Assert.True(parsed.Info.HasInnerMessage);
    Assert.Equal("Inner letter", parsed.Inner?.Subject);
    Assert.Equal("hello inner", parsed.Inner?.TextBody);
  }

  [Fact]
  public void Inspect_ReceiptWithoutInner() {
    var xml = """
      <postacert tipo="accettazione" errore="nessuno">
        <dati>
          <gestore-emittente>Example PEC</gestore-emittente>
          <identificativo>id-2</identificativo>
        </dati>
      </postacert>
      """;
    var mixed = new Multipart("mixed");
    mixed.Add(new TextPart("plain") { Text = "accettazione" });
    mixed.Add(XmlPart("daticert.xml", xml));
    var outer = new MimeMessage();
    outer.Headers.Add("X-Ricevuta", "accettazione");
    outer.Body = mixed;
    var parsed = EnvelopeUnwrapper.Inspect(outer);
    Assert.Equal(EnvelopeKind.PecReceipt, parsed.Info.Kind);
    Assert.False(parsed.Info.HasInnerMessage);
    Assert.Null(parsed.Inner);
  }

  [Fact]
  public void Inspect_SavedPostclientObjects_IfPresent() {
    var dir = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "Postclient",
      "objects");
    if (!Directory.Exists(dir))
      return;
    var files = Directory.GetFiles(dir, "*.eml");
    if (files.Length == 0)
      return;
    foreach (var file in files) {
      var mime = MimeMessage.Load(file, TestContext.Current.CancellationToken);
      var parsed = EnvelopeUnwrapper.Inspect(mime);
      if (parsed.Info.Kind == EnvelopeKind.Ordinary)
        continue;
      if (parsed.Info.Kind is EnvelopeKind.PecTransport or EnvelopeKind.PecReceipt)
        Assert.Equal(EnvelopeRegion.Italy, parsed.Info.Region);
      if (parsed.Info.Kind == EnvelopeKind.EidasRem)
        Assert.Equal(EnvelopeRegion.Europe, parsed.Info.Region);
    }
  }

  private static MimePart XmlPart(string name, string xml) {
    var part = new MimePart("application", "xml") {
      Content = new MimeContent(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml))),
      FileName = name
    };
    part.ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = name };
    part.ContentType.Name = name;
    return part;
  }

  private static MessagePart Rfc822Part(MimeMessage inner, string name) {
    var part = new MessagePart { Message = inner };
    part.ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = name };
    part.ContentType.Name = name;
    return part;
  }
}


public class ReceiptStatusTests {
  [Fact]
  public void FromTipo_PecDeliveryIsNotRead() {
    Assert.Equal(ReceiptStatus.Accepted, ReceiptStatus.FromTipo("accettazione"));
    Assert.Equal(ReceiptStatus.Delivered, ReceiptStatus.FromTipo("avvenuta-consegna"));
    Assert.Equal(ReceiptStatus.Failed, ReceiptStatus.FromTipo("mancata-consegna"));
    Assert.Equal(
      "Delivered to the recipient certified mailbox (not a read receipt).",
      ReceiptStatus.Explain("avvenuta-consegna"));
  }

  [Fact]
  public void FromTipo_RemRetrievalIsReadLike() {
    Assert.Equal(ReceiptStatus.Accepted, ReceiptStatus.FromTipo("SubmissionAcceptance"));
    Assert.Equal(ReceiptStatus.Delivered, ReceiptStatus.FromTipo("Delivery"));
    Assert.Equal(ReceiptStatus.Retrieved, ReceiptStatus.FromTipo("ContentConsignment"));
    Assert.Equal(ReceiptStatus.Retrieved, ReceiptStatus.FromTipo("Abholbestätigung"));
    Assert.Equal(ReceiptStatus.Delivered, ReceiptStatus.FromTipo("avis-de-reception"));
  }

  [Fact]
  public void Combine_DoesNotDowngradeDeliveredToAccepted() {
    var status = ReceiptStatus.Combine(ReceiptStatus.Submitted, ReceiptStatus.Accepted);
    status = ReceiptStatus.Combine(status, ReceiptStatus.Delivered);
    Assert.Equal(ReceiptStatus.Delivered, ReceiptStatus.Combine(status, ReceiptStatus.Accepted));
  }
}


public class RemEvidenceParserTests {
  [Fact]
  public void Parse_ReadsEventAndRelatedId() {
    var xml = """
      <REMEvidence xmlns="http://uri.etsi.org/02640/v2#">
        <EventCode>ContentConsignment</EventCode>
        <RelatesTo>orig@studio.it</RelatesTo>
        <EvidenceIdentifier>ev-1</EvidenceIdentifier>
      </REMEvidence>
      """;
    var data = RemEvidenceParser.Parse(xml);
    Assert.NotNull(data);
    Assert.Equal("ContentConsignment", data.Event);
    Assert.Equal("orig@studio.it", data.RelatedMessageId);
    Assert.Equal("ev-1", data.EvidenceId);
  }
}


public class SentReceiptStoreTests {
  [Fact]
  public void RememberAndApply_MatchesMsgidAndSubject() {
    var path = Path.Combine(Path.GetTempPath(), "postclient-receipts-" + Guid.NewGuid().ToString("N") + ".json");
    try {
      var store = new FileSentReceiptStore(path);
      store.Remember(new SentDispatch {
        MailboxId = "box-1",
        MessageId = "<orig@studio.it>",
        Subject = "Fattura studio",
        Status = ReceiptStatus.Submitted
      });
      Assert.Equal(ReceiptStatus.Submitted, store.StatusFor("box-1", "orig@studio.it"));
      store.ApplyEvidence("box-1", "<orig@studio.it>", "id-9", "ACCETTAZIONE: Fattura studio", "accettazione");
      Assert.Equal(ReceiptStatus.Accepted, store.StatusFor("box-1", "orig@studio.it"));
      store.ApplyEvidence("box-1", "orig@studio.it", null, "CONSEGNA: Fattura studio", "avvenuta-consegna");
      Assert.Equal(ReceiptStatus.Delivered, store.StatusFor("box-1", "<orig@studio.it>", "Fattura studio"));
      store.ApplyEvidence("box-1", null, null, "ACCETTAZIONE: Fattura studio", "accettazione");
      Assert.Equal(ReceiptStatus.Delivered, store.StatusFor("box-1", "orig@studio.it"));
    }
    finally {
      if (File.Exists(path))
        File.Delete(path);
    }
  }

  [Fact]
  public void ForgetMailbox_DropsOnlyThatAccount() {
    var path = Path.Combine(Path.GetTempPath(), "postclient-receipts-" + Guid.NewGuid().ToString("N") + ".json");
    try {
      var store = new FileSentReceiptStore(path);
      store.Remember(new SentDispatch {
        MailboxId = "gmail-1",
        MessageId = "gmail@msg",
        Subject = "Hello from gmail",
        Status = ReceiptStatus.Submitted
      });
      store.Remember(new SentDispatch {
        MailboxId = "pec-1",
        MessageId = "pec@msg",
        Subject = "Certified",
        Status = ReceiptStatus.Submitted
      });
      store.ForgetMailbox("gmail-1");
      Assert.Equal("", store.StatusFor("gmail-1", "gmail@msg", "Hello from gmail"));
      Assert.Equal(ReceiptStatus.Submitted, store.StatusFor("pec-1", "pec@msg"));
    }
    finally {
      if (File.Exists(path))
        File.Delete(path);
    }
  }
}


public class MimeBodyTests {
  [Fact]
  public async Task FromMimeAsync_LeavesMessageUnread() {
    var mime = new MimeMessage();
    mime.From.Add(new MailboxAddress("A", "a@b.c"));
    mime.To.Add(new MailboxAddress("B", "b@c.d"));
    mime.Subject = "Hello";
    mime.Body = new TextPart("plain") { Text = "Hi" };
    var body = await MimeBody.FromMimeAsync("INBOX", 8, mime, CancellationToken.None);
    Assert.False(body.Header.IsSeen);
    Assert.False(body.Header.IsFlagged);
  }
}
