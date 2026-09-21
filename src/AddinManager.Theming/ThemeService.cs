using AddinManager.Theming.Abstractions;

namespace AddinManager.Theming;

/// <summary>
/// Управление темой: выбор хранится в файле, системная резолвится делегатом.
/// Чистая логика без UI-зависимостей.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private readonly Func<bool> _isSystemDark;
    private readonly string _storagePath;
    private AppTheme? _cached;

    /// <summary>Создает сервис.</summary>
    /// <param name="isSystemDark">Делегат: темная ли сейчас системная тема.</param>
    /// <param name="storagePath">Файл выбора. По умолчанию — в %APPDATA%.</param>
    public ThemeService(Func<bool> isSystemDark, string? storagePath = null)
    {
        _isSystemDark = isSystemDark;
        _storagePath = storagePath ?? DefaultPath();
    }

    /// <inheritdoc />
    public AppTheme Theme
    {
        get
        {
            _cached ??= Load();
            return _cached.Value;
        }
    }

    /// <inheritdoc />
    public bool IsDark => Theme switch
    {
        AppTheme.Dark => true,
        AppTheme.Light => false,
        _ => _isSystemDark(),
    };

    /// <inheritdoc />
    public void SetTheme(AppTheme theme)
    {
        _cached = theme;
        var directory = Path.GetDirectoryName(_storagePath);
        if (directory is not null)
            Directory.CreateDirectory(directory);
        File.WriteAllText(_storagePath, theme.ToString());
    }

    private AppTheme Load()
    {
        try
        {
            if (File.Exists(_storagePath)
                && Enum.TryParse<AppTheme>(File.ReadAllText(_storagePath).Trim(), out var theme))
                return theme;
        }
        catch (IOException)
        {
            // Битый или недоступный файл выбора — откатываемся к System ниже.
        }
        catch (UnauthorizedAccessException)
        {
            // Нет прав на чтение — откатываемся к System ниже.
        }

        return AppTheme.System;
    }

    /// <summary>Путь хранения по умолчанию.</summary>
    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Volocy",
        "Revit.AddinManager",
        "theme.txt");
}
