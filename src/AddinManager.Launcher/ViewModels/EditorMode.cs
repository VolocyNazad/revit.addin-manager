namespace AddinManager.Launcher.ViewModels;

/// <summary>Режим панели редактора. Меняется при жизни приложения.</summary>
public enum EditorMode
{
    /// <summary>Только список записей.</summary>
    Entries,

    /// <summary>Только форма записи.</summary>
    Form,

    /// <summary>Только разметка.</summary>
    Markup,

    /// <summary>Записи и форма.</summary>
    EntriesForm,

    /// <summary>Записи и разметка.</summary>
    EntriesMarkup,

    /// <summary>Только файловые настройки (ManifestSettings/UseRevitContext).</summary>
    Settings,
}
