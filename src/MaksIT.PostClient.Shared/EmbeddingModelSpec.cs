namespace MaksIT.PostClient.Shared;


public static class EmbeddingModelSpec {
  public const string Id = "embeddinggemma-300m-int8-d512";
  public const string DisplayName = "EmbeddingGemma 300M";
  public const string OnnxFileName = "embeddinggemma-300m-int8.onnx";
  public const string SidecarFileName = "model_quantized.onnx_data";
  public const string TokenizerFileName = "tokenizer.model";
  public const string HubRepo = "onnx-community/embeddinggemma-300m-ONNX";
  public const string HubBase =
    "https://huggingface.co/onnx-community/embeddinggemma-300m-ONNX/resolve/main/";
  public const string HubOnnxUrl = HubBase + "onnx/model_quantized.onnx?download=true";
  public const string HubSidecarUrl = HubBase + "onnx/model_quantized.onnx_data?download=true";
  public const string HubTokenizerUrl = HubBase + "tokenizer.model?download=true";
  public const int NativeDimensions = 768;
  public const int StoredDimensions = 512;
  public const int MaxTokens = 512;
  public const float MinScore = 0.32f;
  public const int VectorTopK = 40;

  public static IReadOnlyList<EmbeddingModelFile> Files { get; } = [
    new(HubOnnxUrl, OnnxFileName, "onnx", 10_000),
    new(HubSidecarUrl, SidecarFileName, "weights", 1_000_000),
    new(HubTokenizerUrl, TokenizerFileName, "tokenizer", 1_000)
  ];

  public static string OnnxPath(string? directory = null) =>
    Path.Combine(directory ?? AppPaths.ModelsDirectory(), OnnxFileName);

  public static string SidecarPath(string? directory = null) =>
    Path.Combine(directory ?? AppPaths.ModelsDirectory(), SidecarFileName);

  public static string TokenizerPath(string? directory = null) =>
    Path.Combine(directory ?? AppPaths.ModelsDirectory(), TokenizerFileName);

  public static bool FilesLookReady(string? directory = null) {
    var root = directory ?? AppPaths.ModelsDirectory();
    var onnx = OnnxPath(root);
    var tokenizer = TokenizerPath(root);
    if (!File.Exists(onnx) || !File.Exists(tokenizer) || new FileInfo(tokenizer).Length <= 1_000)
      return false;
    if (new FileInfo(onnx).Length > 1_000_000)
      return true;
    var sidecar = SidecarPath(root);
    return File.Exists(sidecar) && new FileInfo(sidecar).Length > 1_000_000;
  }
}


public readonly record struct EmbeddingModelFile(
  string Url,
  string FileName,
  string Label,
  long MinBytes);
