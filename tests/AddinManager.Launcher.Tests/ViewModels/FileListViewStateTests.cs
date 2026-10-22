using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using AddinManager.Core.Manifests;
using AddinManager.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Состояние представления списка: фильтр по scope/версии/состоянию/поиску, сортировка и
/// группировка живут в одном классе над <see cref="ICollectionView"/>, а не в зоне списка.
/// </summary>
public sealed class FileListViewStateTests
{
    [Fact]
    public void Apply_SearchText_FiltersRowsByName()
    {
        FileListViewState state = NewState();

        state.Apply(Query(searchText: "B."));

        Assert.Equal(["B.addin"], Visible(state));
    }

    [Fact]
    public void Apply_ScopeFilter_KeepsMatchingScope()
    {
        FileListViewState state = NewState();

        state.Apply(Query(scope: ScopeFilter.Machine));

        Assert.Equal(["M.addin"], Visible(state));
    }

    [Fact]
    public void Apply_VersionFilter_KeepsMatchingVersion()
    {
        FileListViewState state = NewState();

        state.Apply(Query(version: "2026"));

        Assert.Equal(["M.addin"], Visible(state));
    }

    [Fact]
    public void Apply_EnabledFilter_KeepsMatchingState()
    {
        FileListViewState state = NewState();

        state.Apply(Query(enabled: EnabledFilter.Disabled));

        Assert.Equal(["B.addin"], Visible(state));
    }

    [Fact]
    public void Apply_SortDescending_ReversesNameOrder()
    {
        FileListViewState state = NewState();

        state.Apply(Query(sortDescending: true));

        Assert.Equal(["M.addin", "B.addin", "A.addin"], Visible(state));
    }

    [Fact]
    public void Apply_GroupByScope_AddsOneScopeGroupDescription()
    {
        FileListViewState state = NewState();

        state.Apply(Query(groupByScope: true));

        var description = Assert.Single(state.View.GroupDescriptions.Cast<PropertyGroupDescription>());
        Assert.Equal(nameof(AddinFileRowViewModel.Scope), description.PropertyName);
    }

    [Fact]
    public void Apply_SwitchGrouping_ReplacesPreviousGroupDescription()
    {
        FileListViewState state = NewState();
        state.Apply(Query(groupByScope: true));

        state.Apply(Query(groupByVersion: true));

        var description = Assert.Single(state.View.GroupDescriptions.Cast<PropertyGroupDescription>());
        Assert.Equal(nameof(AddinFileRowViewModel.Version), description.PropertyName);
    }

    [Fact]
    public void Apply_WithoutGrouping_ClearsPreviousGroups()
    {
        FileListViewState state = NewState();
        state.Apply(Query(groupByVersion: true));

        state.Apply(Query());

        Assert.Empty(state.View.GroupDescriptions);
    }

    [Fact]
    public void Matches_UsesLatestAppliedQuery()
    {
        FileListViewState state = NewState();
        state.Apply(Query(searchText: "B."));
        AddinFileRowViewModel bRow = state.View.Cast<AddinFileRowViewModel>().Single();

        state.Apply(Query(searchText: "M."));

        Assert.False(state.Matches(bRow));
        Assert.Equal(["M.addin"], Visible(state));
    }

    private static FileListViewState NewState()
    {
        var store = new FakeAddinStore();
        var files = new ObservableCollection<AddinFileRowViewModel>
        {
            NewRow(store, "A.addin", AddinScope.User, "2025", enabled: true),
            NewRow(store, "B.addin", AddinScope.User, "2025", enabled: false),
            NewRow(store, "M.addin", AddinScope.Machine, "2026", enabled: true),
        };
        return new FileListViewState(files);
    }

    private static string[] Visible(FileListViewState state) =>
        [.. state.View.Cast<AddinFileRowViewModel>().Select(row => row.FileName)];

    private static FileListQuery Query(
        ScopeFilter scope = ScopeFilter.All,
        string? version = null,
        EnabledFilter enabled = EnabledFilter.All,
        string? searchText = null,
        ListSortOrder sortOrder = ListSortOrder.Name,
        bool sortDescending = false,
        bool groupByScope = false,
        bool groupByVersion = false) =>
        new(scope, version, enabled, searchText, sortOrder, sortDescending, groupByScope, groupByVersion);

    private static AddinFileRowViewModel NewRow(
        FakeAddinStore store, string fileName, AddinScope scope, string version, bool enabled)
    {
        var file = new AddinFile(
            fileName, scope, version, enabled,
            FullPath: $@"C:\fake\{version}\{fileName}",
            VersionRootDirectory: $@"C:\fake\{version}",
            Manifest: new AddinManifest([], null, null, null, null, [], [], []));
        return new AddinFileRowViewModel(
            store, file, NullLogger<AddinFileRowViewModel>.Instance,
            TestLocalization.For<AddinFileRowViewModel>());
    }
}
