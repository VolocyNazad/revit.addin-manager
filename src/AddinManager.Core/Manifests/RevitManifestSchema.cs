using AddinManager.Core.Abstractions.Manifests;

namespace AddinManager.Core.Manifests;

/// <summary>
/// <see cref="IManifestSchema"/> для настоящего Revit API в нашем диапазоне версий
/// (2021-2027, план раздел 2 — "all seven"). Матрица построена по официальной документации
/// Autodesk ("Add-in Registration", Revit API Developers Guide) и истории API, а не по
/// предположению из плана: гипотеза "DBApplication — since 2022" не подтвердилась —
/// <c>IExternalDBApplication</c> (а с ним <c>Type="DBApplication"</c>) в API с Revit 2012,
/// то есть доступен во ВСЕХ наших версиях (2021-2027 идут куда позже). То же для
/// <c>VisibilityMode</c>/<c>Discipline</c>/<c>AvailabilityClassName</c>/
/// <c>LargeImage</c>/<c>SmallImage</c>/<c>ToolTipImage</c> — все они старше 2021 на годы.
/// Единственная реальная версийная граница в нашем диапазоне — файловый
/// <see cref="ManifestSettings"/> (2026+, план раздел 6, уже подтверждено при реализации
/// Markup-зоны). <see cref="AvailableTypes"/>/<see cref="Fields"/> всё равно принимают
/// версию — таблица, а не константа, — так что более узкий будущий диапазон (например,
/// добавление версий старше 2021, где DBApplication ещё не было) ляжет сюда без изменения
/// потребителей.
/// </summary>
public sealed class RevitManifestSchema : IManifestSchema
{
    private static readonly IReadOnlyList<AddinEntryType> AllTypes =
        [AddinEntryType.Application, AddinEntryType.DBApplication, AddinEntryType.Command];

    private static readonly IReadOnlySet<AddinEntryField> CommonFields =
        new HashSet<AddinEntryField> { AddinEntryField.VendorId, AddinEntryField.VendorDescription };

    private static readonly IReadOnlySet<AddinEntryField> ApplicationFields =
        new HashSet<AddinEntryField>(CommonFields) { AddinEntryField.Name };

    private static readonly IReadOnlySet<AddinEntryField> CommandFields = new HashSet<AddinEntryField>(CommonFields)
    {
        AddinEntryField.Text,
        AddinEntryField.Description,
        AddinEntryField.LongDescription,
        AddinEntryField.AvailabilityClassName,
        AddinEntryField.VisibilityMode,
        AddinEntryField.Discipline,
        AddinEntryField.LargeImage,
        AddinEntryField.SmallImage,
        AddinEntryField.ToolTipImage,
    };

    /// <inheritdoc />
    public IReadOnlyList<AddinEntryType> AvailableTypes(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        return AllTypes;
    }

    /// <inheritdoc />
    public IReadOnlySet<AddinEntryField> Fields(string version, AddinEntryType type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        return type switch
        {
            AddinEntryType.Application or AddinEntryType.DBApplication => ApplicationFields,
            AddinEntryType.Command => CommandFields,
            _ => CommonFields,
        };
    }

    /// <inheritdoc />
    public bool SupportsManifestSettings(string version) => int.TryParse(version, out var year) && year >= 2026;

    /// <summary>
    /// Значения VisibilityMode ("Add-in Registration", Revit API Developers Guide):
    /// AlwaysVisible, NotVisibleInProject, NotVisibleInFamily, NotVisibleWhenNoActiveDocument.
    /// </summary>
    public IReadOnlyList<string> VisibilityModeValues { get; } =
        ["AlwaysVisible", "NotVisibleInProject", "NotVisibleInFamily", "NotVisibleWhenNoActiveDocument"];

    /// <summary>Значения Discipline — тот же набор, что и <c>Autodesk.Revit.DB.Discipline</c>.</summary>
    public IReadOnlyList<string> DisciplineValues { get; } =
    [
        "Any", "Architecture", "Structure", "StructuralAnalysis", "MassingAndSite",
        "EnergyAnalysis", "Mechanical", "Electrical", "Piping", "MechanicalAnalysis",
        "PipingAnalysis", "ElectricalAnalysis",
    ];
}
