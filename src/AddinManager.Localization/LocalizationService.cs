using System.Globalization;
using AddinManager.Localization.Abstractions;

namespace AddinManager.Localization;

/// <summary>
/// Управление языком: выбор хранится в файле, системный резолвится делегатом.
/// Чистая логика без UI-зависимостей, тот же приём, что <c>ThemeService</c>
/// (делегат + путь как параметры конструктора — та же шовная точка для тестов).
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private static readonly CultureInfo RussianCulture = new("ru");
    private static readonly CultureInfo EnglishCulture = new("en");

    private readonly Func<CultureInfo> _systemCulture;
    private readonly string _storagePath;
    private AppLanguage? _cached;

    /// <summary>Создает сервис.</summary>
    /// <param name="systemCulture">Делегат: текущая системная UI-культура.</param>
    /// <param name="storagePath">Файл выбора. По умолчанию — в %APPDATA%.</param>
    public LocalizationService(Func<CultureInfo> systemCulture, string? storagePath = null)
    {
        _systemCulture = systemCulture;
        _storagePath = storagePath ?? DefaultPath();
    }

    /// <inheritdoc />
    public AppLanguage Language => _cached ??= Load();

    /// <inheritdoc />
    public CultureInfo Culture => Language switch
    {
        AppLanguage.Russian => RussianCulture,
        AppLanguage.English => EnglishCulture,
        _ => _systemCulture().TwoLetterISOLanguageName == RussianCulture.TwoLetterISOLanguageName
            ? RussianCulture
            : EnglishCulture,
    };

    /// <inheritdoc />
    public event EventHandler? LanguageChanged;

    /// <inheritdoc />
    public void SetLanguage(AppLanguage language)
    {
        _cached = language;
        var directory = Path.GetDirectoryName(_storagePath);
        if (directory is not null)
            Directory.CreateDirectory(directory);
        File.WriteAllText(_storagePath, language.ToString());
        ApplyCurrentCulture();
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void ApplyCurrentCulture()
    {
        CultureInfo.CurrentUICulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
    }

    private AppLanguage Load()
    {
        try
        {
            if (File.Exists(_storagePath)
                && Enum.TryParse<AppLanguage>(File.ReadAllText(_storagePath).Trim(), out var language))
                return language;
        }
        catch (IOException)
        {
            // Битый или недоступный файл выбора — откатываемся к System ниже.
        }
        catch (UnauthorizedAccessException)
        {
            // Нет прав на чтение — откатываемся к System ниже.
        }

        return AppLanguage.System;
    }

    /// <summary>Путь хранения по умолчанию.</summary>
    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Volocy",
        "Revit.AddinManager",
        "language.txt");
}
