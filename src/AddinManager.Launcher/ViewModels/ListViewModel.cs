using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using AddinManager.Core.Abstractions.Guard;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Guard;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Abstractions.Composition;
using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.Composition;
using AddinManager.Launcher.Services;
using AddinManager.Localization;
using AddinManager.Localization.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace AddinManager.Launcher.ViewModels;

/// <summary>Зона списка: файлы всех версий Revit — поиск, фильтр и группировка по scope/версии (план, раздел 6).</summary>
public sealed partial class ListViewModel : ObservableObject
{
    /// <summary>Все версии, которые сканируем (план, раздел 2 — "all seven").</summary>
    public static readonly IReadOnlyList<string> RevitVersions = ["2021", "2022", "2023", "2024", "2025", "2026", "2027"];

    private readonly IAddinStore _store;
    private readonly IAddinChangeWatcher _changeWatcher;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILocalizationService _localizationService;
    private readonly IStringLocalizer<ListViewModel> _localizer;
    private readonly IAddinFileRowViewModelFactory _rowFactory;
    private readonly IToastService _toastService;
    private readonly IRevitProcessGuard _guard;
    private readonly IDialogService _dialogService;
    private readonly IFolderOpener _folderOpener;
    private readonly ListCollectionView _filesView;
    private readonly ILogger<ListViewModel> _logger;

    [ObservableProperty]
    private int _fileCount;

    /// <summary>
    /// Выбранная кликом строка (план, раздел 6) — подпанель разметки (<see cref="MarkupViewModel"/>)
    /// читает её напрямую конструкторной зависимостью, см. docs/architecture.md, раздел "Zone
    /// dependencies". <see langword="null"/>, если ничего не выбрано.
    /// </summary>
    [ObservableProperty]
    private AddinFileRowViewModel? _selectedFile;

    /// <summary>Revit запущен — удаление запрещено.</summary>
    [ObservableProperty]
    private bool _isLocked;

    /// <summary>Сколько строк отмечено чекбоксом пакетного выбора.</summary>
    [ObservableProperty]
    private int _selectedCount;

    /// <summary>Есть ли хоть одна отмеченная строка — видимость bulk-панели.</summary>
    [ObservableProperty]
    private bool _hasSelection;

    /// <summary>Ошибка последнего создания файла — баннером над списком.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private ScopeFilter _scopeFilter = ScopeFilter.All;

    /// <summary><see langword="null"/> — все версии; иначе одна из <see cref="RevitVersions"/>.</summary>
    [ObservableProperty]
    private string? _versionFilter;

    [ObservableProperty]
    private ListSortOrder _sortOrder = ListSortOrder.Name;

    /// <summary>Группировать по scope; взаимоисключающе с <see cref="GroupByVersion"/> — включение одного гасит другой.</summary>
    [ObservableProperty]
    private bool _groupByScope;

    /// <summary>Группировать по версии; взаимоисключающе с <see cref="GroupByScope"/> — включение одного гасит другой.</summary>
    [ObservableProperty]
    private bool _groupByVersion;

    /// <summary>
    /// Создает модель, настраивает представление над <see cref="Files"/>, сразу читает диск и
    /// запускает слежение за внешними изменениями (<paramref name="changeWatcher"/>) — правки,
    /// сделанные не через это приложение (другой процесс, ручное редактирование в проводнике,
    /// вторая копия менеджера), тоже должны появляться в списке без ручного "Обновить".
    /// </summary>
    /// <param name="store">Чтение файлов с диска и переключение enabled/disabled.</param>
    /// <param name="changeWatcher">
    /// Уведомляет, что .addin-файлы могли измениться извне. Событие может прийти из любого
    /// потока — вот зачем <paramref name="dispatcher"/>, отдельно его не передают.
    /// </param>
    /// <param name="dispatcher">
    /// Маршалинг в поток UI: <see cref="Refresh"/> трогает <see cref="Files"/>
    /// (<see cref="ObservableCollection{T}"/>), а <paramref name="changeWatcher"/> может
    /// сработать не из потока UI.
    /// </param>
    /// <param name="logger">Логгер зоны списка.</param>
    /// <param name="localizationService">Сервис языка — смена языка перечитывает строки через <see cref="Refresh"/>.</param>
    /// <param name="localizer">Строки тулбара списка.</param>
    /// <param name="rowFactory">Создание строк списка (файлы известны только при сканировании).</param>
    /// <param name="toastService">Подтверждение ручного обновления тостом.</param>
    /// <param name="guard">Сторож запущенного Revit — тогглы строк гаснут, пока он жив.</param>
    /// <param name="dialogService">Подтверждение удаления.</param>
    /// <param name="folderOpener">Показ файла в проводнике (контекстное меню строки).</param>
    public ListViewModel(
        IAddinStore store,
        IAddinChangeWatcher changeWatcher,
        IUiDispatcher dispatcher,
        ILogger<ListViewModel> logger,
        ILocalizationService localizationService,
        IStringLocalizer<ListViewModel> localizer,
        IAddinFileRowViewModelFactory rowFactory,
        IToastService toastService,
        IRevitProcessGuard guard,
        IDialogService dialogService,
        IFolderOpener folderOpener)
    {
        _store = store;
        _changeWatcher = changeWatcher;
        _dispatcher = dispatcher;
        _logger = logger;
        _localizationService = localizationService;
        _localizer = localizer;
        _rowFactory = rowFactory;
        _toastService = toastService;
        _guard = guard;
        _dialogService = dialogService;
        _folderOpener = folderOpener;

        _filesView = (ListCollectionView)CollectionViewSource.GetDefaultView(Files);
        _filesView.Filter = MatchesFilter;
        _filesView.CustomSort = Comparer<object>.Create(CompareRows);

        Refresh();

        // Дребезг сырых событий уже погашен внутри changeWatcher — здесь только маршалинг.
        _changeWatcher.Changed += (_, _) => _dispatcher.Invoke(Refresh);
        _changeWatcher.Start();

        // Смена языка тоже идёт через Refresh: строки пересоздаются с новыми подписями.
        _localizationService.LanguageChanged += (_, _) => _dispatcher.Invoke(Refresh);

        // Событие сторожа может прийти не из потока UI — маршалим, как watcher выше.
        // Сам guard здесь не стартуем (владелец — MainViewModel), только читаем состояние.
        _guard.Changed += (_, _) => _dispatcher.Invoke(SyncEditLock);
        SyncEditLock();
    }

    /// <summary>Сырые строки списка — всё, что прочитано с диска, без фильтра/сортировки/группировки.</summary>
    public ObservableCollection<AddinFileRowViewModel> Files { get; } = [];

    /// <summary>То, что реально показывает вид: <see cref="Files"/> с применённым фильтром, сортировкой и группировкой.</summary>
    public ICollectionView FilesView => _filesView;

    /// <summary>Перечитывает файлы всех версий с диска (план, раздел 4 — событийный rescan).</summary>
    [RelayCommand]
    public void Refresh()
    {
        _logger.LogDebug("Refresh: пересканирование всех версий начато");
        ErrorMessage = null;

        // Refresh пересоздаёт все AddinFileRowViewModel — старый SelectedFile больше не входит в
        // Files, восстанавливаем выбор по идентичности (FileName, Scope, Version), а не по ссылке.
        // То же для пакетного выбора: набор отмеченных ключей переживает пересканирование.
        var previousSelection = SelectedFile is { } selected
            ? (selected.FileName, selected.Scope, selected.Version)
            : ((string FileName, AddinScope Scope, string Version)?)null;
        var previousBulkSelection = new HashSet<(string FileName, AddinScope Scope, string Version)>(
            Files.Where(f => f.IsSelected).Select(f => (f.FileName, f.Scope, f.Version)));

        foreach (var row in Files)
            row.PropertyChanged -= OnRowPropertyChanged;
        Files.Clear();
        foreach (var version in RevitVersions)
        {
            var filesForVersion = _store.ScanVersion(version);
            if (filesForVersion.Count > 0)
                _logger.LogDebug("Refresh({Version}): {Count} файлов", version, filesForVersion.Count);

            foreach (var file in filesForVersion)
            {
                var row = _rowFactory.Create(file);
                row.DeleteRequested += OnRowDeleteRequested;
                row.PropertyChanged += OnRowPropertyChanged;
                row.IsSelected = previousBulkSelection.Contains((row.FileName, row.Scope, row.Version));
                Files.Add(row);
            }
        }

        FileCount = Files.Count;
        UpdateRowWarnings();
        _filesView.Refresh();

        // Восстановленного по идентичности файла нет — ни прежнего выбора не было (запуск
        // приложения), ни прежний файл не пережил пересканирование — выбираем первый в
        // отображаемом (отфильтрованном/отсортированном) порядке, а не оставляем список без
        // выделения.
        var restored = previousSelection is { } key
            ? Files.FirstOrDefault(f => f.FileName == key.FileName && f.Scope == key.Scope && f.Version == key.Version)
            : null;
        SelectedFile = restored ?? _filesView.Cast<AddinFileRowViewModel>().FirstOrDefault();
        RefreshSelectionSnapshot();

        if (previousSelection is { } previous)
        {
            if (restored is not null)
                _logger.LogDebug("Refresh: выбор {FileName} пережил пересканирование", restored.FileName);
            else
                _logger.LogInformation(
                    "Refresh: прежний выбор {FileName} ({Scope}, {Version}) не пережил пересканирование, выбран {NewFileName}",
                    previous.FileName, previous.Scope, previous.Version, SelectedFile?.FileName ?? "-");
        }

        _logger.LogInformation("Refresh: найдено {Count} файлов по всем версиям", FileCount);
    }
    /// <summary>Устанавливает фильтр по scope (кнопки-чипсы в тулбаре списка).</summary>
    [RelayCommand]
    public void SetScopeFilter(ScopeFilter filter) => ScopeFilter = filter;

    /// <summary>Устанавливает фильтр по версии; <see langword="null"/> — все версии.</summary>
    [RelayCommand]
    public void SetVersionFilter(string? version) => VersionFilter = version;

    /// <summary>Устанавливает порядок сортировки (кнопки-чипсы в тулбаре списка).</summary>
    [RelayCommand]
    public void SetSortOrder(ListSortOrder order) => SortOrder = order;

    /// <summary>Снимает отметку со всех строк.</summary>
    [RelayCommand]
    public void ToggleSelectAll()
    {
        // Переключатель "Выбрать все/Снять выбор": все видимые отмечены (или видимых нет,
        // а выбор есть за фильтром) — сбрасываем всё; иначе отмечаем все видимые.
        var visible = VisibleRows().ToList();
        if (visible.Count != 0 && !visible.All(f => f.IsSelected))
            SelectAllVisible();
        else
            ClearSelection();
    }

    private void ClearSelection()
    {
        foreach (var row in Files)
            row.IsSelected = false;
    }

    private void SelectAllVisible()
    {
        foreach (var row in VisibleRows())
            row.IsSelected = true;
    }

    /// <summary>Строки, видимые после фильтра/сортировки — ось пакетного выбора.</summary>
    private IEnumerable<AddinFileRowViewModel> VisibleRows() =>
        _filesView.Cast<AddinFileRowViewModel>();

    private bool CanBulkEdit() => HasSelection && !IsLocked;

    /// <summary>
    /// Переключает все отмеченные файлы: есть хоть один выключенный — включает все,
    /// иначе выключает все. Ошибки отдельных строк остаются под строками (см. строку).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanBulkEdit))]
    public void ToggleSelected()
    {
        var targets = Files.Where(f => f.IsSelected).ToList();
        var enable = targets.Any(f => !f.IsEnabled);
        _logger.LogInformation("BulkToggle: {Count} файлов -> IsEnabled={Value}", targets.Count, enable);
        foreach (var row in targets)
            row.IsEnabled = enable;
        RefreshSelectionSnapshot();
    }

    /// <summary>
    /// Удаляет все отмеченные файлы с диска после одного подтверждения. Ошибки отдельных
    /// файлов — в баннер над списком, удалённые уже не восстановить через <c>Refresh</c>.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanBulkEdit))]
    public void DeleteSelected()
    {
        var targets = Files.Where(f => f.IsSelected).ToList();
        if (targets.Count == 0)
            return;

        if (!_dialogService.Confirm(_localizer["DeleteMultipleConfirm", targets.Count]))
            return;

        _logger.LogInformation("BulkDelete: подтверждено удаление {Count} файлов", targets.Count);
        string? firstError = null;
        foreach (var row in targets)
        {
            try
            {
                _store.Delete(row.File);
            }
            catch (Exception ex) when (ex is IOException or RevitRunningException)
            {
                _logger.LogWarning(
                    ex,
                    "BulkDelete {FileName} ({Scope}, {Version}): не удалён",
                    row.FileName, row.Scope, row.Version);
                firstError ??= ex.Message;
            }
        }

        ErrorMessage = firstError;
        Refresh();
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // IsSelected двигает счётчик и доступность команд; IsEnabled — подпись
        // переключателя "Включить/Выключить" (зависит от состояния отмеченных).
        if (e.PropertyName is nameof(AddinFileRowViewModel.IsSelected) or nameof(AddinFileRowViewModel.IsEnabled))
            RefreshSelectionSnapshot();
    }

    /// <summary>
    /// Пересчитывает <see cref="SelectedCount"/>/<see cref="HasSelection"/> и доступность
    /// bulk-команд. Вызывается при смене отметок, тоглов, фильтров и после каждого <see cref="Refresh"/>.
    /// </summary>
    private void RefreshSelectionSnapshot()
    {
        SelectedCount = Files.Count(f => f.IsSelected);
        HasSelection = SelectedCount > 0;

        ToggleSelectedCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(ToggleSelectedLabel));
        OnPropertyChanged(nameof(SelectAllToggleLabel));
    }

    /// <summary>Разносит <see cref="IRevitProcessGuard.IsRunning"/> по строкам (тогглы гаснут).</summary>
    private void SyncEditLock()
    {
        IsLocked = _guard.IsRunning;
        foreach (var row in Files)
            row.IsLocked = IsLocked;
    }

    /// <summary>
    /// Пересчитывает предупреждения строк: дубли <c>AddInId</c> считаются только внутри одной
    /// версии Revit (идентификатор в двух файлах разных версий — не конфликт: Revit грузит
    /// версии независимо). Дубли внутри файла строка видит сама; сюда передаётся лишь множество
    /// идентификаторов, встречающихся минимум в двух файлах версии.
    /// </summary>
    private void UpdateRowWarnings()
    {
        var duplicatedByVersion = FindCrossFileDuplicates(Files);
        foreach (var row in Files)
        {
            duplicatedByVersion.TryGetValue(row.Version, out var duplicated);
            row.UpdateWarnings(duplicated ?? (IReadOnlySet<Guid>)new HashSet<Guid>());
        }
    }

    /// <summary>
    /// Идентификаторы, встречающиеся минимум в двух разных файлах одной версии.
    /// Дубль внутри одного файла без второго файла сюда не попадает (это не кросс-файловый).
    /// </summary>
    internal static Dictionary<string, HashSet<Guid>> FindCrossFileDuplicates(
        IEnumerable<AddinFileRowViewModel> rows)
    {
        var filesById = new Dictionary<(string Version, Guid Id), HashSet<string>>( );
        foreach (var row in rows)
        {
            var fileKey = $"{row.Scope}/{row.FileName}";
            foreach (var id in row.File.Manifest.Entries.Select(e => e.AddInId).Distinct())
            {
                var key = (row.Version, id);
                if (!filesById.TryGetValue(key, out var files))
                    filesById[key] = files = [];
                files.Add(fileKey);
            }
        }

        var result = new Dictionary<string, HashSet<Guid>>(StringComparer.Ordinal);
        foreach (var ((version, id), files) in filesById)
        {
            if (files.Count < 2)
                continue;
            if (!result.TryGetValue(version, out var set))
                result[version] = set = [];
            set.Add(id);
        }

        return result;
    }

    private void OnRowDeleteRequested(object? sender, EventArgs e)
    {
        if (sender is AddinFileRowViewModel row)
            DeleteRow(row);
    }

    /// <summary>
    /// Удаляет файл строки с диска навсегда (после подтверждения). Ошибка (нет прав,
    /// файл занят, живой Revit) — в сообщение под строкой, выбор не теряется.
    /// </summary>
    private void DeleteRow(AddinFileRowViewModel row)
    {
        if (!_dialogService.Confirm(_localizer["DeleteConfirm", row.FileName]))
            return;

        try
        {
            _store.Delete(row.File);
        }
        catch (Exception ex) when (ex is IOException or RevitRunningException)
        {
            row.ErrorMessage = ex.Message;
            return;
        }

        Refresh();
    }

    /// <summary>Тултип кнопки добавления файла.</summary>
    public string AddFileTooltip => _localizer["AddFileTooltip"];

    private bool CanAddFile() => !IsLocked;

    /// <summary>
    /// Создаёт файл по параметрам из диалога. Ошибка (уже есть, нет прав, живой Revit) —
    /// в баннер над списком.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddFile))]
    public void AddFile()
    {
        if (_dialogService.PromptNewFile() is not { } options)
            return;

        try
        {
            _store.CreateFile(options.FileName, options.Version, options.Scope, options.Disabled);
            ErrorMessage = null;
        }
        catch (Exception ex) when (ex is IOException or RevitRunningException)
        {
            ErrorMessage = ex.Message;
            return;
        }

        Refresh();
    }

    partial void OnIsLockedChanged(bool value)
    {
        AddFileCommand.NotifyCanExecuteChanged();
        ToggleSelectedCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Ручное обновление по кнопке: тот же <see cref="Refresh"/>, плюс тост. Остальные вызовы
    /// <see cref="Refresh"/> молчат, иначе автосканы спамили бы тостами.
    /// </summary>
    [RelayCommand]
    public void RefreshManual()
    {
        Refresh();
        _toastService.Show(_localizer["ListRefreshed"]);
    }

    private bool CanShowInFolder() => SelectedFile is not null;

    /// <summary>
    /// Показывает выбранный манифест в проводнике (папка открывается, файл подсвечен).
    /// Только чтение — сторож Revit не при чём, работает и при живом Revit.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanShowInFolder))]
    public void ShowInFolder()
    {
        if (SelectedFile is not { } file)
            return;

        _logger.LogInformation(
            "ShowInFolder: {FileName} ({Scope}, {Version})",
            file.FileName, file.Scope, file.Version);
        _folderOpener.Reveal(file.FullPath);
    }

    /// <summary>Пункт контекстного меню строки "Показать в папке".</summary>
    public string ShowInFolderLabel => _localizer["ShowInFolderLabel"];

    /// <summary>Плейсхолдер поиска. "User"/"Machine"/"Scope"/годы/типы записей — технические токены данных, не переводятся.</summary>
    public string SearchPlaceholder => _localizer["ListSearchPlaceholder"];

    /// <summary>Тултип кнопки обновления.</summary>
    public string RefreshTooltip => _localizer["RefreshButtonTooltip"];

    /// <summary>Чипс scope "все".</summary>
    public string ScopeAllLabel => _localizer["ScopeFilter_AllOption"];

    /// <summary>Чипс версий "все".</summary>
    public string VersionAllLabel => _localizer["VersionFilter_AllVersionsOption"];

    /// <summary>Подпись "Сортировка:".</summary>
    public string SortLabel => _localizer["SortLabel"];

    /// <summary>Сортировка по имени.</summary>
    public string SortNameLabel => _localizer["SortByNameOption"];

    /// <summary>Сортировка по версии.</summary>
    public string SortVersionLabel => _localizer["SortByVersionOption"];

    /// <summary>Подпись "Группировать:".</summary>
    public string GroupLabel => _localizer["GroupByLabel"];

    /// <summary>Группировка по версии.</summary>
    public string GroupVersionLabel => _localizer["GroupByVersionOption"];

    /// <summary>"Выбрано: N" в bulk-панели.</summary>
    public string SelectionSummary => _localizer["SelectionSummary", SelectedCount];

    /// <summary>
    /// Подпись переключателя "Включить/Выключить": есть хоть один выключенный среди
    /// отмеченных — "Включить", иначе "Выключить".
    /// </summary>
    public string ToggleSelectedLabel => Files.Any(f => f.IsSelected && !f.IsEnabled)
        ? _localizer["EnableSelectedLabel"]
        : _localizer["DisableSelectedLabel"];

    /// <summary>
    /// Подпись переключателя "Выбрать все/Снять выбор": все видимые отмечены (или выбор
    /// спрятан за фильтром) — "Снять выбор", иначе "Выбрать все".
    /// </summary>
    public string SelectAllToggleLabel
    {
        get
        {
            var visible = VisibleRows().ToList();
            var showClear = visible.Count == 0 ? HasSelection : visible.All(f => f.IsSelected);
            return showClear ? _localizer["ClearSelectionLabel"] : _localizer["SelectAllLabel"];
        }
    }

    /// <summary>Кнопка bulk-панели "Удалить".</summary>
    public string DeleteSelectedLabel => _localizer["DeleteSelectedLabel"];

    // Filter/CustomSort — делегаты ICollectionView, читают текущие значения полей при каждом Refresh().
    // Три оси фильтра намеренно делят один шаг (refresh + пересчёт шапки выбора): Sonar S4144 здесь ложный.
#pragma warning disable S4144 // Одинаковые тела — общий шаг трёх независимых осей фильтра.
    partial void OnSearchTextChanged(string? value)
    {
        _filesView.Refresh();
        RefreshSelectionSnapshot();
    }

    partial void OnScopeFilterChanged(ScopeFilter value)
    {
        _filesView.Refresh();
        RefreshSelectionSnapshot();
    }

    partial void OnVersionFilterChanged(string? value)
    {
        _filesView.Refresh();
        RefreshSelectionSnapshot();
    }
#pragma warning restore S4144

    partial void OnSortOrderChanged(ListSortOrder value) => _filesView.Refresh();

    partial void OnGroupByScopeChanged(bool value)
    {
        // Взаимоисключающе: включение одного гасит другой, что само вызовет ApplyGrouping ниже по цепочке.
        if (value && GroupByVersion)
            GroupByVersion = false;
        else
            ApplyGrouping();
    }

    partial void OnGroupByVersionChanged(bool value)
    {
        if (value && GroupByScope)
            GroupByScope = false;
        else
            ApplyGrouping();
    }

    /// <summary>Предикат <see cref="ICollectionView.Filter"/>: scope, версия, текст поиска по имени файла.</summary>
    private bool MatchesFilter(object obj)
    {
        if (obj is not AddinFileRowViewModel row)
            return false;

        if (ScopeFilter != ScopeFilter.All)
        {
            var scope = ScopeFilter == ScopeFilter.User ? AddinScope.User : AddinScope.Machine;
            if (row.Scope != scope)
                return false;
        }

        if (VersionFilter is not null && row.Version != VersionFilter)
            return false;

        if (!string.IsNullOrWhiteSpace(SearchText) && !row.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    /// <summary>
    /// Компаратор <see cref="ListCollectionView.CustomSort"/> — по умолчанию имя; при
    /// <see cref="ListSortOrder.Scope"/>/<see cref="ListSortOrder.Version"/> сначала своя ось, имя — как добавочный ключ.
    /// </summary>
    private int CompareRows(object? x, object? y)
    {
        var left = (AddinFileRowViewModel)x!;
        var right = (AddinFileRowViewModel)y!;

        switch (SortOrder)
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

    /// <summary>
    /// Переставляет <see cref="ICollectionView.GroupDescriptions"/> под <see cref="GroupByScope"/>/
    /// <see cref="GroupByVersion"/> — оба выключены значит без группировки, включённым может быть
    /// только один (см. их OnChanged-обработчики).
    /// </summary>
    private void ApplyGrouping()
    {
        _filesView.GroupDescriptions.Clear();

        if (GroupByScope)
            _filesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AddinFileRowViewModel.Scope)));

        if (GroupByVersion)
            _filesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AddinFileRowViewModel.Version)));
    }

    partial void OnFileCountChanged(int value) => RefreshSnapshot();

    partial void OnSelectedFileChanged(AddinFileRowViewModel? value)
    {
        ShowInFolderCommand.NotifyCanExecuteChanged();
        RefreshSnapshot();
    }

    private void RefreshSnapshot() => OnPropertyChanged((string?)null);

    /// <inheritdoc />
    public override string ToString() => $"List(Files={FileCount}, Selected={SelectedFile?.FileName ?? "-"}, Bulk={SelectedCount})";
}
