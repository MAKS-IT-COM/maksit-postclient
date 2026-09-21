namespace MaksIT.PostClient.Client.Search;


public interface ITextEmbedder : IDisposable {
  bool IsReady { get; }

  bool UsesGpu { get; }

  string ModelId { get; }

  float[] Embed(string text);
}
