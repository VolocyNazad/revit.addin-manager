namespace AddinManager.Launcher.ViewModels;

/// <summary>
/// Снимок осей фильтра, сортировки и группировки списка файлов: по нему
/// <see cref="FileListViewState"/> настраивает <c>ICollectionView</c>. Собирается зоной списка из
/// её публичных свойств, поэтому состояние представления не зависит от них напрямую.
/// </summary>
/// <param name="Scope">Фильтр по области (User/Machine/все).</param>
/// <param name="Version">Фильтр по версии Revit; <see langword="null"/> — все версии.</param>
/// <param name="Enabled">Фильтр по состоянию enabled/disabled.</param>
/// <param name="SearchText">Поиск по имени файла; пусто — без поиска.</param>
/// <param name="SortOrder">Основная ось сортировки.</param>
/// <param name="SortDescending">Сортировать по убыванию.</param>
/// <param name="GroupByScope">Группировать по области.</param>
/// <param name="GroupByVersion">Группировать по версии.</param>
public sealed record FileListQuery(
    ScopeFilter Scope,
    string? Version,
    EnabledFilter Enabled,
    string? SearchText,
    ListSortOrder SortOrder,
    bool SortDescending,
    bool GroupByScope,
    bool GroupByVersion);
