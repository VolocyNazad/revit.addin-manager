using System.Xml.Linq;

namespace AddinManager.Core.Manifests;

/// <summary>Манифест целиком: записи + настройки файла + данные для честного round-trip.</summary>
public sealed record AddinManifest(
    IReadOnlyList<AddinEntry> Entries,
    ManifestSettings? Settings,
    string? DeclarationVersion,
    string? DeclarationEncoding,
    string? DeclarationStandalone,
    IReadOnlyList<XNode> BeforeRoot,
    IReadOnlyList<XNode> AfterRoot,
    IReadOnlyList<XNode> RootUnknownNodes,
    // Порядок детей корня и незнакомые атрибуты самого <RevitAddIns> — чтобы не терять их при сохранении.
    IReadOnlyList<ManifestRootChild>? RootOrder = null,
    IReadOnlyList<XAttribute>? RootAttributes = null);
