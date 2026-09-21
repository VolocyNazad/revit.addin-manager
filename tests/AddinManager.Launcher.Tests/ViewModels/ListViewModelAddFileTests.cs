using AddinManager.Core.Storage;
using AddinManager.Launcher.Composition;
using AddinManager.Launcher.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Создание файла из диалога: подтверждение — файл в сторе и списке, отмена — ничего,
/// ошибка стора — баннер, живой Revit — кнопка недоступна.
/// </summary>
public sealed class ListViewModelAddFileTests
{
    [Fact]
    public void AddFile_Confirmed_CreatesAndRefreshes()
    {
        var store = new FakeAddinStore();
        var dialog = new FakeDialogService
        {
            NewFileResult = new NewFileOptions("Fresh", AddinScope.User, "2025", Disabled: false),
        };
        var list = NewList(store, dialog);
        Assert.Equal(0, list.FileCount);

        list.AddFileCommand.Execute(null);

        Assert.Equal(1, list.FileCount);
        Assert.Equal("Fresh.addin", list.SelectedFile?.FileName);
        Assert.Null(list.ErrorMessage);
    }

    [Fact]
    public void AddFile_Cancelled_DoesNothing()
    {
        var store = new FakeAddinStore();
        var dialog = new FakeDialogService { NewFileResult = null };
        var list = NewList(store, dialog);
        var scansBeforeAdd = store.ScanCallCount;

        list.AddFileCommand.Execute(null);

        Assert.Equal(0, list.FileCount);
        Assert.Equal(scansBeforeAdd, store.ScanCallCount);
    }

    [Fact]
    public void AddFile_StoreThrows_ShowsBanner()
    {
        var store = new FakeAddinStore();
        var dialog = new FakeDialogService
        {
            NewFileResult = new NewFileOptions("A", AddinScope.User, "2025", Disabled: false),
        };
        var list = NewList(store, dialog);
        store.ThrowOnNextCreateFile = new System.IO.IOException("boom");

        list.AddFileCommand.Execute(null);

        Assert.Equal("boom", list.ErrorMessage);
        Assert.Equal(0, list.FileCount);
    }

    [Fact]
    public void AddFile_Locked_CannotExecute()
    {
        var guard = new FakeRevitProcessGuard();
        var list = NewList(store: new FakeAddinStore(), dialog: new FakeDialogService(), guard: guard);
        Assert.True(list.AddFileCommand.CanExecute(null));

        guard.RunningVersions = new HashSet<string>(["2025"], StringComparer.Ordinal);
        guard.RaiseChanged();

        Assert.False(list.AddFileCommand.CanExecute(null));
    }

    private static ListViewModel NewList(
        FakeAddinStore store, FakeDialogService dialog, FakeRevitProcessGuard? guard = null) =>
        new(
            store,
            new FakeAddinChangeWatcher(),
            new RecordingUiDispatcher(),
            NullLogger<ListViewModel>.Instance,
            new FakeLocalizationService(),
            TestLocalization.For<ListViewModel>(),
            new AddinFileRowViewModelFactory(store, NullLoggerFactory.Instance, TestLocalization.For<AddinFileRowViewModel>()),
            new FakeToastService(),
            guard ?? new FakeRevitProcessGuard(),
            dialog);
}
