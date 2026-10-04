namespace MaksIT.PostClient.Tests.Mail;


public class MailPipelineTests {
  [Fact]
  public async Task EachTurn_GetsMailThenAppliesRulesThenIndexesAFewBodies() {
    var cts = new CancellationTokenSource();
    var work = new Script(cts);
    var pipeline = new MailPipeline(work, TimeSpan.Zero, TimeSpan.Zero, MailPipeline.BodyBatch);
    await pipeline.LoopAsync(cts.Token);
    Assert.Equal(["fetch", "rules", "bodies", "fetch", "rules", "bodies"], work.Calls);
    Assert.Equal(2, work.Rules);
    Assert.Equal(MailPipeline.BodyBatch, work.BodyLimit);
    Assert.Equal(1, work.Wakes);
  }

  private sealed class Script : IMailPipelineWork {
    private readonly CancellationTokenSource _cts;

    public Script(CancellationTokenSource cts) => _cts = cts;

    public List<string> Calls { get; } = [];

    public int Rules { get; private set; }

    public int BodyLimit { get; private set; }

    public int Wakes { get; private set; }

    public Task WaitWhilePausedAsync(CancellationToken token) => Task.CompletedTask;

    public Task<bool> FetchNextPageAsync(CancellationToken token) {
      Calls.Add("fetch");
      return Task.FromResult(Rules == 0);
    }

    public Task<bool> ApplyRulesAsync(CancellationToken token) {
      Calls.Add("rules");
      Rules++;
      if (Rules == 2)
        _cts.Cancel();
      return Task.FromResult(true);
    }

    public Task<int> IndexBodiesAsync(int limit, CancellationToken token) {
      Calls.Add("bodies");
      BodyLimit = limit;
      return Task.FromResult(Rules == 1 ? 1 : 0);
    }

    public void WakeMeaningIndex() => Wakes++;
  }
}
