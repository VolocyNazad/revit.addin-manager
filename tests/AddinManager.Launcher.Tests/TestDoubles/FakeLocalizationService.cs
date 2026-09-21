using System.Globalization;
using AddinManager.Localization;
using AddinManager.Localization.Abstractions;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// В памяти, без файлов и системной культуры: <see cref="ILocalizationService"/> для тестов
/// ViewModel'ей — интересует только подписка на смену языка (<see cref="RaiseLanguageChanged"/>),
/// а не персистентность (та уже покрыта <c>AddinManager.Localization.Tests</c>, другая сборка).
/// </summary>
public sealed class FakeLocalizationService : ILocalizationService
{
    /// <inheritdoc />
    public AppLanguage Language { get; private set; } = AppLanguage.System;

    /// <inheritdoc />
    public CultureInfo Culture => Language switch
    {
        AppLanguage.Russian => new CultureInfo("ru"),
        AppLanguage.English => new CultureInfo("en"),
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru"
            ? new CultureInfo("ru")
            : new CultureInfo("en"),
    };

    /// <inheritdoc />
    public event EventHandler? LanguageChanged;

    /// <inheritdoc />
    public void SetLanguage(AppLanguage language) => Language = language;

    /// <inheritdoc />
    public void ApplyCurrentCulture()
    {
    }

    /// <summary>Вручную firing <see cref="LanguageChanged"/> — имитирует смену языка пользователем.</summary>
    public void RaiseLanguageChanged() => LanguageChanged?.Invoke(this, EventArgs.Empty);
}
