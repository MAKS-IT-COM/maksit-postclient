using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.Client;


public interface ISemanticSearchService : IDisposable {
  bool IsReady { get; }

  bool UsesGpu { get; }

  string StatusLine { get; }

  event Action? Changed;

  void Start();

  void NotifySettingsChanged();

  float[]? EmbedQuery(string query);

  ArchiveIndexStats IndexStats();

  int RebuildKeywordIndex();

  ArchiveIndexStats SanitizeIndices();

  int RebuildMeaningIndex();
}
