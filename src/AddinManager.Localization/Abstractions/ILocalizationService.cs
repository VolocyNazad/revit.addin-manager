using System.Globalization;
using AddinManager.Localization;

namespace AddinManager.Localization.Abstractions;

/// <summary>
/// Абстракция управления языком интерфейса. Реализация заменяема через DI.
/// Выбор хранится в файле (как тема в <c>AddinManager.Theming</c>), применяется через
/// <see cref="CultureInfo.CurrentUICulture"/> — <c>IStringLocalizer</c> читает культуру
/// лениво, при каждом обращении, поэтому сами локайзеры пересоздавать не нужно.
/// </summary>
public interface ILocalizationService
{
    /// <summary>Выбранный режим языка.</summary>
    AppLanguage Language { get; }

    /// <summary>Эффективная культура под <see cref="Language"/> (русская или английская, других нет).</summary>
    CultureInfo Culture { get; }

    /// <summary>Язык сменили через <see cref="SetLanguage"/> — потребители перечитывают строки.</summary>
    event EventHandler? LanguageChanged;

    /// <summary>Выбрать, сохранить и сразу применить язык.</summary>
    void SetLanguage(AppLanguage language);

    /// <summary>Применить <see cref="Culture"/> к текущему потоку и потокам по умолчанию (вызывать на старте).</summary>
    void ApplyCurrentCulture();
}
