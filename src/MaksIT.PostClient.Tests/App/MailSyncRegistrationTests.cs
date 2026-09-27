namespace MaksIT.PostClient.Tests.App;


public class MailSyncRegistrationTests {
  [Theory]
  [InlineData("root")]
  [InlineData("SYSTEM")]
  [InlineData("LOCALSYSTEM")]
  [InlineData(@"NT AUTHORITY\SYSTEM")]
  [InlineData("LocalService")]
  [InlineData("NetworkService")]
  [InlineData("")]
  [InlineData(null)]
  public void SharedMachineAccountsAreRefused(string? name) =>
    Assert.True(MailSyncIdentity.IsSharedAccount(name));

  [Theory]
  [InlineData("S-1-5-18")]
  [InlineData("S-1-5-19")]
  [InlineData("S-1-5-20")]
  public void MachineSidsAreRefused(string sid) =>
    Assert.True(MailSyncIdentity.IsMachineSid(sid));

  [Fact]
  public void APersonalAccountIsNotShared() =>
    Assert.False(MailSyncIdentity.IsSharedAccount("maksym"));

  [Fact]
  public void UnitMustStayInsideTheUserHome() {
    Assert.True(MailSyncIdentity.LivesInHome(
      @"C:\Users\maksym",
      @"C:\Users\maksym\.config\systemd\user"));
    Assert.False(MailSyncIdentity.LivesInHome(
      "/home/maksym",
      "/etc/systemd/system"));
  }

  [Fact]
  public void LinuxUnitIsASystemService() {
    var unit = MailSyncRegistrar.LinuxUnit("/usr/bin/postclient");
    Assert.Contains("ExecStart=/usr/bin/postclient --sync", unit, StringComparison.Ordinal);
    Assert.Contains("User=postclient", unit, StringComparison.Ordinal);
    Assert.Contains("WantedBy=multi-user.target", unit, StringComparison.Ordinal);
    Assert.DoesNotContain("default.target", unit, StringComparison.Ordinal);
  }

  [Fact]
  public void LinuxUnitQuotesPathsWithSpaces() {
    var unit = MailSyncRegistrar.LinuxUnit("/opt/Maks IT/postclient");
    Assert.Contains("ExecStart=\"/opt/Maks IT/postclient\" --sync", unit, StringComparison.Ordinal);
  }

  [Fact]
  public void WindowsServiceStartsAtBootAsLocalService() {
    var args = MailSyncRegistrar.WindowsCreateArguments(@"C:\Apps\Postclient.exe");
    Assert.Contains("start= auto", args, StringComparison.Ordinal);
    Assert.Contains("NT AUTHORITY\\LocalService", args, StringComparison.Ordinal);
    Assert.Contains("--sync", args, StringComparison.Ordinal);
    Assert.DoesNotContain("ONLOGON", args, StringComparison.Ordinal);
    Assert.DoesNotContain("LocalSystem", args, StringComparison.Ordinal);
  }
}
