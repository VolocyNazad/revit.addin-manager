using AddinManager.Core;
using AddinManager.Core.Manifests;
using AddinManager.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Сортировка по направлению, фильтр состояния enabled/disabled и счётчики на чипсах.
/// </summary>
public sealed class ListViewModelFilterTests
{
    [Fact]
    public void SetSortOrder_SameChip_TogglesDirection()
    {
        var store = new FakeAddinStore();
        store.SetFiles(
            "2025",
            NewFile("B.addin", AddinScope.User, enabled: true),
            NewFile("A.addin", AddinScope.User, enabled: true),
            NewFile("C.addin", AddinScope.User, enabled: true));
        var list = NewList(store);

        Assert.Equal(["A.addin", "B.addin", "C.addin"], Names(list));

        list.SetSortOrder(ListSortOrder.Name);

        Assert.True(list.SortDescending);
        Assert.Equal(["C.addin", "B.addin", "A.addin"], Names(list));

        list.SetSortOrder(ListSortOrder.Name);

        Assert.False(list.SortDescending);
        Assert.Equal(["A.addin", "B.addin", "C.addin"], Names(list));
    }

    [Fact]
    public void ToggleSortDirection_ChangesOrder()
    {
        var store = new FakeAddinStore();
        store.SetFiles(
            "2025",
            NewFile("A.addin", AddinScope.User, enabled: true),
            NewFile("B.addin", AddinScope.User, enabled: true));
        var list = NewList(store);

        list.ToggleSortDirectionCommand.Execute(null);

        Assert.True(list.SortDescending);
        Assert.Equal(["B.addin", "A.addin"], Names(list));
        Assert.Equal("↓", list.SortDirectionGlyph);
    }

    [Fact]
    public void EnabledFilter_ShowsOnlyRequestedState()
    {
        var store = new FakeAddinStore();
        store.SetFiles(
            "2025",
            NewFile("On.addin", AddinScope.User, enabled: true),
            NewFile("Off.addin", AddinScope.User, enabled: false));
        var list = NewList(store);

        list.SetEnabledFilter(EnabledFilter.Disabled);

        var row = Assert.Single(list.FilesView.Cast<AddinFileRowViewModel>());
        Assert.Equal("Off.addin", row.FileName);
        Assert.Contains("1", list.EnabledOffChipLabel);
    }

    [Fact]
    public void ChipLabels_CountFilesByAxis()
    {
        var store = new FakeAddinStore();
        store.SetFiles(
            "2025",
            NewFile("A.addin", AddinScope.User, enabled: true),
            NewFile("B.addin", AddinScope.Machine, enabled: false),
            NewFile("C.addin", AddinScope.User, enabled: true));
        var list = NewList(store);

        Assert.Equal("2025 · 3", list.VersionChips.Single(chip => chip.Value == "2025").Label);
        Assert.Equal("User · 2", list.ScopeUserChipLabel);
        Assert.Equal("Machine · 1", list.ScopeMachineChipLabel);
        Assert.EndsWith(" · 3", list.ScopeAllChipLabel);
        Assert.EndsWith(" · 2", list.EnabledOnChipLabel);
        Assert.EndsWith(" · 1", list.EnabledOffChipLabel);
    }

    [Fact]
    public void VersionChips_ComeFromCatalog_AndTrackActiveFilter()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin", AddinScope.User, enabled: true));
        var list = NewList(store);

        Assert.Equal(RevitVersionCatalog.SupportedVersions.Count + 1, list.VersionChips.Count);
        Assert.True(list.VersionChips[0].IsActive);

        list.VersionFilter = "2025";

        Assert.False(list.VersionChips[0].IsActive);
        Assert.True(list.VersionChips.Single(chip => chip.Value == "2025").IsActive);
        Assert.False(list.VersionChips.Single(chip => chip.Value == "2026").IsActive);
    }

    private static string[] Names(ListViewModel list) =>
        [.. list.FilesView.Cast<AddinFileRowViewModel>().Select(row => row.FileName)];

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

    private static AddinFile NewFile(string fileName, AddinScope scope, bool enabled) =>
        new(fileName, scope, "2025", enabled,
            FullPath: $@"C:\fake\2025\{fileName}",
            VersionRootDirectory: @"C:\fake\2025",
            Manifest: new AddinManifest([], null, null, null, null, [], [], []));
}
