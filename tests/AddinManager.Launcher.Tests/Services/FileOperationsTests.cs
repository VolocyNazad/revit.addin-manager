using System.IO;
using AddinManager.Core.Manifests;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.Services;

/// <summary>
/// Файловые операции: какой результат они отдают зоне списка (перечитать, выбрать, баннер, тост)
/// и какие мутации/undo-записи делают. Поведение команд целиком покрыто тестами
/// <see cref="ViewModels.ListViewModelMoveCopyTests"/> и соседними.
/// </summary>
public sealed class FileOperationsTests
{
    [Fact]
    public void AddFile_DialogCancelled_ReturnsNone()
    {
        var store = new FakeAddinStore();
        var dialog = new FakeDialogService { NewFileResult = null };
        IFileOperations operations = NewOperations(store, dialog);

        FileOperationOutcome outcome = operations.AddFile();

        Assert.Same(FileOperationOutcome.None, outcome);
    }

    [Fact]
    public void AddFile_StoreThrows_ReturnsBannerError()
    {
        var store = new FakeAddinStore { ThrowOnNextCreateFile = new IOException("Файл занят") };
        var dialog = new FakeDialogService
        {
            NewFileResult = new NewFileOptions("New", AddinScope.User, "2025", Disabled: false),
        };
        IFileOperations operations = NewOperations(store, dialog);

        FileOperationOutcome outcome = operations.AddFile();

        Assert.False(outcome.RefreshNeeded);
        Assert.Equal("Файл занят", outcome.BannerError);
    }

    [Fact]
    public void DeleteSelected_PartialFailure_ReturnsRefreshAndBannerErrorAndRecordsUndo()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin"), NewFile("B.addin"));
        store.ThrowOnNextDelete = new IOException("Файл занят");
        var undo = ListViewModelFixture.CreateUndoService(store);
        IFileOperations operations = NewOperations(store, new FakeDialogService(), undo);
        var rows = new[]
        {
            NewRow(store, "A.addin"),
            NewRow(store, "B.addin"),
        };

        FileOperationOutcome outcome = operations.DeleteSelected(rows);

        Assert.True(outcome.RefreshNeeded);
        Assert.Equal("Файл занят", outcome.BannerError);
        Assert.True(undo.HasUndo);
    }

    [Fact]
    public void MoveCopy_Move_ReturnsTargetSelectionRevealAndRecordsUndo()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin"));
        var dialog = new FakeDialogService
        {
            MoveCopyResult = new MoveCopyOptions("2026", AddinScope.Machine, EnableAfter: true, Overwrite: false),
        };
        var undo = ListViewModelFixture.CreateUndoService(store);
        IFileOperations operations = NewOperations(store, dialog, undo);
        AddinFileRowViewModel row = NewRow(store, "A.addin");

        FileOperationOutcome outcome = operations.MoveCopy(row, MoveCopyKind.Move);

        Assert.True(outcome.RefreshNeeded);
        Assert.Equal(new AddinFileIdentity("A.addin", AddinScope.Machine, "2026"), outcome.SelectIdentity);
        Assert.Equal("2026", outcome.RevealTarget?.Version);
        Assert.True(undo.HasUndo);
    }

    [Fact]
    public void MoveCopy_DirtyEditorCancelled_ReturnsNoneAndDoesNotMove()
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
        IFileOperations operations = NewOperations(store, dialog, dirtyState: dirty);
        AddinFileRowViewModel row = NewRow(store, "A.addin");

        FileOperationOutcome outcome = operations.MoveCopy(row, MoveCopyKind.Move);

        Assert.Same(FileOperationOutcome.None, outcome);
        Assert.Equal(0, store.MoveCallCount);
    }

    private static IFileOperations NewOperations(
        FakeAddinStore store,
        FakeDialogService dialog,
        IUndoService? undoService = null,
        IEditorDirtyState? dirtyState = null) =>
        ListViewModelFixture.CreateOperations(store, dialog, undoService, dirtyState);

    private static AddinFileRowViewModel NewRow(FakeAddinStore store, string fileName)
    {
        AddinFile file = NewFile(fileName);
        return new AddinFileRowViewModel(
            store, file, NullLogger<AddinFileRowViewModel>.Instance,
            TestLocalization.For<AddinFileRowViewModel>());
    }

    private static AddinFile NewFile(string fileName) =>
        new(fileName, AddinScope.User, "2025", Enabled: true,
            FullPath: $@"C:\fake\2025\{fileName}",
            VersionRootDirectory: @"C:\fake\2025",
            Manifest: new AddinManifest([], null, null, null, null, [], [], []));
}
