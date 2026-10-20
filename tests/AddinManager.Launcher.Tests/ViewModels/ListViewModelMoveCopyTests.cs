using AddinManager.Core.Manifests;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Composition;
using AddinManager.Launcher.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Команды MoveTo/CopyTo: диалог цели, перенос/копирование через store, выбор цели после
/// перемещения, сохранение исходника после копирования и отмена перемещения через Ctrl+Z.
/// </summary>
public sealed class ListViewModelMoveCopyTests
{
    [Fact]
    public void MoveTo_RefreshesAndSelectsTarget_ThenUndoReturnsFile()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin"));
        var dialog = new FakeDialogService
        {
            MoveCopyResult = new MoveCopyOptions("2026", AddinScope.Machine, EnableAfter: true, Overwrite: false),
        };
        var list = NewList(store, dialog);

        list.MoveToCommand.Execute(list.Files.Single());

        Assert.Equal(1, store.MoveCallCount);
        var moved = Assert.Single(list.Files);
        Assert.Equal("2026", moved.Version);
        Assert.Equal(AddinScope.Machine, moved.Scope);
        Assert.Same(moved, list.SelectedFile);
        Assert.True(list.HasUndo);

        list.UndoLastActionCommand.Execute(null);

        var restored = Assert.Single(list.Files);
        Assert.Equal("2025", restored.Version);
        Assert.Equal(AddinScope.User, restored.Scope);
    }

    [Fact]
    public void CopyTo_KeepsSourceSelectedAndShowsToast()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin"));
        var dialog = new FakeDialogService
        {
            MoveCopyResult = new MoveCopyOptions("2026", AddinScope.User, EnableAfter: true, Overwrite: false),
        };
        var toast = new FakeToastService();
        var list = NewList(store, dialog, toast);

        list.CopyToCommand.Execute(list.Files.Single());

        Assert.Equal(1, store.CopyCallCount);
        Assert.Equal(2, list.Files.Count);
        var source = list.Files.Single(file => file.Version == "2025");
        Assert.Same(source, list.SelectedFile);
        Assert.Contains(toast.ShownMessages, message => message.Contains("2026"));
    }

    [Fact]
    public void MoveTo_DialogCancelledDoesNothing()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin"));
        var list = NewList(store, new FakeDialogService { MoveCopyResult = null });

        list.MoveToCommand.Execute(list.Files.Single());

        Assert.Equal(0, store.MoveCallCount);
        Assert.Equal("2025", Assert.Single(list.Files).Version);
    }

    private static ListViewModel NewList(FakeAddinStore store, FakeDialogService dialog, FakeToastService? toast = null) =>
        new(
            store,
            new FakeAddinChangeWatcher(),
            new RecordingUiDispatcher(),
            NullLogger<ListViewModel>.Instance,
            new FakeLocalizationService(),
            TestLocalization.For<ListViewModel>(),
            new AddinFileRowViewModelFactory(store, NullLoggerFactory.Instance, TestLocalization.For<AddinFileRowViewModel>()),
            toast ?? new FakeToastService(),
            new FakeRevitProcessGuard(),
            dialog,
            new FakeFolderOpener(),
            new FakeClipboardService());

    private static AddinFile NewFile(string fileName) =>
        new(fileName, AddinScope.User, "2025", Enabled: true,
            FullPath: $@"C:\fake\2025\{fileName}",
            VersionRootDirectory: @"C:\fake\2025",
            Manifest: new AddinManifest([], null, null, null, null, [], [], []));
}
