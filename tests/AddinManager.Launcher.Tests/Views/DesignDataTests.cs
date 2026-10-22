using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AddinManager.Launcher.Design;

namespace AddinManager.Launcher.Tests.Views;

/// <summary>
/// Политика design-time данных: каждый составной вид с биндингами объявляет
/// <c>d:DataContext</c> через существующий <c>Design*</c>-стаб, а <c>d</c> входит в
/// <c>mc:Ignorable</c>. Тривиальный <c>ToolbarView</c> пока исключён — у него нет биндингов.
/// </summary>
public sealed class DesignDataTests
{
    private static readonly XNamespace D = "http://schemas.microsoft.com/expression/blend/2008";
    private static readonly XNamespace Mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    private static readonly Regex DesignType = new(@"Type=design:([A-Za-z0-9_]+)", RegexOptions.CultureInvariant);

    [Fact]
    public void EveryComposedView_HasDesignTimeData()
    {
        var viewsDirectory = Path.Combine(RepositoryRoot.Find(), "src", "AddinManager.Launcher", "Views");
        var designAssembly = typeof(DesignMainViewModel).Assembly;
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(viewsDirectory, "*.xaml", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) == "ToolbarView.xaml")
                continue;

            var root = XDocument.Load(file).Root!;
            var relativePath = Path.GetRelativePath(viewsDirectory, file);
            var designContext = (string?)root.Attribute(D + "DataContext");
            if (designContext is null)
            {
                violations.Add($"{relativePath}: нет d:DataContext");
                continue;
            }

            var match = DesignType.Match(designContext);
            if (!match.Success)
            {
                violations.Add($"{relativePath}: d:DataContext не ссылается на design:Design*");
                continue;
            }

            var designTypeName = $"AddinManager.Launcher.Design.{match.Groups[1].Value}";
            if (designAssembly.GetType(designTypeName) is null)
                violations.Add($"{relativePath}: тип {designTypeName} не найден");

            var ignorable = (string?)root.Attribute(Mc + "Ignorable");
            if (ignorable is null || !ignorable.Split(' ').Contains("d", StringComparer.Ordinal))
                violations.Add($"{relativePath}: mc:Ignorable не содержит d");
        }

        Assert.Empty(violations);
    }
}
