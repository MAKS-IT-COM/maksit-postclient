using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public sealed class OnnxGemmaEmbedder : ITextEmbedder {
  private readonly InferenceSession _session;
  private readonly Tokenizer _tokenizer;
  private readonly string _inputIds;
  private readonly string _attentionMask;
  private readonly string? _tokenTypeIds;
  private readonly string _output;
  private readonly bool _outputIsTokens;
  private readonly Lock _gate = new();

  public OnnxGemmaEmbedder(string onnxPath, string tokenizerPath, bool useGpu) {
    ArgumentException.ThrowIfNullOrWhiteSpace(onnxPath);
    ArgumentException.ThrowIfNullOrWhiteSpace(tokenizerPath);
    using var stream = File.OpenRead(tokenizerPath);
    _tokenizer = LlamaTokenizer.Create(stream, addBeginOfSentence: true, addEndOfSentence: true);
    var options = new SessionOptions();
    UsesGpu = false;
    if (useGpu)
      UsesGpu = TryDirectMl(options);
    try {
      options.AppendExecutionProvider_CPU();
    }
    catch {
    }

    _session = new InferenceSession(onnxPath, options);
    _inputIds = PickInput(_session, "input_ids", "input");
    _attentionMask = PickInput(_session, "attention_mask", "mask");
    _tokenTypeIds = FindInput(_session, "token_type_ids");
    _output = PickOutput(_session);
    _outputIsTokens = _session.OutputMetadata[_output].Dimensions.Length == 3;
  }

  public bool IsReady =>
    true;

  public bool UsesGpu { get; }

  public string ModelId =>
    EmbeddingModelSpec.Id;

  public float[] Embed(string text) {
    var ids = _tokenizer.EncodeToIds(text ?? "");
    if (ids.Count == 0)
      ids = [0];
    if (ids.Count > EmbeddingModelSpec.MaxTokens)
      ids = ids.Take(EmbeddingModelSpec.MaxTokens).ToList();
    var length = ids.Count;
    var inputIds = new DenseTensor<long>(new[] { 1, length });
    var mask = new DenseTensor<long>(new[] { 1, length });
    for (var i = 0; i < length; i++) {
      inputIds[0, i] = ids[i];
      mask[0, i] = 1;
    }

    var inputs = new List<NamedOnnxValue> {
      NamedOnnxValue.CreateFromTensor(_inputIds, inputIds),
      NamedOnnxValue.CreateFromTensor(_attentionMask, mask)
    };
    if (_tokenTypeIds is not null) {
      var types = new DenseTensor<long>(new[] { 1, length });
      inputs.Add(NamedOnnxValue.CreateFromTensor(_tokenTypeIds, types));
    }

    IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results;
    lock (_gate)
      results = _session.Run(inputs);
    using (results) {
      var tensor = results.First(r => r.Name == _output).AsTensor<float>();
      var vector = _outputIsTokens ? MeanPool(tensor, length) : Row(tensor);
      return EmbeddingVector.Truncate(vector, EmbeddingModelSpec.StoredDimensions);
    }
  }

  public void Dispose() =>
    _session.Dispose();

  private static bool TryDirectMl(SessionOptions options) {
    var method = typeof(SessionOptions).GetMethod("AppendExecutionProvider_DML", [typeof(int)]);
    if (method is null)
      return false;
    try {
      method.Invoke(options, [0]);
      return true;
    }
    catch {
      return false;
    }
  }

  private static string PickInput(InferenceSession session, params string[] names) {
    foreach (var name in names) {
      foreach (var key in session.InputMetadata.Keys) {
        if (key.Equals(name, StringComparison.OrdinalIgnoreCase) || key.Contains(name, StringComparison.OrdinalIgnoreCase))
          return key;
      }
    }

    return session.InputMetadata.Keys.First();
  }

  private static string? FindInput(InferenceSession session, string name) {
    foreach (var key in session.InputMetadata.Keys) {
      if (key.Equals(name, StringComparison.OrdinalIgnoreCase))
        return key;
    }

    return null;
  }

  private static string PickOutput(InferenceSession session) {
    foreach (var key in session.OutputMetadata.Keys) {
      if (key.Contains("sentence", StringComparison.OrdinalIgnoreCase)
          || key.Contains("pool", StringComparison.OrdinalIgnoreCase)
          || key.Equals("embeddings", StringComparison.OrdinalIgnoreCase))
        return key;
    }

    return session.OutputMetadata.Keys.Last();
  }

  private static float[] MeanPool(Tensor<float> tensor, int length) {
    var dims = tensor.Dimensions;
    var hidden = dims.Length >= 3 ? dims[2] : dims[1];
    var sum = new float[hidden];
    for (var i = 0; i < length; i++) {
      for (var h = 0; h < hidden; h++)
        sum[h] += tensor[0, i, h];
    }

    var scale = 1f / Math.Max(length, 1);
    for (var h = 0; h < hidden; h++)
      sum[h] *= scale;
    return sum;
  }

  private static float[] Row(Tensor<float> tensor) {
    var dims = tensor.Dimensions;
    var hidden = dims.Length == 1 ? dims[0] : dims[^1];
    var values = new float[hidden];
    for (var i = 0; i < hidden; i++)
      values[i] = dims.Length == 1 ? tensor[i] : tensor[0, i];
    return values;
  }
}
