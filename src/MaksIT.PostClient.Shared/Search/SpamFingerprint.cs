using System.Security.Cryptography;
using System.Text;


namespace MaksIT.PostClient.Shared.Search;


public static class SpamFingerprint {
  public static string Of(string? messageId, string? from, string? subject, string? dateUtc, string? bodyStart) {
    var id = (messageId ?? "").Trim();
    if (id.Length > 0)
      return "id:" + id;
    var body = bodyStart ?? "";
    if (body.Length > 160)
      body = body[..160];
    var raw = (from ?? "").Trim() + "\n" + (subject ?? "").Trim() + "\n" + (dateUtc ?? "").Trim() + "\n" + body;
    return "h:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
  }
}


public static class SpamScore {
  public const float ShowAt = 0.85f;

  public const int Cap = 2000;

  public static float Max(float[] vector, IEnumerable<float[]> examples) {
    ArgumentNullException.ThrowIfNull(vector);
    ArgumentNullException.ThrowIfNull(examples);
    var best = 0f;
    foreach (var example in examples) {
      var score = EmbeddingVector.Cosine(vector, example);
      if (score > best)
        best = score;
    }

    return best;
  }
}


public readonly record struct SpamFlag(bool Explicit, float Score);


public readonly record struct SpamLesson(
  string Fingerprint,
  string ModelId,
  string From,
  string Subject,
  string DateUtc,
  string MarkedUtc,
  bool Explicit,
  float[]? Vector);


public sealed class SpamExampleInfo {
  public string Fingerprint { get; init; } = "";

  public string From { get; init; } = "";

  public string Subject { get; init; } = "";

  public string DateUtc { get; init; } = "";

  public bool Explicit { get; init; }

  public bool Gone { get; init; }

  public bool Found { get; init; }

  public string Folder { get; init; } = "";

  public uint Uid { get; init; }
}
