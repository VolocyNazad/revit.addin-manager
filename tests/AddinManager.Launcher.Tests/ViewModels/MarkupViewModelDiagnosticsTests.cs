using System.IO;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Composition;
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
    private readonly List<string> _tempRoots = [];
    private readonly IAddinManifestParser _parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());

    [Fact]
    public void EmptyRequiredFields_AreMarked()
    {
        var (_, _, markup) = NewGraph(ValidAddinXml("Original"));

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
        var (_, _, markup) = NewGraph(ValidAddinXml("Original"));

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
        var (_, _, markup) = NewGraph(ValidAddinXml("Original"));

        Assert.Empty(markup.DiagnosticSpans);
    }

    [Fact]
    public void EditedXml_UpdatesSpansLive()
    {
        var (_, _, markup) = NewGraph(ValidAddinXml("Original"));
        Assert.Empty(markup.DiagnosticSpans);

        markup.RawXml = ValidAddinXml("Original").Replace("<Assembly>a.dll</Assembly>", "<Assembly></Assembly>", StringComparison.Ordinal);

        Assert.Single(markup.DiagnosticSpans);

        markup.RawXml = ValidAddinXml("Original");

        Assert.Empty(markup.DiagnosticSpans);
    }

    private (ListViewModel List, EntriesViewModel Entries, MarkupViewModel Markup) NewGraph(string xml)
    {
        var userRoot = NewRoot();
        var versionDir = Path.Combine(userRoot, "Autodesk", "Revit", "Addins", "2025");
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "A.addin"), xml);

        var store = new FileSystemAddinStore(_parser, NullLogger<FileSystemAddinStore>.Instance, new FakeRevitProcessGuard(), TestLocalization.For<FileSystemAddinStore>(), userRoot, NewRoot());
        var list = new ListViewModel(
            store,
            new FakeAddinChangeWatcher(),
            new RecordingUiDispatcher(),
            NullLogger<ListViewModel>.Instance,
            new FakeLocalizationService(),
            TestLocalization.For<ListViewModel>(),
            new AddinFileRowViewModelFactory(store, NullLoggerFactory.Instance, TestLocalization.For<AddinFileRowViewModel>()),
            new FakeToastService(),
            new FakeRevitProcessGuard(),
            new FakeDialogService(),
            new FakeFolderOpener());
        var entries = new EntriesViewModel(
            list,
            new FakeLocalizationService(),
            TestLocalization.For<EntriesViewModel>(),
            new AddinEntryRowViewModelFactory(TestLocalization.For<AddinEntryRowViewModel>()),
            _parser,
            new FileAddinMarkupService(_parser, NullLogger<FileAddinMarkupService>.Instance, new FakeRevitProcessGuard(), TestLocalization.For<FileAddinMarkupService>()),
            new FakeDialogService(),
            new RecordingUiDispatcher(),
            new FakeRevitProcessGuard());
        var markup = new MarkupViewModel(
            list,
            new FileAddinMarkupService(_parser, NullLogger<FileAddinMarkupService>.Instance, new FakeRevitProcessGuard(), TestLocalization.For<FileAddinMarkupService>()),
            NullLogger<MarkupViewModel>.Instance,
            new FakeLocalizationService(),
            TestLocalization.For<MarkupViewModel>(),
            new RecordingUiDispatcher(),
            new FakeRevitProcessGuard(),
            entries);

        return (list, entries, markup);
    }

    private static string ValidAddinXml(string name) => $"""
        <RevitAddIns>
          <AddIn Type="Application">
            <Name>{name}</Name>
            <Assembly>a.dll</Assembly>
            <AddInId>11111111-1111-1111-1111-111111111111</AddInId>
            <FullClassName>A.App</FullClassName>
          </AddIn>
        </RevitAddIns>
        """;

    private string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "AddinManagerTests-" + Guid.NewGuid());
        _tempRoots.Add(root);
        return root;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var root in _tempRoots)
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // Временная папка теста — не критично, если не удалилась.
            }
            catch (UnauthorizedAccessException)
            {
                // Временная папка теста — не критично, если не удалилась.
            }
        }
    }
}
