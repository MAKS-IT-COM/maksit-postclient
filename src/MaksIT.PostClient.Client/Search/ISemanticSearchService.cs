

namespace MaksIT.PostClient.Client.Search;


public interface ISemanticSearchService : IDisposable {
  bool IsReady { get; }

  bool UsesGpu { get; }

  string StatusLine { get; }

  event Action? Changed;

  event Action<Exception>? Faulted;

  void Start();

  void ReleaseModel();

  void ResumeModel();

  void NotifySettingsChanged();

  void Wake();

  float[]? EmbedQuery(string query);

  ArchiveIndexStats IndexStats();

  int RebuildKeywordIndex();

  ArchiveIndexStats SanitizeIndices();

  int RebuildMeaningIndex();
}
