namespace MaksIT.PostClient.Shared.Mail;


public static class MailListBudget {
  public readonly record struct Slice(int Start, int End, int Take, int NextOffset, bool Incomplete);

  public static Slice NewestRange(int count, int offset, int budget) {
    if (count <= 0 || offset >= count || budget <= 0)
      return new Slice(0, -1, 0, Math.Max(0, offset), false);

    var remaining = count - offset;
    var take = Math.Min(budget, remaining);
    var end = count - 1 - offset;
    var start = end - take + 1;
    var next = offset + take;
    return new Slice(start, end, take, next, next < count);
  }
}
