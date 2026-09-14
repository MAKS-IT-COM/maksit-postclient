using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MaksIT.PostClient.Shared;


namespace MaksIT.PostClient.UI.ViewModels;


public sealed partial class FeatureRowViewModel : ObservableObject {
  private readonly Action _changed;

  public FeatureRowViewModel(FeatureDefinition feature, bool on, Action changed) {
    Id = feature.Id;
    Region = feature.Region;
    Title = feature.Title;
    _changed = changed;
    enabled = on;
  }

  public string Id { get; }

  public string Region { get; }

  public string Title { get; }

  public bool ShowItaly => Region == AppRegion.Italy;

  public bool ShowEurope => Region == AppRegion.Europe;

  public bool ShowFrance => Region == AppRegion.France;

  public bool ShowGermany => Region == AppRegion.Germany;

  public bool ShowSpain => Region == AppRegion.Spain;

  public bool ShowSwitzerland => Region == AppRegion.Switzerland;

  [ObservableProperty]
  private bool enabled;

  partial void OnEnabledChanged(bool value) =>
    _changed();
}


public sealed partial class FeatureMatrixViewModel : ObservableObject {
  private readonly ConfigurationFileService _files;
  private readonly Action _applied;
  private bool _syncing;

  public FeatureMatrixViewModel(ConfigurationFileService files, Action applied) {
    _files = files;
    _applied = applied;
    Columns = AppRegion.All.Select(id => new FeatureRegionColumn(this, id)).ToList();
    Rows = new ObservableCollection<FeatureRowViewModel>(
      FeatureCatalog.All.Select(feature => new FeatureRowViewModel(
        feature,
        Features.IsEnabled(feature.Id),
        Persist)));
    RefreshPacks();
  }

  public UiCopy Copy =>
    UiLocale.Copy;

  public IReadOnlyList<FeatureRegionColumn> Columns { get; }

  public ObservableCollection<FeatureRowViewModel> Rows { get; }

  public FeatureRegionColumn ItalyColumn => Columns[0];

  public FeatureRegionColumn EuropeColumn => Columns[1];

  public FeatureRegionColumn FranceColumn => Columns[2];

  public FeatureRegionColumn GermanyColumn => Columns[3];

  public FeatureRegionColumn SpainColumn => Columns[4];

  public FeatureRegionColumn SwitzerlandColumn => Columns[5];

  public bool IsSyncing =>
    _syncing;

  private FeatureSettings Features {
    get {
      var configuration = _files.Current;
      configuration.Features ??= new FeatureSettings();
      configuration.Features.Normalize();
      return configuration.Features;
    }
  }

  public void Persist() {
    if (_syncing)
      return;
    var configuration = _files.Current;
    configuration.Features ??= new FeatureSettings();
    foreach (var row in Rows)
      configuration.Features.Set(row.Id, row.Enabled);
    FeatureGate.Use(configuration.Features);
    _files.Save(configuration);
    RefreshPacks();
    _applied();
  }

  public void SetRegion(string region, bool on) {
    _syncing = true;
    foreach (var row in Rows.Where(r => r.Region == region))
      row.Enabled = on;
    _syncing = false;
    Persist();
  }

  private void RefreshPacks() {
    _syncing = true;
    foreach (var column in Columns)
      column.Refresh();
    _syncing = false;
  }
}


public sealed partial class FeatureRegionColumn : ObservableObject {
  private readonly FeatureMatrixViewModel _owner;

  public FeatureRegionColumn(FeatureMatrixViewModel owner, string id) {
    _owner = owner;
    Id = id;
  }

  public string Id { get; }

  public string Title =>
    FeatureCatalog.RegionTitle(Id, UiLocale.Id);

  public string Hint =>
    FeatureCatalog.RegionHint(Id, UiLocale.Id);

  [ObservableProperty]
  private bool enabled;

  public void Refresh() {
    Enabled = _owner.Rows.Where(r => r.Region == Id).All(r => r.Enabled);
    OnPropertyChanged(nameof(Title));
    OnPropertyChanged(nameof(Hint));
  }

  partial void OnEnabledChanged(bool value) {
    if (!_owner.IsSyncing)
      _owner.SetRegion(Id, value);
  }
}
