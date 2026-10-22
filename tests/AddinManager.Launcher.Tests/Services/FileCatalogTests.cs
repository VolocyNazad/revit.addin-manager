using AddinManager.Core;
using AddinManager.Core.Manifests;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Composition;
using AddinManager.Launcher.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.Services;

/// <summary>
/// Каталог строк списка: сканирование всех версий, пересоздание строк, сохранение пакетной
/// отметки по идентичности и кросс-файловые предупреждения.
/// </summary>
public sealed class FileCatalogTests
{
    private static readonly Guid SharedId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Refresh_ScansEverySupportedVersion()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin", "2025"));
        store.SetFiles("2026", NewFile("B.addin", "2026"));
        FileCatalog catalog = NewCatalog(store);

        catalog.Refresh();

        Assert.Equal(2, catalog.Files.Count);
        Assert.Equal(RevitVersionCatalog.SupportedVersions.Count, store.ScanCallCount);
    }

    [Fact]
    public void Refresh_PreservesBulkSelectionByIdentity()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin", "2025"), NewFile("B.addin", "2025"));
        FileCatalog catalog = NewCatalog(store);
        catalog.Refresh();
        catalog.Files.Single(row => row.FileName == "A.addin").IsSelected = true;

        catalog.Refresh();

        Assert.True(catalog.Files.Single(row => row.FileName == "A.addin").IsSelected);
        Assert.False(catalog.Files.Single(row => row.FileName == "B.addin").IsSelected);
    }

    [Fact]
    public void Refresh_MarksAcrossFileDuplicateWarnings()
    {
        var store = new FakeAddinStore();
        store.SetFiles(
            "2025",
            NewFile("First.addin", "2025", SharedId),
            NewFile("Second.addin", "2025", SharedId));
        FileCatalog catalog = NewCatalog(store);

        catalog.Refresh();

        Assert.All(catalog.Files, row => Assert.True(row.HasWarning));
    }

    [Fact]
    public void Refresh_SameIdInDifferentVersions_IsNotAcrossFileWarning()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("First.addin", "2025", SharedId));
        store.SetFiles("2026", NewFile("Second.addin", "2026", SharedId));
        FileCatalog catalog = NewCatalog(store);

        catalog.Refresh();

        Assert.All(catalog.Files, row => Assert.False(row.HasWarning));
    }

    private static FileCatalog NewCatalog(FakeAddinStore store) =>
        new(
            store,
            new AddinFileRowViewModelFactory(
                store, NullLoggerFactory.Instance, TestLocalization.For<AddinFileRowViewModel>()),
            NullLogger<FileCatalog>.Instance);

    private static AddinFile NewFile(string fileName, string version, params Guid[] addInIds)
    {
        AddinEntry[] entries = [.. addInIds.Select(NewEntry)];
        var manifest = new AddinManifest(entries, null, null, null, null, [], [], []);
        return new AddinFile(
            fileName, AddinScope.User, version, Enabled: true,
            FullPath: $@"C:\fake\{version}\{fileName}",
            VersionRootDirectory: $@"C:\fake\{version}",
            Manifest: manifest);
    }

    private static AddinEntry NewEntry(Guid addInId) => new(
        AddinEntryType.Application,
        "Application",
        "Entry",
        null,
        null,
        null,
        "AddinManager.dll",
        addInId,
        "AddinManager.Command",
        null,
        null,
        null,
        [],
        [],
        null,
        null,
        null,
        [],
        []);
}
