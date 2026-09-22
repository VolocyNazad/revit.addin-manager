using System.IO;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Guard;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Composition;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Удаление записи из файла: подтверждение — запись исчезает с диска и из списка; отказ —
/// ничего не трогает; живой Revit — запрет через CanExecute и через отказ сервиса.
/// </summary>
public sealed class EntriesViewModelDeleteTests : IDisposable
{
    private readonly List<string> _tempRoots = [];
    private readonly IAddinManifestParser _parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());

    [Fact]
    public void DeleteEntry_Confirmed_RemovesFromDiskAndReloads()
    {
        var (entries, dialog, path) = NewGraph(TwoEntryXml());
        var row = entries.Entries[1];
        Assert.Equal(2, entries.Entries.Count);

        TestCulture.RunIn("ru", () =>
        {
            entries.DeleteEntryCommand.Execute(row);

            Assert.Equal(["Удалить запись 'Second' навсегда?"], dialog.ShownMessages);
        });

        Assert.DoesNotContain("22222222-2222-2222-2222-222222222222", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.Single(entries.Entries);
        Assert.Equal("First", entries.SelectedEntry?.DisplayName);
        Assert.Null(entries.ErrorMessage);
    }

    [Fact]
    public void DeleteEntry_Declined_DoesNothing()
    {
        var (entries, dialog, path) = NewGraph(TwoEntryXml());
        var before = File.ReadAllText(path);
        dialog.Result = false;

        entries.DeleteEntryCommand.Execute(entries.Entries[1]);

        Assert.Single(dialog.ShownMessages);
        Assert.Equal(2, entries.Entries.Count);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public void DeleteEntry_Locked_CannotExecute()
    {
        var guard = new FakeRevitProcessGuard();
        var (entries, _, _) = NewGraph(TwoEntryXml(), entriesGuard: guard);
        Assert.True(entries.DeleteEntryCommand.CanExecute(entries.Entries[0]));
        Assert.False(entries.DeleteEntryCommand.CanExecute(null));

        guard.RunningVersions = new HashSet<string>(["2025"], StringComparer.Ordinal);
        guard.RaiseChanged();

        Assert.False(entries.DeleteEntryCommand.CanExecute(entries.Entries[0]));
    }

    [Fact]
    public void DeleteEntry_ServiceThrows_ShowsErrorAndKeepsEntries()
    {
        using var runningGuard = new PollingRevitProcessGuard(
            static () => new HashSet<string>(["2024"], StringComparer.Ordinal));
        runningGuard.Start();
        var runningMarkup = new FileAddinMarkupService(
            _parser,
            NullLogger<FileAddinMarkupService>.Instance,
            runningGuard,
            TestLocalization.For<FileAddinMarkupService>());
        var (entries, _, _) = NewGraph(TwoEntryXml(), markupService: runningMarkup);

        entries.DeleteEntryCommand.Execute(entries.Entries[1]);

        Assert.False(string.IsNullOrEmpty(entries.ErrorMessage));
        Assert.Equal(2, entries.Entries.Count);
    }

    private (EntriesViewModel Entries, FakeDialogService Dialog, string Path) NewGraph(
        string xml, IAddinMarkupService? markupService = null, FakeRevitProcessGuard? entriesGuard = null)
    {
        var userRoot = NewRoot();
        var versionDir = Path.Combine(userRoot, "Autodesk", "Revit", "Addins", "2025");
        Directory.CreateDirectory(versionDir);
        var path = Path.Combine(versionDir, "A.addin");
        File.WriteAllText(path, xml);

        var stoppedGuard = new FakeRevitProcessGuard();
        var store = new FileSystemAddinStore(_parser, NullLogger<FileSystemAddinStore>.Instance, stoppedGuard, TestLocalization.For<FileSystemAddinStore>(), userRoot, NewRoot());
        var list = new ListViewModel(
            store,
            new FakeAddinChangeWatcher(),
            new RecordingUiDispatcher(),
            NullLogger<ListViewModel>.Instance,
            new FakeLocalizationService(),
            TestLocalization.For<ListViewModel>(),
            new AddinFileRowViewModelFactory(store, NullLoggerFactory.Instance, TestLocalization.For<AddinFileRowViewModel>()),
            new FakeToastService(),
            stoppedGuard,
            new FakeDialogService(),
            new FakeFolderOpener());
        var dialog = new FakeDialogService();
        var entries = new EntriesViewModel(
            list, list,
            new FakeLocalizationService(),
            TestLocalization.For<EntriesViewModel>(),
            new AddinEntryRowViewModelFactory(TestLocalization.For<AddinEntryRowViewModel>()),
            _parser,
            markupService ?? new FileAddinMarkupService(_parser, NullLogger<FileAddinMarkupService>.Instance, stoppedGuard, TestLocalization.For<FileAddinMarkupService>()),
            dialog,
            new RecordingUiDispatcher(),
            entriesGuard ?? stoppedGuard);

        return (entries, dialog, path);
    }

    private static string TwoEntryXml() => """
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
            <AddInId>22222222-2222-2222-2222-222222222222</AddInId>
            <FullClassName>A.Second</FullClassName>
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
