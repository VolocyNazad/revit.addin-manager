using System.IO;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Manifests;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Composition;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Иконка предупреждений в строках списков и подсветка кросс-файловых дублей:
/// файл без записей, пустые обязательные поля, неизвестный тип, дубли внутри файла
/// и дубли в других файлах той же версии Revit.
/// </summary>
public sealed class RowWarningTests : IDisposable
{
    private static readonly Guid SharedId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly List<string> _tempRoots = [];
    private readonly IAddinManifestParser _parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());

    [Fact]
    public void CleanFile_NoWarning()
    {
        var row = NewRow(ValidXml("A", SharedId));

        Assert.False(row.HasWarning);
        Assert.Null(row.WarningMessage);
    }

    [Fact]
    public void FileWithoutEntries_Warns()
    {
        var row = NewRow("<RevitAddIns></RevitAddIns>");

        Assert.True(row.HasWarning);
        Assert.NotNull(row.WarningMessage);
    }

    [Fact]
    public void EmptyAssembly_Warns()
    {
        var row = NewRow(ValidXml("A", SharedId).Replace("<Assembly>a.dll</Assembly>", "<Assembly></Assembly>", StringComparison.Ordinal));

        Assert.True(row.HasWarning);
        Assert.NotNull(row.WarningMessage);
    }

    [Fact]
    public void UnknownType_Warns()
    {
        var xml = ValidXml("A", SharedId).Replace("Type=\"Application\"", "Type=\"SomethingElse\"", StringComparison.Ordinal);
        var row = NewRow(xml);

        Assert.True(row.HasWarning);
        Assert.NotNull(row.WarningMessage);
    }

    [Fact]
    public void DuplicateInFile_WarnsFileAndEntries()
    {
        // Парсер дубли внутри файла не пропускает (стор такой файл скипает), поэтому
        // собираем манифест вручную через FakeAddinStore, а не через диск.
        var manifest = new AddinManifest(
            [NewEntry("First", SharedId), NewEntry("Second", SharedId)],
            null, null, null, null, [], [], []);
        var file = new AddinFile(
            "A.addin", AddinScope.User, "2025", true,
            @"C:\fake\2025\A.addin", @"C:\fake\2025", manifest);
        var store = new FakeAddinStore();
        store.SetFiles("2025", file);
        var list = NewList(store);
        var entries = NewEntries(list);

        var fileRow = Assert.Single(list.Files);
        Assert.True(fileRow.HasWarning);
        Assert.True(
            fileRow.WarningMessage.Contains("within the file", StringComparison.OrdinalIgnoreCase)
                || fileRow.WarningMessage.Contains("внутри файла", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(2, entries.Entries.Count);
        Assert.All(entries.Entries, row => Assert.True(row.HasWarning));
    }

    [Fact]
    public void CrossFileDuplicate_SameVersion_WarnsBothFiles()
    {
        var (list, entries, _) = NewGraph(
            ("A.addin", "2025", ValidXml("A", SharedId)),
            ("B.addin", "2025", ValidXml("B", SharedId)));

        Assert.Equal(2, list.Files.Count);
        Assert.All(list.Files, row => Assert.True(row.HasWarning));
        Assert.All(list.Files, row => Assert.Contains("2025", row.WarningMessage, StringComparison.Ordinal));

        var entryRows = entries.Entries;
        Assert.All(entryRows, row => Assert.True(row.HasWarning));
    }

    [Fact]
    public void CrossFileDuplicate_DifferentVersions_NoWarning()
    {
        var (list, _, _) = NewGraph(
            ("A.addin", "2025", ValidXml("A", SharedId)),
            ("B.addin", "2026", ValidXml("B", SharedId)));

        Assert.Equal(2, list.Files.Count);
        Assert.All(list.Files, row => Assert.False(row.HasWarning));
    }

    [Fact]
    public void Markup_SingleOccurrenceWithExternalDuplicate_IsMarked()
    {
        var (_, _, markup) = NewGraph(
            ("A.addin", "2025", ValidXml("A", SharedId)),
            ("B.addin", "2025", ValidXml("B", SharedId)));

        Assert.NotEmpty(markup.DiagnosticSpans);
        var text = markup.RawXml!;
        Assert.Contains(
            markup.DiagnosticSpans,
            span => text.Substring(span.Start, span.Length).Contains(SharedId.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Markup_SingleOccurrenceWithoutExternalDuplicate_NotMarked()
    {
        var (_, _, markup) = NewGraph(("A.addin", "2025", ValidXml("A", SharedId)));

        Assert.Empty(markup.DiagnosticSpans);
    }

    [Fact]
    public void Markup_ExternalDuplicateOtherVersion_NotMarked()
    {
        var (_, _, markup) = NewGraph(
            ("A.addin", "2025", ValidXml("A", SharedId)),
            ("B.addin", "2026", ValidXml("B", SharedId)));

        // Выбран первый файл (2025); дубль лежит только в 2026 — маркировки нет.
        Assert.Empty(markup.DiagnosticSpans);
    }

    [Fact]
    public void WarningTooltipTitle_Localized()
    {
        var fileRow = NewRow(ValidXml("A", SharedId));
        var entry = _parser.Parse(ValidXml("A", SharedId)).Entries[0];
        var entryRow = new AddinEntryRowViewModel(entry, 1, TestLocalization.For<AddinEntryRowViewModel>());

        TestCulture.RunIn("en", () =>
        {
            Assert.Equal("Warning", fileRow.WarningTooltipTitle);
            Assert.Equal("Warning", entryRow.WarningTooltipTitle);
        });
        TestCulture.RunIn("ru", () =>
        {
            Assert.Equal("Предупреждение", fileRow.WarningTooltipTitle);
            Assert.Equal("Предупреждение", entryRow.WarningTooltipTitle);
        });
    }

    [Fact]
    public void EntryRow_EmptyFields_Warns()
    {
        var entry = _parser.Parse(ValidXml("A", SharedId)).Entries[0] with { AssemblyPath = string.Empty };
        var row = new AddinEntryRowViewModel(entry, 1, TestLocalization.For<AddinEntryRowViewModel>());

        Assert.True(row.HasWarning);
        Assert.NotNull(row.WarningMessage);
    }

    private AddinFileRowViewModel NewRow(string xml)
    {
        var file = new AddinFile(
            "A.addin", AddinScope.User, "2025", true,
            @"C:\fake\2025\A.addin", @"C:\fake\2025",
            _parser.Parse(xml));
        return new AddinFileRowViewModel(
            new FakeAddinStore(), file, NullLogger<AddinFileRowViewModel>.Instance,
            TestLocalization.For<AddinFileRowViewModel>());
    }

    private (ListViewModel List, EntriesViewModel Entries, MarkupViewModel Markup) NewGraph(
        params (string FileName, string Version, string Xml)[] files)
    {
        var userRoot = NewRoot();
        var machineRoot = NewRoot();
        foreach (var (fileName, version, xml) in files)
        {
            var versionDir = Path.Combine(userRoot, "Autodesk", "Revit", "Addins", version);
            Directory.CreateDirectory(versionDir);
            File.WriteAllText(Path.Combine(versionDir, fileName), xml);
        }

        var store = new FileSystemAddinStore(_parser, NullLogger<FileSystemAddinStore>.Instance, new FakeRevitProcessGuard(), TestLocalization.For<FileSystemAddinStore>(), userRoot, machineRoot);
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
            list, list,
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
            entries,
            list);

        return (list, entries, markup);
    }

    private static string ValidXml(string name, Guid id) => $"""
        <RevitAddIns>
          <AddIn Type="Application">
            <Name>{name}</Name>
            <Assembly>a.dll</Assembly>
            <AddInId>{id}</AddInId>
            <FullClassName>A.App</FullClassName>
          </AddIn>
        </RevitAddIns>
        """;

    private static AddinEntry NewEntry(string name, Guid id) =>
        new(AddinEntryType.Application, "Application", name, null, null, null,
            "a.dll", id, "A.App", null, null, null, [], [], null, null, null, [], []);

    private static ListViewModel NewList(FakeAddinStore store) =>
        new(store,
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

    private EntriesViewModel NewEntries(ListViewModel list) =>
        new(list, list,
            new FakeLocalizationService(),
            TestLocalization.For<EntriesViewModel>(),
            new AddinEntryRowViewModelFactory(TestLocalization.For<AddinEntryRowViewModel>()),
            _parser,
            new FileAddinMarkupService(_parser, NullLogger<FileAddinMarkupService>.Instance, new FakeRevitProcessGuard(), TestLocalization.For<FileAddinMarkupService>()),
            new FakeDialogService(),
            new RecordingUiDispatcher(),
            new FakeRevitProcessGuard());

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
