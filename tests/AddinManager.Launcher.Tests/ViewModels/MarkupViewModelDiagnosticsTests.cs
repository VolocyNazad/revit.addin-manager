using System.IO;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Диагностические срезы разметки (<see cref="MarkupViewModel.DiagnosticSpans"/>): пустые
/// обязательные поля и повторяющиеся <c>AddInId</c> — то же, что блокирует сохранение.
/// В отличие от селекции блока, маркеры пересчитываются на каждое нажатие и не трогают
/// каретку; текст с проблемами задаём напрямую через <c>RawXml</c> (файл с пустым
/// <c>Assembly</c> стор бы вообще не прочитал — см. пропуск непарсящихся файлов).
/// </summary>
public sealed class MarkupViewModelDiagnosticsTests : IDisposable
{
    private readonly TempDirectorySet _tempDirectories = new();
    private readonly IAddinManifestParser _parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());

    [Fact]
    public void EmptyRequiredFields_AreMarked()
    {
        var (_, _, markup) = NewGraph(TestManifestXml.ValidAddin("Original"));

        var xml = """
            <RevitAddIns>
              <AddIn Type="Application">
                <Name>Broken</Name>
                <Assembly></Assembly>
                <AddInId>11111111-1111-1111-1111-111111111111</AddInId>
                <FullClassName>   </FullClassName>
              </AddIn>
              <AddIn Type="Command">
                <Text>Broken</Text>
                <Assembly>a.dll</Assembly>
                <AddInId />
                <FullClassName>A.Broken</FullClassName>
              </AddIn>
            </RevitAddIns>
            """;
        markup.RawXml = xml;

        Assert.Equal(3, markup.DiagnosticSpans.Count);
        var marked = markup.DiagnosticSpans
            .Select(span => xml.Substring(span.Start, span.Length))
            .ToList();
        Assert.Contains(marked, text => text.Contains("<Assembly>", StringComparison.Ordinal));
        Assert.Contains(marked, text => text.Contains("<FullClassName>", StringComparison.Ordinal));
        Assert.Contains(marked, text => text.Contains("<AddInId", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateAddInIds_BothOccurrencesMarked()
    {
        var (_, _, markup) = NewGraph(TestManifestXml.ValidAddin("Original"));

        var xml = """
            <RevitAddIns>
              <AddIn Type="Application">
                <Name>First</Name>
                <Assembly>a.dll</Assembly>
                <AddInId>11111111-1111-1111-1111-111111111111</AddInId>
                <FullClassName>A.First</FullClassName>
              </AddIn>
              <AddIn Type="Command">
                <Text>Second</Text>
                <Assembly>b.dll</Assembly>
                <AddInId>11111111-1111-1111-1111-111111111111</AddInId>
                <FullClassName>B.Second</FullClassName>
              </AddIn>
            </RevitAddIns>
            """;
        markup.RawXml = xml;

        Assert.Equal(2, markup.DiagnosticSpans.Count);
        Assert.All(
            markup.DiagnosticSpans,
            span => Assert.Contains(
                "11111111-1111-1111-1111-111111111111",
                xml.Substring(span.Start, span.Length),
                StringComparison.Ordinal));
    }

    [Fact]
    public void ValidXml_NoSpans()
    {
        var (_, _, markup) = NewGraph(TestManifestXml.ValidAddin("Original"));

        Assert.Empty(markup.DiagnosticSpans);
    }

    [Fact]
    public void UnknownEntryType_IsMarked()
    {
        var (_, _, markup) = NewGraph(TestManifestXml.ValidAddin("Original"));

        const string xml = """
            <RevitAddIns>
              <AddIn Type="SomethingNew">
                <Name>Unknown</Name>
                <Assembly>a.dll</Assembly>
                <AddInId>33333333-3333-3333-3333-333333333333</AddInId>
                <FullClassName>A.Unknown</FullClassName>
              </AddIn>
            </RevitAddIns>
            """;
        markup.RawXml = xml;

        var span = Assert.Single(markup.DiagnosticSpans);
        Assert.Equal("SomethingNew", xml.Substring(span.Start, span.Length));
    }

    [Fact]
    public void EditedXml_UpdatesSpansLive()
    {
        var (_, _, markup) = NewGraph(TestManifestXml.ValidAddin("Original"));
        Assert.Empty(markup.DiagnosticSpans);

        markup.RawXml = TestManifestXml.ValidAddin("Original").Replace("<Assembly>a.dll</Assembly>", "<Assembly></Assembly>", StringComparison.Ordinal);

        Assert.Single(markup.DiagnosticSpans);

        markup.RawXml = TestManifestXml.ValidAddin("Original");

        Assert.Empty(markup.DiagnosticSpans);
    }

    private (ListViewModel List, EntriesViewModel Entries, MarkupViewModel Markup) NewGraph(string xml)
    {
        var userRoot = NewRoot();
        var versionDir = Path.Combine(userRoot, "Autodesk", "Revit", "Addins", "2025");
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "A.addin"), xml);

        var store = new FileSystemAddinStore(_parser, NullLogger<FileSystemAddinStore>.Instance, new FakeRevitProcessGuard(), TestLocalization.For<FileSystemAddinStore>(), userRoot, NewRoot());
        var list = ListViewModelFixture.Create(store);
        var entries = EditorZoneFixture.CreateEntries(_parser, list, list);
        var markup = new MarkupViewModel(
            list,
            TestMarkupSearch.Create(),
            new FileAddinMarkupService(_parser, NullLogger<FileAddinMarkupService>.Instance, new FakeRevitProcessGuard(), TestLocalization.For<FileAddinMarkupService>()),
            NullLogger<MarkupViewModel>.Instance,
            new FakeLocalizationService(),
            TestLocalization.For<MarkupViewModel>(),
            new RecordingUiDispatcher(),
            new FakeRevitProcessGuard(),
            entries,
            list);

        return (list, entries, markup);
    }

    private string NewRoot() => _tempDirectories.Create();

    /// <inheritdoc />
    public void Dispose()
    {
        _tempDirectories.Dispose();
    }
}
