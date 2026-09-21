using Xunit;

namespace AddinManager.Theming.Tests;

public sealed class ThemeServiceTests
{
    [Fact]
    public void MissingFile_DefaultsToSystem()
    {
        var service = new ThemeService(() => false, TempPath());

        Assert.Equal(AppTheme.System, service.Theme);
    }

    [Fact]
    public void SetTheme_PersistsAcrossInstances()
    {
        var path = TempPath();
        new ThemeService(() => false, path).SetTheme(AppTheme.Dark);

        Assert.Equal(AppTheme.Dark, new ThemeService(() => false, path).Theme);
    }

    [Fact]
    public void CorruptFile_DefaultsToSystem()
    {
        var path = TempPath();
        File.WriteAllText(path, "not-a-theme");

        Assert.Equal(AppTheme.System, new ThemeService(() => false, path).Theme);
    }

    [Theory]
    [InlineData(AppTheme.Dark, false, true)]
    [InlineData(AppTheme.Dark, true, true)]
    [InlineData(AppTheme.Light, false, false)]
    [InlineData(AppTheme.Light, true, false)]
    [InlineData(AppTheme.System, false, false)]
    [InlineData(AppTheme.System, true, true)]
    public void IsDark_ResolvesChoice(AppTheme theme, bool systemDark, bool expected)
    {
        var path = TempPath();
        var service = new ThemeService(() => systemDark, path);
        service.SetTheme(theme);

        Assert.Equal(expected, service.IsDark);
    }

    private static string TempPath() => Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");
}
