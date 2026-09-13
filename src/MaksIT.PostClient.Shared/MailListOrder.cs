namespace MaksIT.PostClient.Shared;


public static class MailListOrder {
  public static List<List<MailChainLink>> ThreadBlocks(IReadOnlyList<MailChainLink> links) {
    ArgumentNullException.ThrowIfNull(links);
    var blocks = new List<List<MailChainLink>>();
    for (var i = 0; i < links.Count;) {
      var take = 1;
      if (i + 1 < links.Count && links[i + 1].Depth > 0) {
        while (i + take < links.Count && links[i + take].Depth > 0)
          take++;
      }
      else if (links[i].Size > 1)
        take = Math.Min(links[i].Size, links.Count - i);

      var block = new List<MailChainLink>(take);
      for (var n = 0; n < take; n++)
        block.Add(links[i + n]);
      blocks.Add(block);
      i += take;
    }

    return blocks;
  }

  public static IReadOnlyList<MailChainLink> ReorderThreads(
    IReadOnlyList<MailChainLink> links,
    Comparison<uint> compareRoots) {
    ArgumentNullException.ThrowIfNull(links);
    ArgumentNullException.ThrowIfNull(compareRoots);
    var blocks = ThreadBlocks(links);
    blocks.Sort((a, b) => compareRoots(a[0].Uid, b[0].Uid));
    return Flatten(blocks);
  }

  public static IReadOnlyList<MailChainLink> ReverseThreads(IReadOnlyList<MailChainLink> links) {
    ArgumentNullException.ThrowIfNull(links);
    var blocks = ThreadBlocks(links);
    blocks.Reverse();
    return Flatten(blocks);
  }

  private static List<MailChainLink> Flatten(List<List<MailChainLink>> blocks) {
    var rows = new List<MailChainLink>();
    foreach (var block in blocks)
      rows.AddRange(block);
    return rows;
  }
}
