namespace AddinManager.Launcher.ViewModels;

/// <summary>Фильтр списка по состоянию enabled/disabled.</summary>
public enum EnabledFilter
{
    /// <summary>Без фильтра — и включённые, и выключенные.</summary>
    All,

    /// <summary>Только включённые файлы.</summary>
    Enabled,

    /// <summary>Только выключенные файлы (папка <c>disabled/</c>).</summary>
    Disabled,
}
