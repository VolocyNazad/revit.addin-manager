using System.IO;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Guard;
using AddinManager.Core.Manifests;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Composition;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Добавление записи (отдельное изменение, сразу в файл — новый AddInId и заголовок
/// Name/Text, остальные поля пустые) и удаление записей: подтверждение, отказ, лок, ошибка.
/// </summary>
public sealed class EntriesViewModelAddDeleteTests : IDisposable
{
    private readonly List<string> _tempRoots = [];
    private readonly IAddinManifestParser _parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());

    [Fact]
    public void AddEntry_SavesToFileImmediately()
    {
        var (_, entries, dialog, path) = NewGraph(TwoEntryXml());
        Assert.Equal(2, entries.Entries.Count);
        dialog.NewEntryResult = AddinEntryType.Command;

        entries.AddEntryCommand.Execute(null);

        Assert.Equal(3, entries.Entries.Count);
        Assert.Equal(AddinEntryType.Command, entries.SelectedEntry?.Type);
        Assert.Equal(3, _parser.Parse(File.ReadAllText(path)).Entries.Count);
    }

    [Fact]
    public void AddEntry_Command_FillsText_OtherFieldsEmpty()
    {
        var (_, entries, dialog, path) = NewGraph(TwoEntryXml());
        dialog.NewEntryResult = AddinEntryType.Command;

        TestCulture.RunIn("en", () => entries.AddEntryCommand.Execute(null));

        var added = entries.SelectedEntry!.Entry;
        Assert.Equal(AddinEntryType.Command, added.Type);
        Assert.Equal("New entry", added.Text);
        Assert.Null(added.Name);
        Assert.Equal(string.Empty, added.AssemblyPath);
        Assert.Equal(string.Empty, added.FullClassName);
        Assert.Null(added.VendorId);
        Assert.Empty(added.VisibilityModes);
        Assert.Equal(3, _parser.Parse(File.ReadAllText(path)).Entries.Count);
    }

    [Fact]
    public void AddEntry_Application_FillsName_OtherFieldsEmpty()
    {
        var (_, entries, dialog, path) = NewGraph(TwoEntryXml());
        dialog.NewEntryResult = AddinEntryType.Application;

        TestCulture.RunIn("en", () => entries.AddEntryCommand.Execute(null));

        var added = entries.SelectedEntry!.Entry;
        Assert.Equal(AddinEntryType.Application, added.Type);
        Assert.Equal("New entry", added.Name);
        Assert.Null(added.Text);
        Assert.Equal(string.Empty, added.AssemblyPath);
        Assert.Equal(string.Empty, added.FullClassName);
        Assert.Equal(3, _parser.Parse(File.ReadAllText(path)).Entries.Count);
    }

    [Fact]
    public void AddEntry_SecondAdd_AppendsAnotherEntry()
    {
        var (_, entries, dialog, path) = NewGraph(TwoEntryXml());
        dialog.NewEntryResult = AddinEntryType.DBApplication;
        entries.AddEntryCommand.Execute(null);
        var firstId = entries.SelectedEntry!.Entry.AddInId;

        dialog.NewEntryResult = AddinEntryType.Command;
        entries.AddEntryCommand.Execute(null);

        Assert.Equal(4, entries.Entries.Count);
        Assert.NotEqual(firstId, entries.SelectedEntry?.Entry.AddInId);
        Assert.Equal(4, _parser.Parse(File.ReadAllText(path)).Entries.Count);
    }

    [Fact]
    public void AddEntry_Locked_CannotExecute()
    {
        var guard = new FakeRevitProcessGuard();
        var (_, entries, _, _) = NewGraph(TwoEntryXml(), entriesGuard: guard);
        Assert.True(entries.AddEntryCommand.CanExecute(null));

        guard.RunningVersions = new HashSet<string>(["2025"], StringComparer.Ordinal);
        guard.RaiseChanged();

        Assert.False(entries.AddEntryCommand.CanExecute(null));
    }

    [Fact]
    public void DeleteEntry_Confirmed_RemovesFromDiskAndReloads()
    {
        var (_, entries, dialog, path) = NewGraph(TwoEntryXml());
        Assert.Equal(2, entries.Entries.Count);

        TestCulture.RunIn("ru", () =>
        {
            entries.DeleteEntryCommand.Execute(entries.Entries[1]);

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
        var (_, entries, dialog, path) = NewGraph(TwoEntryXml());
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
        var (_, entries, _, _) = NewGraph(TwoEntryXml(), entriesGuard: guard);
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
        var (_, entries, _, _) = NewGraph(TwoEntryXml(), markupService: runningMarkup);

        entries.DeleteEntryCommand.Execute(entries.Entries[1]);

        Assert.False(string.IsNullOrEmpty(entries.ErrorMessage));
        Assert.Equal(2, entries.Entries.Count);
    }

    private (ListViewModel List, EntriesViewModel Entries, FakeDialogService Dialog, string Path) NewGraph(
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
            new FakeDialogService());
        var dialog = new FakeDialogService();
        var entries = new EntriesViewModel(
            list,
            new FakeLocalizationService(),
            TestLocalization.For<EntriesViewModel>(),
            new AddinEntryRowViewModelFactory(TestLocalization.For<AddinEntryRowViewModel>()),
            _parser,
            markupService ?? new FileAddinMarkupService(_parser, NullLogger<FileAddinMarkupService>.Instance, stoppedGuard, TestLocalization.For<FileAddinMarkupService>()),
            dialog,
            new RecordingUiDispatcher(),
            entriesGuard ?? stoppedGuard);

        return (list, entries, dialog, path);
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
