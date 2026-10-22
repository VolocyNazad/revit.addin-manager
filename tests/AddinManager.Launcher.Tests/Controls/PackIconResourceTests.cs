using System.IO;
using System.Xml.Linq;
using AddinManager.Launcher.Controls;

namespace AddinManager.Launcher.Tests.Controls;

/// <summary>
/// Каждому значению <see cref="PackIconKind"/> соответствует шаблон <c>Icon.{Kind}</c> в общем
/// словаре: иначе иконка молча рисует fallback. Заодно проверяет, что словарь не содержит
/// «осиротевших» Icon-ключей без значения enum.
/// </summary>
public sealed class PackIconResourceTests
{
    [Fact]
    public void EveryPackIconKind_HasIconTemplate()
    {
        HashSet<string> keys = ReadIconKeys();

        foreach (var kind in Enum.GetNames<PackIconKind>())
            Assert.Contains("Icon." + kind, keys);
    }

    [Fact]
    public void IconTemplates_AllMapToPackIconKinds()
    {
        HashSet<string> keys = ReadIconKeys();
        HashSet<string> kinds = [.. Enum.GetNames<PackIconKind>().Select(name => "Icon." + name)];

        var orphans = keys.Where(key => key != "Icon.Fallback" && !kinds.Contains(key)).ToList();

        Assert.Empty(orphans);
    }

    private static HashSet<string> ReadIconKeys()
    {
        var path = Path.Combine(RepositoryRoot.Find(), "src", "AddinManager.Launcher", "Resources", "Icons.xaml");
        Assert.True(File.Exists(path), $"Словарь иконок не найден: {path}");

        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return [.. XDocument.Load(path).Descendants()
            .Select(element => (string?)element.Attribute(x + "Key"))
            .Where(key => key is not null && key.StartsWith("Icon.", StringComparison.Ordinal))
            .Select(key => key!)];
    }
}
