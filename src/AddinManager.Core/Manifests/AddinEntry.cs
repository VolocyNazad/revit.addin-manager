using System.Xml.Linq;

namespace AddinManager.Core.Manifests;

/// <summary>
/// Одна запись AddIn внутри манифеста. Единица отображения и редактирования;
/// единицей активации остается файл целиком.
/// </summary>
public sealed record AddinEntry(
    AddinEntryType Type,
    string? RawType,
    string? Name,
    string? Text,
    string? Description,
    string? LongDescription,
    string AssemblyPath,
    Guid AddInId,
    string FullClassName,
    string? AvailabilityClassName,
    string? VendorId,
    string? VendorDescription,
    IReadOnlyList<string> VisibilityModes,
    IReadOnlyList<string> Disciplines,
    string? LargeImage,
    string? SmallImage,
    string? ToolTipImage,
    // Порядок дочерних тегов как в исходнике + узлы, которые мы не знаем (чужие теги, комментарии).
    IReadOnlyList<string> TagOrder,
    IReadOnlyList<XNode> UnknownNodes);
