namespace MaksIT.PostClient.Client;


public interface ITextEmbedder : IDisposable {
  bool IsReady { get; }

  bool UsesGpu { get; }

  string ModelId { get; }

  float[] Embed(string text);
}
