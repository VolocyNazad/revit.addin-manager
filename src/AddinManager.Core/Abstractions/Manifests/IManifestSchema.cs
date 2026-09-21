using AddinManager.Core.Manifests;

namespace AddinManager.Core.Abstractions.Manifests;

/// <summary>
/// Таблица версия×тип (план, раздел 8 — "IManifestSchema (version table)"): какие
/// <see cref="AddinEntryType"/> и какие опциональные поля (<see cref="AddinEntryField"/>)
/// актуальны для версии Revit, и доступен ли файловый блок <see cref="ManifestSettings"/>.
/// Данные, не код (план, раздел 6 — "a table in Core, data — not code"): реализация не должна
/// ветвиться по версии сложной логикой, только смотреть в таблицу. Реализации заменяемы через DI.
/// </summary>
public interface IManifestSchema
{
    /// <summary>Типы записей, доступные для выбора в данной версии Revit.</summary>
    IReadOnlyList<AddinEntryType> AvailableTypes(string version);

    /// <summary>Опциональные поля, актуальные для этого типа записи в данной версии.</summary>
    IReadOnlySet<AddinEntryField> Fields(string version, AddinEntryType type);

    /// <summary>Доступен ли файловый блок ManifestSettings (изоляция контекста загрузки) в этой версии.</summary>
    bool SupportsManifestSettings(string version);

    /// <summary>Известные значения VisibilityMode — чипы формы читают список отсюда, а не парсят XML руками.</summary>
    IReadOnlyList<string> VisibilityModeValues { get; }

    /// <summary>Известные значения Discipline — чипы формы читают список отсюда.</summary>
    IReadOnlyList<string> DisciplineValues { get; }
}
