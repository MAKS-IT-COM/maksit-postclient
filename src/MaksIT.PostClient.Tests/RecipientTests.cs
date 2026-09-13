using MaksIT.PostClient.Client;


namespace MaksIT.PostClient.Tests;


public class RecipientParserTests {
  [Fact]
  public void Parse_FriendlyNameAndAddress() {
    var tags = RecipientParser.Parse("\"Mario Rossi\" <mario.rossi@pec.example.it>");
    Assert.Single(tags);
    Assert.Equal("Mario Rossi", tags[0].Display);
    Assert.Equal("mario.rossi@pec.example.it", tags[0].Address);
    Assert.Contains("mario.rossi@pec.example.it", tags[0].Tooltip);
    Assert.True(tags[0].IsValid);
  }

  [Fact]
  public void Parse_BareAddressUsesAddressAsDisplay() {
    var tags = RecipientParser.Parse("anna@studio.it");
    Assert.Single(tags);
    Assert.Equal("anna@studio.it", tags[0].Display);
  }

  [Fact]
  public void Parse_CommaAndSemicolonLists() {
    var tags = RecipientParser.Parse("a@b.it, c@d.it; e@f.it");
    Assert.Equal(3, tags.Count);
  }
}
