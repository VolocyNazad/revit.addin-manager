using AddinManager.Core.Manifests;
using AddinManager.Core.Storage;
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
    public void CopyTo_ThenUndo_RemovesCopyAndSelectsSource()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin"));
        var dialog = new FakeDialogService
        {
            MoveCopyResult = new MoveCopyOptions("2026", AddinScope.User, EnableAfter: true, Overwrite: false),
        };
        var list = NewList(store, dialog);
        var source = list.Files.Single();

        list.CopyToCommand.Execute(source);
        Assert.Equal(2, list.Files.Count);
        Assert.True(list.HasUndo);

        list.UndoLastActionCommand.Execute(null);

        var restored = Assert.Single(list.Files);
        Assert.Equal("2025", restored.Version);
        Assert.Same(restored, list.SelectedFile);
        Assert.False(list.HasUndo);
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

    [Fact]
    public void MoveTo_SavesDirtyEditorBeforeMoving()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin"));
        var dialog = new FakeDialogService
        {
            MoveCopyResult = new MoveCopyOptions("2026", AddinScope.User, EnableAfter: true, Overwrite: false),
        };
        var dirty = new EditorDirtyState();
        var saved = false;
        dirty.Update(isDirty: true, save: () =>
        {
            saved = true;
            return true;
        });
        var list = NewList(store, dialog, dirty: dirty);

        list.MoveToCommand.Execute(list.Files.Single());

        Assert.True(saved);
        Assert.Equal(1, store.MoveCallCount);
    }

    [Fact]
    public void MoveTo_DirtyEditorCancel_DoesNotMove()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin"));
        var dialog = new FakeDialogService
        {
            MoveCopyResult = new MoveCopyOptions("2026", AddinScope.User, EnableAfter: true, Overwrite: false),
        };
        dialog.ConfirmResults.Enqueue(false);
        dialog.ConfirmResults.Enqueue(false);
        var dirty = new EditorDirtyState();
        dirty.Update(isDirty: true, save: () => true);
        var list = NewList(store, dialog, dirty: dirty);

        list.MoveToCommand.Execute(list.Files.Single());

        Assert.Equal(0, store.MoveCallCount);
        Assert.Equal(2, dialog.ShownMessages.Count);
    }

    [Fact]
    public void MoveTo_RevealsTargetThroughFilters()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin"));
        var dialog = new FakeDialogService
        {
            MoveCopyResult = new MoveCopyOptions("2026", AddinScope.Machine, EnableAfter: true, Overwrite: false),
        };
        var list = NewList(store, dialog);
        list.ScopeFilter = ScopeFilter.User;
        list.VersionFilter = "2025";

        list.MoveToCommand.Execute(list.Files.Single());

        Assert.Equal(ScopeFilter.Machine, list.ScopeFilter);
        Assert.Equal("2026", list.VersionFilter);
        Assert.Equal("2026", list.SelectedFile?.Version);
    }

    [Fact]
    public void MoveTo_WhenRevitIsRunning_CannotExecute()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin"));
        var guard = new FakeRevitProcessGuard
        {
            RunningVersions = new HashSet<string>(["2024"], StringComparer.Ordinal),
        };
        var list = NewList(store, new FakeDialogService(), guard: guard);

        Assert.False(list.MoveToCommand.CanExecute(list.Files.Single()));
        Assert.False(list.CopyToCommand.CanExecute(list.Files.Single()));
    }

    private static ListViewModel NewList(
        FakeAddinStore store,
        FakeDialogService dialog,
        FakeToastService? toast = null,
        EditorDirtyState? dirty = null,
        FakeRevitProcessGuard? guard = null)
    {
        toast ??= new FakeToastService();
        var undo = ListViewModelFixture.CreateUndoService(store);
        return new(
            ListViewModelFixture.CreateOperations(store, dialog, undo, dirty),
            new FakeAddinChangeWatcher(),
            new RecordingUiDispatcher(),
            NullLogger<ListViewModel>.Instance,
            new FakeLocalizationService(),
            TestLocalization.For<ListViewModel>(),
            ListViewModelFixture.CreateCatalog(store),
            new FakeInstalledRevitVersions(),
            toast,
            guard ?? new FakeRevitProcessGuard(),
            dialog,
            new FakeFolderOpener(),
            new FakeClipboardService(),
            undo,
            dirty);
    }

    private static AddinFile NewFile(string fileName) =>
        new(fileName, AddinScope.User, "2025", Enabled: true,
            FullPath: $@"C:\fake\2025\{fileName}",
            VersionRootDirectory: @"C:\fake\2025",
            Manifest: new AddinManifest([], null, null, null, null, [], [], []));
}
