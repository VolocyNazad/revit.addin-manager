using AddinManager.Core.Storage;
using AddinManager.Launcher.Composition;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// "Показать в папке": команда передаёт открывателю полный путь выбранного файла;
/// без выбора недоступна. Только новые тесты, существующие не трогаем.
/// </summary>
public sealed class ListViewModelShowInFolderTests
{
    [Fact]
    public void ShowInFolder_RevealsSelectedFilePath()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin", AddinScope.User, "2025", enabled: true));
        var opener = new FakeFolderOpener();
        var list = NewList(store, opener);

        list.ShowInFolderCommand.Execute(null);

        Assert.Equal([list.SelectedFile!.FullPath], opener.RevealedPaths);
    }

    [Fact]
    public void ShowInFolder_NoSelection_CannotExecute()
    {
        var list = NewList(new FakeAddinStore(), new FakeFolderOpener());

        Assert.Null(list.SelectedFile);
        Assert.False(list.ShowInFolderCommand.CanExecute(null));
    }

    private static ListViewModel NewList(FakeAddinStore store, FakeFolderOpener opener) =>
        new(
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
            opener);

    private static AddinFile NewFile(string fileName, AddinScope scope, string version, bool enabled) =>
        new(fileName, scope, version, enabled,
            FullPath: $@"C:\fake\{version}\{fileName}",
            VersionRootDirectory: $@"C:\fake\{version}",
            Manifest: new AddinManager.Core.Manifests.AddinManifest([], null, null, null, null, [], [], []));
}
