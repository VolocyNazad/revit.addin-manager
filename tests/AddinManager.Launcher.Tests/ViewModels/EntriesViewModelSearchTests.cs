using System.IO;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Поиск по записям: <see cref="EntriesViewModel.EntriesView"/> — это <see cref="EntriesViewModel.Entries"/>
/// с применённым фильтром по <see cref="EntriesViewModel.SearchText"/> (имя/тип/сборка/класс),
/// пустой текст показывает всё.
/// </summary>
public sealed class EntriesViewModelSearchTests : IDisposable
{
    private readonly TempDirectorySet _tempDirectories = new();
    private readonly IAddinManifestParser _parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());

    [Fact]
    public void SearchText_MatchesDisplayName()
    {
        var entries = NewEntries();

        entries.SearchText = "first";

        Assert.Equal("First", entries.EntriesView.Cast<AddinEntryRowViewModel>().Single().DisplayName);
    }

    [Fact]
    public void SearchText_MatchesType()
    {
        var entries = NewEntries();

        entries.SearchText = "command";

        Assert.Equal("Second", entries.EntriesView.Cast<AddinEntryRowViewModel>().Single().DisplayName);
    }

    [Fact]
    public void SearchText_MatchesAssemblyPath()
    {
        var entries = NewEntries();

        entries.SearchText = "b.dll";

        Assert.Equal("Second", entries.EntriesView.Cast<AddinEntryRowViewModel>().Single().DisplayName);
    }

    [Fact]
    public void SearchText_MatchesFullClassName()
    {
        var entries = NewEntries();

        entries.SearchText = "a.first";

        Assert.Equal("First", entries.EntriesView.Cast<AddinEntryRowViewModel>().Single().DisplayName);
    }

    [Fact]
    public void SearchText_NoMatch_ShowsNothing()
    {
        var entries = NewEntries();

        entries.SearchText = "zzz";

        Assert.Empty(entries.EntriesView.Cast<AddinEntryRowViewModel>());
    }

    [Fact]
    public void SearchText_EmptyOrWhitespace_ShowsAll()
    {
        var entries = NewEntries();

        entries.SearchText = "   ";

        Assert.Equal(2, entries.EntriesView.Cast<AddinEntryRowViewModel>().Count());
    }

    private EntriesViewModel NewEntries()
    {
        var userRoot = NewRoot();
        var versionDir = Path.Combine(userRoot, "Autodesk", "Revit", "Addins", "2025");
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "A.addin"), TwoEntryXml());

        var stoppedGuard = new FakeRevitProcessGuard();
        var store = new FileSystemAddinStore(_parser, NullLogger<FileSystemAddinStore>.Instance, stoppedGuard, TestLocalization.For<FileSystemAddinStore>(), userRoot, NewRoot());
        var list = ListViewModelFixture.Create(store, guard: stoppedGuard);

        return EditorZoneFixture.CreateEntries(_parser, list, list, guard: stoppedGuard);
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

    private string NewRoot() => _tempDirectories.Create();

    /// <inheritdoc />
    public void Dispose()
    {
        _tempDirectories.Dispose();
    }
}
