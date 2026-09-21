namespace AddinManager.Core.Manifests;

/// <summary>
/// Опциональное поле записи AddIn, чья видимость в форме зависит от <see cref="AddinEntryType"/>
/// (план, раздел 6 — "Foreign-Type fields are fully hidden"). <c>Assembly</c>/<c>AddInId</c>/
/// <c>FullClassName</c> сюда не входят — они обязательны и показываются для любого типа, см.
/// <see cref="AddinManager.Core.Abstractions.Manifests.IManifestSchema"/>.
/// </summary>
public enum AddinEntryField
{
    /// <summary>Имя приложения — Application/DBApplication; Command вместо этого использует <see cref="Text"/>.</summary>
    Name,

    /// <summary>Подпись кнопки — Command; Application/DBApplication вместо этого используют <see cref="Name"/>.</summary>
    Text,

    /// <summary>Короткое описание (тултип кнопки) — Command.</summary>
    Description,

    /// <summary>Расширенное описание тултипа — Command.</summary>
    LongDescription,

    /// <summary>Класс IExternalCommandAvailability — Command.</summary>
    AvailabilityClassName,

    /// <summary>Идентификатор вендора — общий для всех типов.</summary>
    VendorId,

    /// <summary>Описание вендора — общий для всех типов.</summary>
    VendorDescription,

    /// <summary>Режимы видимости кнопки (multi) — Command.</summary>
    VisibilityMode,

    /// <summary>Дисциплины видимости кнопки (multi) — Command.</summary>
    Discipline,

    /// <summary>Иконка для меню External Tools — Command.</summary>
    LargeImage,

    /// <summary>Иконка для панели быстрого доступа — Command.</summary>
    SmallImage,

    /// <summary>Изображение расширенного тултипа — Command.</summary>
    ToolTipImage,
}
