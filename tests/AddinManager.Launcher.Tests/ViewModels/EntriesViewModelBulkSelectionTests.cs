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
/// Пакетный выбор записей: счётчик, снятие выбора, удаление одним сохранением после
/// одного подтверждения, блок при живом Revit. Только новые тесты, существующие не трогаем.
/// </summary>
public sealed class EntriesViewModelBulkSelectionTests : IDisposable
{
    private readonly List<string> _tempRoots = [];
    private readonly IAddinManifestParser _parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());

    [Fact]
    public void DeleteSelected_Confirmed_RemovesSelectedWithSingleConfirm()
    {
        var (entries, dialog, path) = NewGraph(ThreeEntryXml());
        entries.Entries.Single(e => e.DisplayName == "Second").IsSelected = true;
        entries.Entries.Single(e => e.DisplayName == "Third").IsSelected = true;
        Assert.Equal(2, entries.SelectedCount);

        TestCulture.RunIn("en", () => entries.DeleteSelectedCommand.Execute(null));

        Assert.Single(dialog.ShownMessages);
        Assert.Equal("Delete 2 entries permanently?", dialog.ShownMessages[0]);
        Assert.DoesNotContain("22222222-2222-2222-2222-222222222222", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.DoesNotContain("33333333-3333-3333-3333-333333333333", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.Single(entries.Entries);
        Assert.Equal("First", entries.Entries.Single().DisplayName);
    }

    [Fact]
    public void DeleteSelected_Declined_KeepsEntries()
    {
        var (entries, dialog, path) = NewGraph(ThreeEntryXml());
        var before = File.ReadAllText(path);
        entries.Entries.Single(e => e.DisplayName == "Second").IsSelected = true;
        dialog.Result = false;

        entries.DeleteSelectedCommand.Execute(null);

        Assert.Single(dialog.ShownMessages);
        Assert.Equal(3, entries.Entries.Count);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public void DeleteSelected_Locked_CannotExecute()
    {
        var guard = new FakeRevitProcessGuard();
        var (entries, _, _) = NewGraph(ThreeEntryXml(), entriesGuard: guard);
        entries.Entries[0].IsSelected = true;
        Assert.True(entries.DeleteSelectedCommand.CanExecute(null));

        guard.RunningVersions = new HashSet<string>(["2025"], StringComparer.Ordinal);
        guard.RaiseChanged();

        Assert.False(entries.DeleteSelectedCommand.CanExecute(null));
    }

    [Fact]
    public void ToggleSelectAll_PartialSelection_SelectsOnlyFiltered()
    {
        var (entries, _, _) = NewGraph(ThreeEntryXml());
        entries.SearchText = "Second";
        entries.Entries.Single(e => e.DisplayName == "First").IsSelected = true;

        entries.ToggleSelectAllCommand.Execute(null);

        Assert.Equal(2, entries.SelectedCount);
        Assert.True(entries.Entries.Single(e => e.DisplayName == "Second").IsSelected);
    }

    [Fact]
    public void ToggleSelectAll_FullSelection_ClearsAll()
    {
        var (entries, _, _) = NewGraph(ThreeEntryXml());
        entries.Entries[0].IsSelected = true;
        entries.Entries[1].IsSelected = true;
        entries.Entries[2].IsSelected = true;

        entries.ToggleSelectAllCommand.Execute(null);

        Assert.Equal(0, entries.SelectedCount);
        Assert.False(entries.HasSelection);
    }

    private (EntriesViewModel Entries, FakeDialogService Dialog, string Path) NewGraph(
        string xml, FakeRevitProcessGuard? entriesGuard = null)
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
            list,
            new FakeLocalizationService(),
            TestLocalization.For<EntriesViewModel>(),
            new AddinEntryRowViewModelFactory(TestLocalization.For<AddinEntryRowViewModel>()),
            _parser,
            new FileAddinMarkupService(_parser, NullLogger<FileAddinMarkupService>.Instance, stoppedGuard, TestLocalization.For<FileAddinMarkupService>()),
            dialog,
            new RecordingUiDispatcher(),
            entriesGuard ?? stoppedGuard);

        return (entries, dialog, path);
    }

    private static string ThreeEntryXml() => """
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
          <AddIn Type="Command">
            <Text>Third</Text>
            <Assembly>c.dll</Assembly>
            <AddInId>33333333-3333-3333-3333-333333333333</AddInId>
            <FullClassName>A.Third</FullClassName>
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
