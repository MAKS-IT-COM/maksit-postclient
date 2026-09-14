namespace MaksIT.PostClient.Shared;


public static class AppRegion {
  public const string Italy = "it";
  public const string Europe = "eu";
  public const string France = "fr";
  public const string Germany = "de";
  public const string Spain = "es";
  public const string Switzerland = "ch";

  public static readonly string[] All = [
    Italy, Europe, France, Germany, Spain, Switzerland
  ];
}


public static class AppFeature {
  public const string PecEnvelope = "pec-envelope";
  public const string PecPresets = "pec-presets";
  public const string FatturaPa = "fattura-pa";
  public const string Fascicolo = "fascicolo";
  public const string RemEvidence = "rem-evidence";
  public const string RemPresets = "rem-presets";
  public const string FrEvidence = "fr-evidence";
  public const string DeEvidence = "de-evidence";
  public const string EsEvidence = "es-evidence";
  public const string ChEvidence = "ch-evidence";
}


public sealed class FeatureDefinition {
  public required string Id { get; init; }

  public required string Region { get; init; }

  public required string TitleEn { get; init; }

  public required string TitleIt { get; init; }

  public required string TitleFr { get; init; }

  public required string TitleDe { get; init; }

  public required string TitleEs { get; init; }

  public bool DefaultOn { get; init; } = true;

  public string Title =>
    TitleFor(UiLocale.Id);

  public string TitleFor(string? language) =>
    UiLanguage.Normalize(language) switch {
      UiLanguage.It => TitleIt,
      UiLanguage.Fr => TitleFr,
      UiLanguage.De => TitleDe,
      UiLanguage.Es => TitleEs,
      _ => TitleEn
    };
}


public static class FeatureCatalog {
  public static IReadOnlyList<FeatureDefinition> All { get; } = [
    new() {
      Id = AppFeature.PecEnvelope,
      Region = AppRegion.Italy,
      TitleEn = "PEC envelope, ricevute, delivery",
      TitleIt = "Busta PEC, ricevute, consegna",
      TitleFr = "Enveloppe PEC, avis, livraison",
      TitleDe = "PEC-Umschlag, Nachweise, Zustellung",
      TitleEs = "Sobre PEC, acuses, entrega"
    },
    new() {
      Id = AppFeature.PecPresets,
      Region = AppRegion.Italy,
      TitleEn = "Italian PEC account presets",
      TitleIt = "Profili PEC italiani",
      TitleFr = "Profils PEC italiens",
      TitleDe = "Italienische PEC-Profile",
      TitleEs = "Perfiles PEC italianos"
    },
    new() {
      Id = AppFeature.FatturaPa,
      Region = AppRegion.Italy,
      TitleEn = "FatturaPA XML preview",
      TitleIt = "Anteprima XML FatturaPA",
      TitleFr = "Aperçu XML FatturaPA",
      TitleDe = "FatturaPA-XML-Vorschau",
      TitleEs = "Vista previa XML FatturaPA"
    },
    new() {
      Id = AppFeature.Fascicolo,
      Region = AppRegion.Italy,
      TitleEn = "Export selected EML (fascicolo)",
      TitleIt = "Esporta EML selezionati (fascicolo)",
      TitleFr = "Exporter les EML sélectionnés (fascicule)",
      TitleDe = "Ausgewählte EML exportieren (Fascicolo)",
      TitleEs = "Exportar EML seleccionados (fascículo)"
    },
    new() {
      Id = AppFeature.RemEvidence,
      Region = AppRegion.Europe,
      TitleEn = "eIDAS REM evidence",
      TitleIt = "Evidenza REM eIDAS",
      TitleFr = "Preuve REM eIDAS",
      TitleDe = "eIDAS-REM-Nachweis",
      TitleEs = "Evidencia REM eIDAS"
    },
    new() {
      Id = AppFeature.RemPresets,
      Region = AppRegion.Europe,
      TitleEn = "REM account presets (Intesi)",
      TitleIt = "Profili REM (Intesi)",
      TitleFr = "Profils REM (Intesi)",
      TitleDe = "REM-Profile (Intesi)",
      TitleEs = "Perfiles REM (Intesi)"
    },
    new() {
      Id = AppFeature.FrEvidence,
      Region = AppRegion.France,
      TitleEn = "AR24 / LRE evidence labels",
      TitleIt = "Etichette evidenza AR24 / LRE",
      TitleFr = "Libellés de preuve AR24 / LRE",
      TitleDe = "AR24-/LRE-Nachweisbeschriftungen",
      TitleEs = "Etiquetas de evidencia AR24 / LRE",
      DefaultOn = false
    },
    new() {
      Id = AppFeature.DeEvidence,
      Region = AppRegion.Germany,
      TitleEn = "De-Mail evidence labels",
      TitleIt = "Etichette evidenza De-Mail",
      TitleFr = "Libellés de preuve De-Mail",
      TitleDe = "De-Mail-Nachweisbeschriftungen",
      TitleEs = "Etiquetas de evidencia De-Mail",
      DefaultOn = false
    },
    new() {
      Id = AppFeature.EsEvidence,
      Region = AppRegion.Spain,
      TitleEn = "Lleida / acuse evidence labels",
      TitleIt = "Etichette evidenza Lleida / acuse",
      TitleFr = "Libellés de preuve Lleida / acuse",
      TitleDe = "Lleida-/Acuse-Nachweisbeschriftungen",
      TitleEs = "Etiquetas de evidencia Lleida / acuse",
      DefaultOn = false
    },
    new() {
      Id = AppFeature.ChEvidence,
      Region = AppRegion.Switzerland,
      TitleEn = "IncaMail evidence labels",
      TitleIt = "Etichette evidenza IncaMail",
      TitleFr = "Libellés de preuve IncaMail",
      TitleDe = "IncaMail-Nachweisbeschriftungen",
      TitleEs = "Etiquetas de evidencia IncaMail",
      DefaultOn = false
    }
  ];

  public static FeatureDefinition? Find(string id) =>
    All.FirstOrDefault(f => f.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

  public static IEnumerable<FeatureDefinition> ForRegion(string region) =>
    All.Where(f => f.Region.Equals(region, StringComparison.OrdinalIgnoreCase));

  public static string RegionTitle(string region, string language) =>
    UiLanguage.Normalize(language) switch {
      UiLanguage.It => RegionTitleIt(region),
      UiLanguage.Fr => RegionTitleFr(region),
      UiLanguage.De => RegionTitleDe(region),
      UiLanguage.Es => RegionTitleEs(region),
      _ => RegionTitleEn(region)
    };

  public static string RegionHint(string region, string language) =>
    UiLanguage.Normalize(language) switch {
      UiLanguage.It => RegionHintIt(region),
      UiLanguage.Fr => RegionHintFr(region),
      UiLanguage.De => RegionHintDe(region),
      UiLanguage.Es => RegionHintEs(region),
      _ => RegionHintEn(region)
    };

  private static string RegionTitleEn(string region) =>
    region switch {
      AppRegion.Italy => "Italy",
      AppRegion.Europe => "Europe",
      AppRegion.France => "France",
      AppRegion.Germany => "Germany",
      AppRegion.Spain => "Spain",
      AppRegion.Switzerland => "Switzerland",
      _ => region
    };

  private static string RegionTitleIt(string region) =>
    region switch {
      AppRegion.Italy => "Italia",
      AppRegion.Europe => "Europa",
      AppRegion.France => "Francia",
      AppRegion.Germany => "Germania",
      AppRegion.Spain => "Spagna",
      AppRegion.Switzerland => "Svizzera",
      _ => region
    };

  private static string RegionTitleFr(string region) =>
    region switch {
      AppRegion.Italy => "Italie",
      AppRegion.Europe => "Europe",
      AppRegion.France => "France",
      AppRegion.Germany => "Allemagne",
      AppRegion.Spain => "Espagne",
      AppRegion.Switzerland => "Suisse",
      _ => region
    };

  private static string RegionTitleDe(string region) =>
    region switch {
      AppRegion.Italy => "Italien",
      AppRegion.Europe => "Europa",
      AppRegion.France => "Frankreich",
      AppRegion.Germany => "Deutschland",
      AppRegion.Spain => "Spanien",
      AppRegion.Switzerland => "Schweiz",
      _ => region
    };

  private static string RegionTitleEs(string region) =>
    region switch {
      AppRegion.Italy => "Italia",
      AppRegion.Europe => "Europa",
      AppRegion.France => "Francia",
      AppRegion.Germany => "Alemania",
      AppRegion.Spain => "España",
      AppRegion.Switzerland => "Suiza",
      _ => region
    };

  private static string RegionHintEn(string region) =>
    region switch {
      AppRegion.Italy => "PEC and FatturaPA.",
      AppRegion.Europe => "eIDAS REM when the operator publishes IMAP.",
      AppRegion.France => "AR24 / LRE: usually not IMAP.",
      AppRegion.Germany => "De-Mail: usually not IMAP.",
      AppRegion.Spain => "Lleida: usually not IMAP.",
      AppRegion.Switzerland => "IncaMail: usually not IMAP.",
      _ => ""
    };

  private static string RegionHintIt(string region) =>
    region switch {
      AppRegion.Italy => "PEC e FatturaPA.",
      AppRegion.Europe => "REM eIDAS quando l’operatore espone IMAP.",
      AppRegion.France => "AR24 / LRE: di solito non IMAP.",
      AppRegion.Germany => "De-Mail: di solito non IMAP.",
      AppRegion.Spain => "Lleida: di solito non IMAP.",
      AppRegion.Switzerland => "IncaMail: di solito non IMAP.",
      _ => ""
    };

  private static string RegionHintFr(string region) =>
    region switch {
      AppRegion.Italy => "PEC et FatturaPA.",
      AppRegion.Europe => "REM eIDAS lorsque l’opérateur publie IMAP.",
      AppRegion.France => "AR24 / LRE : en général pas d’IMAP.",
      AppRegion.Germany => "De-Mail : en général pas d’IMAP.",
      AppRegion.Spain => "Lleida : en général pas d’IMAP.",
      AppRegion.Switzerland => "IncaMail : en général pas d’IMAP.",
      _ => ""
    };

  private static string RegionHintDe(string region) =>
    region switch {
      AppRegion.Italy => "PEC und FatturaPA.",
      AppRegion.Europe => "eIDAS-REM, wenn der Betreiber IMAP anbietet.",
      AppRegion.France => "AR24 / LRE: in der Regel kein IMAP.",
      AppRegion.Germany => "De-Mail: in der Regel kein IMAP.",
      AppRegion.Spain => "Lleida: in der Regel kein IMAP.",
      AppRegion.Switzerland => "IncaMail: in der Regel kein IMAP.",
      _ => ""
    };

  private static string RegionHintEs(string region) =>
    region switch {
      AppRegion.Italy => "PEC y FatturaPA.",
      AppRegion.Europe => "REM eIDAS cuando el operador publica IMAP.",
      AppRegion.France => "AR24 / LRE: normalmente no IMAP.",
      AppRegion.Germany => "De-Mail: normalmente no IMAP.",
      AppRegion.Spain => "Lleida: normalmente no IMAP.",
      AppRegion.Switzerland => "IncaMail: normalmente no IMAP.",
      _ => ""
    };
}
