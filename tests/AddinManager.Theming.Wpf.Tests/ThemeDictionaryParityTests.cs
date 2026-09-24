using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using Xunit;

namespace AddinManager.Theming.Wpf.Tests;

/// <summary>
/// Светлая и тёмная темы определяют одни и те же ключи (см. docs/architecture.md, "Themes").
/// </summary>
public sealed class ThemeDictionaryParityTests
{
    [Fact]
    public void ChromeDictionaries_DefineSameKeys()
    {
        Assert.Equal(ReadKeys("Chrome/Light.xaml"), ReadKeys("Chrome/Dark.xaml"));
    }

    [Fact]
    public void AppDictionaries_DefineSameKeys()
    {
        Assert.Equal(ReadKeys("App/Light.xaml"), ReadKeys("App/Dark.xaml"));
    }

    private static List<string> ReadKeys(string relativePath)
    {
        var path = Path.Combine(ThemesDirectory(), relativePath);
        Assert.True(File.Exists(path), $"Словарь не найден: {path}");

        var dictionary = (ResourceDictionary)XamlReader.Parse(File.ReadAllText(path));
        var keys = dictionary.Keys.OfType<string>().OrderBy(key => key, StringComparer.Ordinal).ToList();
        Assert.NotEmpty(keys);
        return keys;
    }

    private static string ThemesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Revit.AddinManager.slnx")))
            directory = directory.Parent;

        Assert.True(directory is not null, "Корень репозитория (Revit.AddinManager.slnx) не найден выше каталога тестов.");
        return Path.Combine(directory.FullName, "src", "AddinManager.Theming.Wpf", "Themes");
    }
}
