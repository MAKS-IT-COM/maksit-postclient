

namespace MaksIT.PostClient.Tests.Search;


public class EmbeddingVectorTests {
  [Fact]
  public void Truncate_RenormalizesSlice() {
    var source = EmbeddingVector.Normalize([0.5f, 0.5f, 0.5f, 0.5f]);
    var cut = EmbeddingVector.Truncate(source, 2);
    Assert.Equal(2, cut.Length);
    Assert.InRange(cut[0] * cut[0] + cut[1] * cut[1], 0.99f, 1.01f);
  }

  [Fact]
  public void Cosine_IdenticalIsOne() {
    var a = EmbeddingVector.Normalize([1f, 2f, 3f]);
    Assert.InRange(EmbeddingVector.Cosine(a, a), 0.99f, 1.01f);
  }

  [Fact]
  public void Blob_RoundTrips() {
    var values = new[] { 0.25f, -0.5f, 0.125f };
    var copy = EmbeddingVector.FromBlob(EmbeddingVector.ToBlob(values));
    Assert.Equal(values, copy);
  }
}


public class EmbeddingPromptTests {
  [Fact]
  public void Query_UsesRetrievalPrefix() {
    Assert.StartsWith("task: search result | query: IMU", EmbeddingPrompt.Query("IMU condominio"));
  }

  [Fact]
  public void Document_JoinsSubjectAndBody() {
    var text = EmbeddingPrompt.Document("Avviso IMU", "comune@pec.it", "pagamento 2025", "");
    Assert.StartsWith("title: Avviso IMU | text:", text);
    Assert.Contains("comune@pec.it", text);
    Assert.Contains("pagamento 2025", text);
  }
}


public class SemanticDeviceTests {
  [Fact]
  public void Normalize_Aliases() {
    Assert.Equal(SemanticDevice.Auto, SemanticDevice.Normalize(""));
    Assert.Equal(SemanticDevice.Cpu, SemanticDevice.Normalize("CPU-only"));
    Assert.Equal(SemanticDevice.Gpu, SemanticDevice.Normalize("DirectML"));
  }
}


public class GpuProbeTests {
  [Fact]
  public void PreferForDirectMl_PicksNvidiaOverIntelAndSkipsWarp() {
    GpuAdapter[] adapters = [
      new(0, "Intel UHD Graphics", 0x8086, 128_000_000, false),
      new(1, "NVIDIA GeForce GTX 1660 Ti", 0x10DE, 6_000_000_000, false),
      new(2, "NVIDIA GeForce GTX 1660 Ti", 0x10DE, 6_000_000_000, false),
      new(3, "Microsoft Basic Render Driver", 0x1414, 0, true)
    ];
    var picked = GpuProbe.PreferForDirectMl(adapters);
    Assert.Equal(new[] { 1, 0 }, picked.Select(static adapter => adapter.DeviceId).ToArray());
  }

  [Fact]
  public void IsSoftwareAdapter_DetectsWarp() {
    Assert.True(GpuProbe.IsSoftwareAdapter("Microsoft Basic Render Driver", 0, 0x1414));
    Assert.True(GpuProbe.IsSoftwareAdapter("NVIDIA", 2, 0x10DE));
    Assert.False(GpuProbe.IsSoftwareAdapter("NVIDIA GeForce GTX 1660 Ti", 0, 0x10DE));
  }

  [Fact]
  public void DirectMlAdapters_OnWindowsSkipsSoftware() {
    if (!OperatingSystem.IsWindows())
      return;
    var adapters = GpuProbe.DirectMlAdapters();
    Assert.NotEmpty(adapters);
    Assert.DoesNotContain(adapters, static adapter => adapter.Software);
  }
}


public class SemanticSearchSettingsTests {
  [Fact]
  public void Normalize_KeepsEnabledAndAuto() {
    var settings = new SemanticSearchSettings();
    settings.Normalize();
    Assert.True(settings.Enabled);
    Assert.Equal(SemanticDevice.Auto, settings.Device);
  }
}


public class GitHubEmbeddingAssetTests {
  [Fact]
  public void FindNamed_MatchesModelFile() {
    var release = GitHubReleaseParser.Parse("""
      {"tag_name":"v0.1.1","html_url":"https://example.test","assets":[
        {"name":"postclient-0.1.1.zip","browser_download_url":"https://example.test/zip","size":1},
        {"name":"embeddinggemma-300m-int8.onnx","browser_download_url":"https://example.test/onnx","size":10}
      ]}
      """)!;
    var asset = GitHubReleaseParser.FindNamed(release, EmbeddingModelSpec.OnnxFileName);
    Assert.NotNull(asset);
    Assert.Equal("https://example.test/onnx", asset.Url);
  }
}


public class EmbeddingModelSpecTests {
  [Fact]
  public void HubFiles_ComeFromPublicOnnxCommunity() {
    Assert.Contains("onnx-community", EmbeddingModelSpec.HubOnnxUrl, StringComparison.Ordinal);
    Assert.Contains("onnx-community", EmbeddingModelSpec.HubSidecarUrl, StringComparison.Ordinal);
    Assert.Contains("onnx-community", EmbeddingModelSpec.HubTokenizerUrl, StringComparison.Ordinal);
    Assert.Contains(EmbeddingModelSpec.SidecarFileName, EmbeddingModelSpec.HubSidecarUrl, StringComparison.Ordinal);
    Assert.DoesNotContain("github.com", EmbeddingModelSpec.HubOnnxUrl, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("google/embeddinggemma", EmbeddingModelSpec.HubTokenizerUrl, StringComparison.Ordinal);
  }

  [Fact]
  public void FilesLookReady_AcceptsQuantizedSidecar() {
    var dir = Path.Combine(Path.GetTempPath(), "postclient-models-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    try {
      File.WriteAllBytes(Path.Combine(dir, EmbeddingModelSpec.TokenizerFileName), new byte[2000]);
      File.WriteAllBytes(Path.Combine(dir, EmbeddingModelSpec.OnnxFileName), new byte[5000]);
      Assert.False(EmbeddingModelSpec.FilesLookReady(dir));
      File.WriteAllBytes(Path.Combine(dir, EmbeddingModelSpec.SidecarFileName), new byte[1_000_001]);
      Assert.True(EmbeddingModelSpec.FilesLookReady(dir));
    }
    finally {
      Directory.Delete(dir, true);
    }
  }
}
