namespace AddinManager.Core.Manifests;

/// <summary>Значение атрибута Type элемента AddIn. Unknown — встретили то, чего не знаем (вперед-совместимость).</summary>
public enum AddinEntryType
{
    /// <summary>Неизвестный Type: показываем, не роняем, правит схема по версии.</summary>
    Unknown,

    /// <summary>Внешнее приложение: грузится на старте Revit.</summary>
    Application,

    /// <summary>DB-приложение без UI: события и апдейтеры.</summary>
    DBApplication,

    /// <summary>Внешняя команда: грузится по требованию.</summary>
    Command,
}
