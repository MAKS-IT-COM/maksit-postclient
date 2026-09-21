using MailKit;
using MimeKit;


namespace MaksIT.PostClient.Tests.Mime;


public class MimeAttachmentTests {
  [Fact]
  public void HasFromStructure_NamedInlinePdf() {
    var mixed = new BodyPartMultipart(new ContentType("multipart", "mixed"), "1");
    mixed.BodyParts.Add(new BodyPartText(new ContentType("text", "plain"), "1.1"));
    mixed.BodyParts.Add(new BodyPartBasic(new ContentType("application", "pdf") { Name = "invoice.pdf" }, "1.2") {
      ContentDisposition = new ContentDisposition(ContentDisposition.Inline) {
        FileName = "invoice.pdf"
      },
      Octets = 1200
    });
    Assert.True(MimeAttachments.HasFromStructure(mixed));
  }

  [Fact]
  public void HasFromStructure_PlainTextOnly() {
    var text = new BodyPartText(new ContentType("text", "plain"), "1") {
      Octets = 12
    };
    Assert.False(MimeAttachments.HasFromStructure(text));
  }

  [Fact]
  public void HasFromStructure_CidImageIsSkipped() {
    var related = new BodyPartMultipart(new ContentType("multipart", "related"), "1");
    related.BodyParts.Add(new BodyPartText(new ContentType("text", "html"), "1.1") {
      Octets = 80
    });
    related.BodyParts.Add(new BodyPartBasic(new ContentType("image", "png"), "1.2") {
      ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
      ContentId = "logo@mail",
      Octets = 40
    });
    Assert.False(MimeAttachments.HasFromStructure(related));
  }

  [Fact]
  public void HasFromStructure_NestedRfc822() {
    var mixed = new BodyPartMultipart(new ContentType("multipart", "mixed"), "1");
    mixed.BodyParts.Add(new BodyPartText(new ContentType("text", "plain"), "1.1"));
    mixed.BodyParts.Add(new BodyPartMessage(new ContentType("message", "rfc822"), "1.2") {
      ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) {
        FileName = "postacert.eml"
      }
    });
    Assert.True(MimeAttachments.HasFromStructure(mixed));
  }
}
