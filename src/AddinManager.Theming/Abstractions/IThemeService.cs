using AddinManager.Theming;

namespace AddinManager.Theming.Abstractions;

/// <summary>
/// Абстракция управления темой. Реализация заменяема через DI.
/// </summary>
public interface IThemeService
{
    /// <summary>Выбранная тема.</summary>
    AppTheme Theme { get; }

    /// <summary>Эффективно темная (с учетом системной).</summary>
    bool IsDark { get; }

    /// <summary>Выбрать и сохранить тему.</summary>
    void SetTheme(AppTheme theme);
}
