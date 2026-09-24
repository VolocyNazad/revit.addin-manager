namespace AddinManager.Launcher.Controls;

/// <summary>
/// Иконка общего словаря <c>Resources/Icons.xaml</c>: шаблон ищется по ключу
/// <c>Icon.{значение}</c>, цвет берётся из <see cref="PackIcon"/>-носителя.
/// </summary>
public enum PackIconKind
{
    /// <summary>Список (тулбар).</summary>
    List,

    /// <summary>Сплит список/редактор (тулбар).</summary>
    Split,

    /// <summary>Только редактор (тулбар).</summary>
    Editor,

    /// <summary>Тема (тулбар).</summary>
    Theme,

    /// <summary>Форма записи (редактор).</summary>
    Form,

    /// <summary>Разметка XML (редактор).</summary>
    Markup,

    /// <summary>Записи и форма (редактор).</summary>
    EntriesForm,

    /// <summary>Записи и разметка (редактор).</summary>
    EntriesMarkup,

    /// <summary>Настройки файла (редактор).</summary>
    Settings,

    /// <summary>Обновить (список).</summary>
    Refresh,

    /// <summary>Язык (тулбар).</summary>
    Language,

    /// <summary>Обновление версии (тулбар).</summary>
    Update,

    /// <summary>Поддержка (тулбар).</summary>
    Support,

    /// <summary>Спонсорство (тулбар).</summary>
    Sponsor,

    /// <summary>Удаление (строка).</summary>
    Delete,

    /// <summary>Крестик (строка).</summary>
    Close,

    /// <summary>Предупреждение (строка).</summary>
    Warning,

    /// <summary>Плюс (кнопка добавления).</summary>
    Add,

    /// <summary>Галка пакетного выбора (чекбокс).</summary>
    Check,

    /// <summary>Папка (кнопка "Показать в папке").</summary>
    Folder,
}
