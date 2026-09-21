using AddinManager.Core.Storage;
using AddinManager.Launcher.Composition;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Пакетный выбор списка: счётчик, переключатели вкл/выкл и всё/снять, удаление с одним
/// подтверждением, блок при живом Revit. Только новые тесты, существующие не трогаем.
/// </summary>
public sealed class ListViewModelBulkSelectionTests
{
    [Fact]
    public void RowSelection_SurvivesFilterChange()
    {
        var store = new FakeAddinStore();
        store.SetFiles(
            "2025",
            NewFile("A.addin", AddinScope.User, "2025", enabled: false),
            NewFile("B.addin", AddinScope.Machine, "2025", enabled: false));
        var list = NewList(store);
        list.Files.Single(f => f.FileName == "A.addin").IsSelected = true;

        list.SetScopeFilterCommand.Execute(ScopeFilter.Machine);
        list.SetScopeFilterCommand.Execute(ScopeFilter.All);

        Assert.Equal(1, list.SelectedCount);
        Assert.True(list.HasSelection);
        Assert.True(list.Files.Single(f => f.FileName == "A.addin").IsSelected);
        Assert.False(list.Files.Single(f => f.FileName == "B.addin").IsSelected);
    }

    [Fact]
    public void ToggleSelectAll_PartialSelection_SelectsVisible()
    {
        var store = new FakeAddinStore();
        store.SetFiles(
            "2025",
            NewFile("A.addin", AddinScope.User, "2025", enabled: false),
            NewFile("B.addin", AddinScope.Machine, "2025", enabled: false));
        var list = NewList(store);
        list.SetScopeFilterCommand.Execute(ScopeFilter.User);

        list.ToggleSelectAllCommand.Execute(null);

        Assert.Equal(1, list.SelectedCount);
        Assert.True(list.Files.Single(f => f.FileName == "A.addin").IsSelected);
        Assert.False(list.Files.Single(f => f.FileName == "B.addin").IsSelected);
    }

    [Fact]
    public void ToggleSelectAll_FullSelection_ClearsAll()
    {
        var store = new FakeAddinStore();
        store.SetFiles(
            "2025",
            NewFile("A.addin", AddinScope.User, "2025", enabled: false),
            NewFile("B.addin", AddinScope.User, "2025", enabled: false));
        var list = NewList(store);
        list.Files.Single(f => f.FileName == "A.addin").IsSelected = true;
        list.Files.Single(f => f.FileName == "B.addin").IsSelected = true;

        list.ToggleSelectAllCommand.Execute(null);

        Assert.Equal(0, list.SelectedCount);
        Assert.False(list.HasSelection);
    }

    [Fact]
    public void ToggleSelected_AnyDisabled_EnablesSelected()
    {
        var store = new FakeAddinStore();
        store.SetFiles(
            "2025",
            NewFile("A.addin", AddinScope.User, "2025", enabled: false),
            NewFile("B.addin", AddinScope.User, "2025", enabled: true));
        var list = NewList(store);
        list.Files.Single(f => f.FileName == "A.addin").IsSelected = true;
        list.Files.Single(f => f.FileName == "B.addin").IsSelected = true;

        list.ToggleSelectedCommand.Execute(null);

        Assert.True(list.Files.Single(f => f.FileName == "A.addin").IsEnabled);
        Assert.True(list.Files.Single(f => f.FileName == "B.addin").IsEnabled);
    }

    [Fact]
    public void ToggleSelected_AllEnabled_DisablesSelected()
    {
        var store = new FakeAddinStore();
        store.SetFiles(
            "2025",
            NewFile("A.addin", AddinScope.User, "2025", enabled: true),
            NewFile("B.addin", AddinScope.User, "2025", enabled: true));
        var list = NewList(store);
        list.Files.Single(f => f.FileName == "A.addin").IsSelected = true;

        list.ToggleSelectedCommand.Execute(null);

        Assert.False(list.Files.Single(f => f.FileName == "A.addin").IsEnabled);
        Assert.True(list.Files.Single(f => f.FileName == "B.addin").IsEnabled);
    }

    [Fact]
    public void DeleteSelected_Confirmed_RemovesSelected()
    {
        var store = new FakeAddinStore();
        store.SetFiles(
            "2025",
            NewFile("A.addin", AddinScope.User, "2025", enabled: true),
            NewFile("B.addin", AddinScope.User, "2025", enabled: true));
        var dialog = new FakeDialogService { Result = true };
        var list = NewList(store, dialog);
        list.Files.Single(f => f.FileName == "A.addin").IsSelected = true;

        list.DeleteSelectedCommand.Execute(null);

        Assert.Equal(1, list.FileCount);
        Assert.Equal("B.addin", list.Files.Single().FileName);
        Assert.Single(dialog.ShownMessages);
    }

    [Fact]
    public void DeleteSelected_Cancelled_KeepsFiles()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin", AddinScope.User, "2025", enabled: true));
        var dialog = new FakeDialogService { Result = false };
        var list = NewList(store, dialog);
        list.Files.Single().IsSelected = true;

        list.DeleteSelectedCommand.Execute(null);

        Assert.Equal(1, list.FileCount);
    }

    [Fact]
    public void BulkCommands_Locked_CannotExecute()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin", AddinScope.User, "2025", enabled: false));
        var guard = new FakeRevitProcessGuard();
        var list = NewList(store, dialog: new FakeDialogService(), guard: guard);
        list.Files.Single().IsSelected = true;
        Assert.True(list.ToggleSelectedCommand.CanExecute(null));

        guard.RunningVersions = new HashSet<string>(["2025"], StringComparer.Ordinal);
        guard.RaiseChanged();

        Assert.False(list.ToggleSelectedCommand.CanExecute(null));
        Assert.False(list.DeleteSelectedCommand.CanExecute(null));
    }

    private static ListViewModel NewList(
        FakeAddinStore store, FakeDialogService? dialog = null, FakeRevitProcessGuard? guard = null) =>
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
            dialog ?? new FakeDialogService());

    private static AddinFile NewFile(string fileName, AddinScope scope, string version, bool enabled) =>
        new(fileName, scope, version, enabled,
            FullPath: $@"C:\fake\{version}\{fileName}",
            VersionRootDirectory: $@"C:\fake\{version}",
            Manifest: new AddinManager.Core.Manifests.AddinManifest([], null, null, null, null, [], [], []));
}
