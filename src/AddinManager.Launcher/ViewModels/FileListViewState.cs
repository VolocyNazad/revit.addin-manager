using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using AddinManager.Core.Storage;

namespace AddinManager.Launcher.ViewModels;

/// <summary>
/// Состояние представления списка файлов: единственный владелец <see cref="ListCollectionView"/>
/// над строками — фильтр по scope/версии/состоянию/поиску, сортировка и группировка. Зона списка
/// передаёт снимок осей (<see cref="FileListQuery"/>) и не знает, как устроен <c>ICollectionView</c>.
/// </summary>
public sealed class FileListViewState
{
    private readonly ListCollectionView _view;
    private FileListQuery _query;
    private (bool ByScope, bool ByVersion) _grouping;

    /// <summary>Создает состояние над коллекцией строк.</summary>
    /// <param name="files">Строки списка — источник представления.</param>
    public FileListViewState(ObservableCollection<AddinFileRowViewModel> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        _view = (ListCollectionView)CollectionViewSource.GetDefaultView(files);
        _query = new FileListQuery(
            ScopeFilter.All, Version: null, EnabledFilter.All, SearchText: null,
            ListSortOrder.Name, SortDescending: false, GroupByScope: false, GroupByVersion: false);
        _view.Filter = row => row is AddinFileRowViewModel file && Matches(file);
        _view.CustomSort = Comparer<object>.Create(Compare);
    }

    /// <summary>Представление, которое показывает вид списка.</summary>
    public ICollectionView View => _view;

    /// <summary>
    /// Применяет новые оси: пересчитывает фильтр и сортировку, обновляет представление и
    /// переставляет группы, только если оси группировки действительно изменились.
    /// </summary>
    /// <param name="query">Снимок текущих осей списка.</param>
    public void Apply(FileListQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        _query = query;
        _view.Refresh();
        ApplyGrouping();
    }

    /// <summary>Проходит ли строка через текущий фильтр (поиск, scope, версия, состояние).</summary>
    /// <param name="row">Строка списка.</param>
    public bool Matches(AddinFileRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (_query.Scope != ScopeFilter.All)
        {
            var scope = _query.Scope == ScopeFilter.User ? AddinScope.User : AddinScope.Machine;
            if (row.Scope != scope)
                return false;
        }

        if (_query.Version is not null && row.Version != _query.Version)
            return false;

        var enabled = _query.Enabled switch
        {
            EnabledFilter.Enabled => true,
            EnabledFilter.Disabled => false,
            _ => (bool?)null,
        };
        if (enabled is { } requiredEnabled && row.IsEnabled != requiredEnabled)
            return false;

        return string.IsNullOrWhiteSpace(_query.SearchText)
            || row.FileName.Contains(_query.SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private int Compare(object? x, object? y)
    {
        var left = (AddinFileRowViewModel)x!;
        var right = (AddinFileRowViewModel)y!;
        var result = CompareAscending(left, right);
        return _query.SortDescending ? -result : result;
    }

    private int CompareAscending(AddinFileRowViewModel left, AddinFileRowViewModel right)
    {
        switch (_query.SortOrder)
        {
            case ListSortOrder.Scope:
                var byScope = left.Scope.CompareTo(right.Scope);
                if (byScope != 0)
                    return byScope;
                break;

            case ListSortOrder.Version:
                var byVersion = string.Compare(left.Version, right.Version, StringComparison.Ordinal);
                if (byVersion != 0)
                    return byVersion;
                break;
        }

        return string.Compare(left.FileName, right.FileName, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyGrouping()
    {
        var grouping = (_query.GroupByScope, _query.GroupByVersion);
        if (grouping == _grouping)
            return;

        _grouping = grouping;
        _view.GroupDescriptions.Clear();

        if (_query.GroupByScope)
            _view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AddinFileRowViewModel.Scope)));

        if (_query.GroupByVersion)
            _view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AddinFileRowViewModel.Version)));
    }
}
