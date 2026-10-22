using AddinManager.Core.Manifests;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Services;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Смена выбранного файла при несохранённых правках: «Сохранить / Отбросить / Отмена»,
/// отмена оставляет выбор на прежнем файле (вид возвращается к нему отложенным уведомлением).
/// </summary>
public sealed class ListViewModelSelectionTests
{
    [Fact]
    public void Switch_WithoutDirtyEditor_SwitchesWithoutQuestions()
    {
        var (list, dialog, _, second, _) = NewGraph();

        list.SelectedFile = second;

        Assert.Same(second, list.SelectedFile);
        Assert.Empty(dialog.ShownMessages);
    }

    [Fact]
    public void Switch_DirtyEditor_SaveConfirmed_SavesAndSwitches()
    {
        var (list, dialog, _, second, dirty) = NewGraph();
        var saved = false;
        dirty.Update(isDirty: true, save: () =>
        {
            saved = true;
            return true;
        });
        dialog.ConfirmResults.Enqueue(true);

        list.SelectedFile = second;

        Assert.True(saved);
        Assert.Same(second, list.SelectedFile);
        Assert.Single(dialog.ShownMessages);
    }

    [Fact]
    public void Switch_DirtyEditor_SaveFailed_KeepsSelection()
    {
        var (list, dialog, first, second, dirty) = NewGraph();
        dirty.Update(isDirty: true, save: () => false);
        dialog.ConfirmResults.Enqueue(true);

        list.SelectedFile = second;

        Assert.Same(first, list.SelectedFile);
        Assert.Single(dialog.ShownMessages);
    }

    [Fact]
    public void Switch_DirtyEditor_DiscardConfirmed_SwitchesWithoutSaving()
    {
        var (list, dialog, _, second, dirty) = NewGraph();
        var saved = false;
        dirty.Update(isDirty: true, save: () =>
        {
            saved = true;
            return true;
        });
        dialog.ConfirmResults.Enqueue(false);
        dialog.ConfirmResults.Enqueue(true);
        list.SelectedFile = second;

        Assert.False(saved);
        Assert.Same(second, list.SelectedFile);
        Assert.Equal(2, dialog.ShownMessages.Count);
    }

    [Fact]
    public void Switch_DirtyEditor_Cancelled_KeepsSelectionAndNotifiesView()
    {
        var (list, dialog, first, second, dirty) = NewGraph();
        dirty.Update(isDirty: true, save: () => true);
        dialog.ConfirmResults.Enqueue(false);
        dialog.ConfirmResults.Enqueue(false);

        list.SelectedFile = second;

        Assert.Same(first, list.SelectedFile);
        Assert.Equal(2, dialog.ShownMessages.Count);
    }

    [Fact]
    public void Switch_Cancelled_PostsPropertyChangedSoListBoxReturnsToPreviousRow()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin"), NewFile("B.addin"));
        var dispatcher = new RecordingUiDispatcher();
        var dirty = new EditorDirtyState();
        dirty.Update(isDirty: true, save: () => true);
        var dialog = new FakeDialogService();
        dialog.ConfirmResults.Enqueue(false);
        dialog.ConfirmResults.Enqueue(false);
        var list = ListViewModelFixture.Create(
            store, dialogService: dialog, dirtyState: dirty, dispatcher: dispatcher);

        list.SelectedFile = list.Files[1];

        Assert.Equal(1, dispatcher.PostCount);
    }

    private static (ListViewModel List, FakeDialogService Dialog, AddinFileRowViewModel First, AddinFileRowViewModel Second, EditorDirtyState Dirty) NewGraph()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin"), NewFile("B.addin"));
        var dialog = new FakeDialogService();
        var dirty = new EditorDirtyState();
        var list = ListViewModelFixture.Create(store, dialogService: dialog, dirtyState: dirty);
        return (list, dialog, list.Files[0], list.Files[1], dirty);
    }

    private static AddinFile NewFile(string fileName) =>
        new(fileName, AddinScope.User, "2025", Enabled: true,
            FullPath: $@"C:\fake\2025\{fileName}",
            VersionRootDirectory: @"C:\fake\2025",
            Manifest: new AddinManifest([], null, null, null, null, [], [], []));
}
