using System.Globalization;
using Xunit;

namespace AddinManager.Localization.Tests;

/// <summary>
/// Выбор хранится в файле (как тема), системный резолвится делегатом, применение идёт через
/// <see cref="CultureInfo.CurrentUICulture"/> — тесты, меняющие культуру, возвращают её обратно,
/// чтобы не влиять на соседние тесты в том же процессе.
/// </summary>
public sealed class LocalizationServiceTests
{
    [Fact]
    public void MissingFile_DefaultsToSystem()
    {
        var service = new LocalizationService(() => new CultureInfo("en"), StoragePath());

        Assert.Equal(AppLanguage.System, service.Language);
    }

    [Fact]
    public void CorruptFile_DefaultsToSystem()
    {
        var path = StoragePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "not-a-language");
        var service = new LocalizationService(() => new CultureInfo("en"), path);

        Assert.Equal(AppLanguage.System, service.Language);
    }

    [Fact]
    public void System_RussianOperatingSystem_ResolvesRussian()
    {
        var service = new LocalizationService(() => new CultureInfo("ru-RU"), StoragePath());

        Assert.Equal("ru", service.Culture.TwoLetterISOLanguageName);
    }

    [Fact]
    public void System_NonRussianOperatingSystem_ResolvesEnglish()
    {
        var service = new LocalizationService(() => new CultureInfo("en-US"), StoragePath());

        Assert.Equal("en", service.Culture.TwoLetterISOLanguageName);
    }

    [Fact]
    public void ExplicitLanguage_IgnoresOperatingSystem()
    {
        var path = StoragePath();
        var service = new LocalizationService(() => new CultureInfo("ru-RU"), path);
        service.SetLanguage(AppLanguage.English);

        Assert.Equal(AppLanguage.English, service.Language);
        Assert.Equal("en", service.Culture.TwoLetterISOLanguageName);
    }

    [Fact]
    public void SetLanguage_PersistsAndReloads()
    {
        var path = StoragePath();
        new LocalizationService(() => new CultureInfo("en"), path).SetLanguage(AppLanguage.Russian);

        Assert.Equal("Russian", File.ReadAllText(path).Trim());
        Assert.Equal(
            AppLanguage.Russian,
            new LocalizationService(() => new CultureInfo("en"), path).Language);
    }

    [Fact]
    public void SetLanguage_RaisesLanguageChangedAndAppliesCulture()
    {
        var previousUiCulture = CultureInfo.CurrentUICulture;
        var previousDefaultCulture = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            var service = new LocalizationService(() => new CultureInfo("en"), StoragePath());
            var raised = 0;
            service.LanguageChanged += (_, _) => raised++;

            service.SetLanguage(AppLanguage.Russian);

            Assert.Equal(1, raised);
            Assert.Equal("ru", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            Assert.Equal("ru", CultureInfo.DefaultThreadCurrentUICulture?.TwoLetterISOLanguageName);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
            CultureInfo.DefaultThreadCurrentUICulture = previousDefaultCulture;
        }
    }

    private static string StoragePath() =>
        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "language.txt");
}
