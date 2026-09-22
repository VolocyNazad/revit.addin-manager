using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Composition;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Строки записей обязан создавать контейнер через фабрику: подпанель читает файл, а сами
/// строки собирает <see cref="Launcher.Abstractions.Composition.IAddinEntryRowViewModelFactory"/> (прямого <c>new</c> в
/// прод-коде нет — см. политику в docs/policies/development.md, другая сборка, отсюда без
/// <c>cref</c> на сам документ).
/// </summary>
public sealed class EntriesViewModelFactoryTests
{
    private static readonly IAddinManifestParser Parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());

    [Fact]
    public void LoadFrom_CreatesRowsThroughFactory()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("Two.addin", "2025"));
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
        var factory = new FakeEntryRowFactory();

        var entries = new EntriesViewModel(
            list, list,
            new FakeLocalizationService(),
            TestLocalization.For<EntriesViewModel>(),
            factory,
            Parser,
            new FileAddinMarkupService(Parser, NullLogger<FileAddinMarkupService>.Instance, new FakeRevitProcessGuard(), TestLocalization.For<FileAddinMarkupService>()),
            new FakeDialogService(),
            new RecordingUiDispatcher(),
            new FakeRevitProcessGuard());

        Assert.Equal(2, factory.CreatedEntries.Count);
        Assert.Equal([1, 2], factory.CreatedEntries.Select(e => e.Index));
        Assert.Equal(2, entries.Entries.Count);
    }

    private static AddinFile NewFile(string fileName, string version) =>
        new(fileName, AddinScope.User, version, Enabled: true,
            FullPath: $@"C:\fake\{version}\{fileName}",
            VersionRootDirectory: $@"C:\fake\{version}",
            Manifest: Parser.Parse(TwoEntryXml()));

    private static string TwoEntryXml() => """
        <RevitAddIns>
          <AddIn Type="Application">
            <Name>First</Name>
            <Assembly>a.dll</Assembly>
            <AddInId>11111111-1111-1111-1111-111111111111</AddInId>
            <FullClassName>A.First</FullClassName>
          </AddIn>
          <AddIn Type="Application">
            <Name>Second</Name>
            <Assembly>a.dll</Assembly>
            <AddInId>22222222-2222-2222-2222-222222222222</AddInId>
            <FullClassName>A.Second</FullClassName>
          </AddIn>
        </RevitAddIns>
        """;
}
