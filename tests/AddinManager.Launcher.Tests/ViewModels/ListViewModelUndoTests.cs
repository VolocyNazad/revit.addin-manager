using System.IO;
using AddinManager.Core.Manifests;
using AddinManager.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Ctrl+Z для последнего перемещения enabled/disabled: действие запоминается по событию строки,
/// команда возвращает прежнее состояние и очищает запись.
/// </summary>
public sealed class ListViewModelUndoTests
{
    [Fact]
    public void Undo_WithoutAction_CannotExecute()
    {
        var list = NewList(new FakeAddinStore());

        Assert.False(list.HasUndo);
        Assert.False(list.UndoLastActionCommand.CanExecute(null));
    }

    [Fact]
    public void Toggle_Row_RecordsUndo_AndUndoRestoresState()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin", enabled: true));
        var list = NewList(store);
        var row = Assert.Single(list.Files);

        row.IsEnabled = false;

        Assert.True(list.HasUndo);
        Assert.True(list.UndoLastActionCommand.CanExecute(null));

        list.UndoLastActionCommand.Execute(null);

        Assert.False(list.HasUndo);
        Assert.True(Assert.Single(list.Files).IsEnabled);
    }

    [Fact]
    public void BulkToggle_UndoRestoresAllRows()
    {
        var store = new FakeAddinStore();
        store.SetFiles(
            "2025",
            NewFile("A.addin", enabled: true),
            NewFile("B.addin", enabled: true));
        var list = NewList(store);
        foreach (var row in list.Files)
            row.IsSelected = true;

        list.ToggleSelected();

        Assert.All(list.Files, row => Assert.False(row.IsEnabled));

        list.UndoLastActionCommand.Execute(null);

        Assert.All(list.Files, row => Assert.True(row.IsEnabled));
    }

    [Fact]
    public void Rename_ThenUndo_RestoresOldNameAndSelection()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin", enabled: true));
        var list = NewList(store);
        var row = Assert.Single(list.Files);
        row.BeginRename();
        row.RenameText = "B";
        row.CommitRename();

        Assert.Equal("B.addin", Assert.Single(list.Files).FileName);
        Assert.True(list.HasUndo);

        list.UndoLastActionCommand.Execute(null);

        var restored = Assert.Single(list.Files);
        Assert.Equal("A.addin", restored.FileName);
        Assert.Same(restored, list.SelectedFile);
        Assert.False(list.HasUndo);
    }

    [Fact]
    public void RenameUndo_WhenOldNameIsOccupied_ShowsErrorAndKeepsName()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin", enabled: true));
        var list = NewList(store);
        var row = Assert.Single(list.Files);
        row.BeginRename();
        row.RenameText = "B";
        row.CommitRename();
        store.ThrowOnNextRename = new IOException("Имя занято");

        list.UndoLastActionCommand.Execute(null);

        Assert.Equal("B.addin", Assert.Single(list.Files).FileName);
        Assert.Equal("Имя занято", list.ErrorMessage);
        Assert.False(list.HasUndo);
    }

    private static ListViewModel NewList(FakeAddinStore store)
    {
        var dialog = new FakeDialogService();
        var toast = new FakeToastService();
        var undo = ListViewModelFixture.CreateUndoService(store);
        return new(
            ListViewModelFixture.CreateOperations(store, dialog, undo),
            new FakeAddinChangeWatcher(),
            new RecordingUiDispatcher(),
            NullLogger<ListViewModel>.Instance,
            new FakeLocalizationService(),
            TestLocalization.For<ListViewModel>(),
            ListViewModelFixture.CreateCatalog(store),
            toast,
            new FakeRevitProcessGuard(),
            dialog,
            new FakeFolderOpener(),
            new FakeClipboardService(),
            undo);
    }

    private static AddinFile NewFile(string fileName, bool enabled) =>
        new(fileName, AddinScope.User, "2025", enabled,
            FullPath: $@"C:\fake\2025\{fileName}",
            VersionRootDirectory: @"C:\fake\2025",
            Manifest: new AddinManifest([], null, null, null, null, [], [], []));
}
