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
public sealed partial class ListViewModel : ObservableObject, IFileSelection, IAddinFileCatalog
{
    /// <summary>Все версии, которые сканируем (план, раздел 2 — "all seven").</summary>
    public static readonly IReadOnlyList<string> RevitVersions = ["2021", "2022", "2023", "2024", "2025", "2026", "2027"];

    private readonly IAddinStore _store;
    private readonly IAddinChangeWatcher _changeWatcher;
    private readonly IUiDispatcher _dispatcher;
    private IUndoRecord? _lastUndo;
    private readonly ILocalizationService _localizationService;
    private readonly IStringLocalizer<ListViewModel> _localizer;
    private readonly IAddinFileRowViewModelFactory _rowFactory;
    private readonly IToastService _toastService;
    private readonly IClipboardService _clipboard;
    private readonly IRevitProcessGuard _guard;
    private readonly IDialogService _dialogService;
    private readonly IFolderOpener _folderOpener;
    private readonly IEditorDirtyState? _dirtyState;
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

    /// <summary>Сортировать по убыванию; переключается повторным кликом по активному чипсу.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortDirectionGlyph))]
    private bool _sortDescending;

    /// <summary>Фильтр по состоянию enabled/disabled.</summary>
    [ObservableProperty]
    private EnabledFilter _enabledFilter = EnabledFilter.All;

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
    /// <param name="clipboard">Буфер обмена для пунктов «Копировать путь/имя файла».</param>
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
        IFolderOpener folderOpener,
        IClipboardService clipboard,
        IEditorDirtyState? dirtyState = null)
    {
        _store = store;
        _changeWatcher = changeWatcher;
        _dispatcher = dispatcher;
        _logger = logger;
        _localizationService = localizationService;
        _localizer = localizer;
        _rowFactory = rowFactory;
        _toastService = toastService;
        _clipboard = clipboard;
        _guard = guard;
        _dialogService = dialogService;
        _folderOpener = folderOpener;
        _dirtyState = dirtyState;

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
                row.Renamed += OnRowRenamed;
                row.Toggled += OnRowToggled;
                row.PropertyChanged += OnRowPropertyChanged;
                row.IsSelected = previousBulkSelection.Contains((row.FileName, row.Scope, row.Version));
                Files.Add(row);
            }
        }

        FileCount = Files.Count;
        UpdateRowWarnings();
        RefreshChipLabels();
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

    /// <summary>Устанавливает фильтр по состоянию enabled/disabled (кнопки-чипсы в тулбаре списка).</summary>
    [RelayCommand]
    public void SetEnabledFilter(EnabledFilter filter) => EnabledFilter = filter;

    /// <summary>Устанавливает фильтр по версии; <see langword="null"/> — все версии.</summary>
    [RelayCommand]
    public void SetVersionFilter(string? version) => VersionFilter = version;

    /// <summary>
    /// Устанавливает порядок сортировки (кнопки-чипсы в тулбаре списка). Повторный клик по уже
    /// активному чипсу переключает направление, как в Проводнике; новый чипс начинает с возрастания.
    /// </summary>
    [RelayCommand]
    public void SetSortOrder(ListSortOrder order)
    {
        if (SortOrder == order)
        {
            SortDescending = !SortDescending;
            return;
        }

        SortOrder = order;
        SortDescending = false;
    }

    /// <summary>Переключает направление сортировки отдельной кнопкой-стрелкой.</summary>
    [RelayCommand]
    public void ToggleSortDirection() => SortDescending = !SortDescending;

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
        var toggles = new List<UndoToggle>();
        foreach (var row in targets)
        {
            var before = row.File;
            row.IsEnabled = enable;
            if (row.File.Enabled != before.Enabled)
                toggles.Add(new UndoToggle(row.File, before.Enabled));
        }

        if (toggles.Count > 0)
            SetUndo(new UndoToggleGroup(toggles));

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
        var backups = new List<DeletedFile>();
        foreach (var row in targets)
        {
            try
            {
                var content = _store.Delete(row.File);
                backups.Add(new DeletedFile(row.File, content));
            }
            catch (Exception ex) when (ex is IOException or RevitRunningException or UnauthorizedAccessException)
            {
                _logger.LogWarning(
                    ex,
                    "BulkDelete {FileName} ({Scope}, {Version}): не удалён",
                    row.FileName, row.Scope, row.Version);
                firstError ??= ex.Message;
            }
        }

        if (backups.Count > 0)
            SetUndo(new UndoDelete(backups));

        ErrorMessage = firstError;
        Refresh();
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // IsSelected двигает счётчик и доступность команд; IsEnabled — подпись
        // переключателя "Включить/Выключить" и фильтр состояния.
        if (e.PropertyName == nameof(AddinFileRowViewModel.IsEnabled))
        {
            _filesView.Refresh();
            RefreshChipLabels();
            RefreshSelectionSnapshot();
        }
        else if (e.PropertyName == nameof(AddinFileRowViewModel.IsSelected))
        {
            RefreshSelectionSnapshot();
        }
    }

    /// <summary>
    /// Пересчитывает подписи чипсов со счётчиками: база — все файлы, прошедшие только поиск по
    /// имени; каждый чип считает свою ось (scope/версия/состояние), не сужаясь чужими фильтрами.
    /// </summary>
    private void RefreshChipLabels()
    {
        OnPropertyChanged(nameof(ScopeAllChipLabel));
        OnPropertyChanged(nameof(ScopeUserChipLabel));
        OnPropertyChanged(nameof(ScopeMachineChipLabel));
        OnPropertyChanged(nameof(VersionAllChipLabel));
        OnPropertyChanged(nameof(Version2021ChipLabel));
        OnPropertyChanged(nameof(Version2022ChipLabel));
        OnPropertyChanged(nameof(Version2023ChipLabel));
        OnPropertyChanged(nameof(Version2024ChipLabel));
        OnPropertyChanged(nameof(Version2025ChipLabel));
        OnPropertyChanged(nameof(Version2026ChipLabel));
        OnPropertyChanged(nameof(Version2027ChipLabel));
        OnPropertyChanged(nameof(EnabledAllChipLabel));
        OnPropertyChanged(nameof(EnabledOnChipLabel));
        OnPropertyChanged(nameof(EnabledOffChipLabel));
    }

    private IEnumerable<AddinFileRowViewModel> SearchMatchedRows =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Files
            : Files.Where(row => row.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

    private int SearchMatchedCount => SearchMatchedRows.Count();

    private int CountScope(AddinScope scope) => SearchMatchedRows.Count(row => row.Scope == scope);

    private int CountVersion(string version) => SearchMatchedRows.Count(row => row.Version == version);

    private int CountEnabled(bool enabled) => SearchMatchedRows.Count(row => row.IsEnabled == enabled);

    private static string WithCount(string label, int count) => $"{label} · {count}";

    /// <summary>
    /// Запоминает успешное переключение enabled/disabled как последнее действие для <c>Ctrl+Z</c>.
    /// </summary>
    private void OnRowToggled(object? sender, FileToggledEventArgs e) =>
        SetUndo(new UndoToggle(e.After, e.Before.Enabled));

    /// <summary>Можно ли отменить последнее действие (есть запись и не мешает живой Revit).</summary>
    public bool HasUndo => _lastUndo is not null;

    private bool CanUndo() => _lastUndo is not null && !IsLocked;

    /// <summary>
    /// Отменяет последнее перемещение enabled/disabled или удаление файла: перенос возвращает
    /// прежнее состояние, удаление — записывает сохранённые байты обратно на прежний путь.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    public void UndoLastAction()
    {
        if (_lastUndo is not { } undo)
            return;

        SetUndo(null);
        try
        {
            switch (undo)
            {
                case UndoToggle toggle:
                    var current = Files.FirstOrDefault(file =>
                        file.FileName == toggle.After.FileName
                        && file.Scope == toggle.After.Scope
                        && file.Version == toggle.After.Version);
                    if (current is null)
                        throw new IOException(_localizer["UndoFileMissing"]);

                    _store.SetEnabled(current.File, toggle.PreviousEnabled);
                    Refresh();
                    SelectByIdentity(toggle.After.FileName, toggle.After.Scope, toggle.After.Version);
                    break;

                case UndoToggleGroup group:
                    foreach (var item in group.Toggles)
                    {
                        var currentFile = Files.FirstOrDefault(file =>
                            file.FileName == item.After.FileName
                            && file.Scope == item.After.Scope
                            && file.Version == item.After.Version);
                        if (currentFile is null)
                            throw new IOException(_localizer["UndoFileMissing"]);

                        _store.SetEnabled(currentFile.File, item.PreviousEnabled);
                    }

                    Refresh();
                    var firstToggle = group.Toggles[0].After;
                    SelectByIdentity(firstToggle.FileName, firstToggle.Scope, firstToggle.Version);
                    break;

                case UndoMove move:
                    var moved = Files.FirstOrDefault(file =>
                        file.FileName == move.Moved.FileName
                        && file.Scope == move.Moved.Scope
                        && file.Version == move.Moved.Version);
                    if (moved is null)
                        throw new IOException(_localizer["UndoFileMissing"]);

                    _store.Move(moved.File, move.Original.Version, move.Original.Scope, move.Original.Enabled, overwrite: false);
                    if (move.ReplacedPath is not null && move.ReplacedContent is not null)
                    {
                        if (File.Exists(move.ReplacedPath))
                            throw new IOException(_localizer["UndoFileExists", Path.GetFileName(move.ReplacedPath)]);
                        var directory = Path.GetDirectoryName(move.ReplacedPath);
                        if (!string.IsNullOrEmpty(directory))
                            Directory.CreateDirectory(directory);
                        File.WriteAllBytes(move.ReplacedPath, move.ReplacedContent);
                    }

                    Refresh();
                    SelectByIdentity(move.Original.FileName, move.Original.Scope, move.Original.Version);
                    break;

                case UndoDelete delete:
                    foreach (var item in delete.Files)
                    {
                        if (File.Exists(item.File.FullPath))
                            throw new IOException(_localizer["UndoFileExists", item.File.FileName]);

                        var directory = Path.GetDirectoryName(item.File.FullPath);
                        if (!string.IsNullOrEmpty(directory))
                            Directory.CreateDirectory(directory);
                        File.WriteAllBytes(item.File.FullPath, item.Content);
                    }

                    Refresh();
                    var first = delete.Files[0].File;
                    SelectByIdentity(first.FileName, first.Scope, first.Version);
                    break;
            }

            _toastService.Show(_localizer["UndoDoneMessage"]);
        }
        catch (Exception ex) when (ex is IOException or RevitRunningException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Undo: последнее действие не удалось отменить");
            ErrorMessage = ex.Message;
            _toastService.Show(_localizer["UndoFailedMessage"]);
        }
    }

    private void SetUndo(IUndoRecord? undo)
    {
        _lastUndo = undo;
        UndoLastActionCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasUndo));
    }

    private void SelectByIdentity(string fileName, AddinScope scope, string version)
    {
        var row = Files.FirstOrDefault(file =>
            file.FileName == fileName && file.Scope == scope && file.Version == version);
        if (row is null)
            return;

        SelectedFile = row;
        row.IsSelected = true;
        RefreshSelectionSnapshot();
    }

    /// <summary>
    /// Возвращается после успешного переименования строки: перечитывает список (файл на диске уже
    /// с новым именем) и восстанавливает выбор на переименованном файле, а не на первой строке.
    /// </summary>
    private void OnRowRenamed(object? sender, EventArgs e)
    {
        if (sender is not AddinFileRowViewModel row)
            return;

        var renamedKey = (row.FileName, row.Scope, row.Version);
        Refresh();

        var renamed = Files.FirstOrDefault(file =>
            file.FileName == renamedKey.FileName
            && file.Scope == renamedKey.Scope
            && file.Version == renamedKey.Version);
        if (renamed is null)
            return;

        SelectedFile = renamed;
        renamed.IsSelected = true;
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
        BeginRenameCommand.NotifyCanExecuteChanged();
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
    /// Удаляет файл строки (после подтверждения) в Корзину. Ошибка (нет прав,
    /// файл занят, живой Revit) — в сообщение под строкой, выбор не теряется. Байты файла
    /// сохраняются для <c>Ctrl+Z</c>.
    /// </summary>
    private void DeleteRow(AddinFileRowViewModel row)
    {
        if (!_dialogService.Confirm(_localizer["DeleteConfirm", row.FileName]))
            return;

        DeletedFile backup;
        try
        {
            var content = _store.Delete(row.File);
            backup = new DeletedFile(row.File, content);
        }
        catch (Exception ex) when (ex is IOException or RevitRunningException or UnauthorizedAccessException)
        {
            row.ErrorMessage = ex.Message;
            return;
        }

        SetUndo(new UndoDelete([backup]));
        Refresh();
    }

    /// <summary>Тултип кнопки добавления файла (хоткей: Ctrl+N).</summary>
    public string AddFileTooltip => $"{_localizer["AddFileTooltip"]} (Ctrl+N)";

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
        UndoLastActionCommand.NotifyCanExecuteChanged();
        MoveToCommand.NotifyCanExecuteChanged();
        CopyToCommand.NotifyCanExecuteChanged();
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

    private bool CanShowInFolder(AddinFileRowViewModel? row) => (row ?? SelectedFile) is not null;

    /// <summary>
    /// Показывает манифест в проводнике (папка открывается, файл подсвечен).
    /// Кнопка строки передаёт свою строку параметром — клик по кнопке выбор не двигает,
    /// поэтому брать всегда SelectedFile было бы неверно; без параметра — выбранный файл.
    /// Только чтение — сторож Revit не при чём, работает и при живом Revit.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanShowInFolder))]
    public void ShowInFolder(AddinFileRowViewModel? row)
    {
        if ((row ?? SelectedFile) is not { } file)
            return;

        _logger.LogInformation(
            "ShowInFolder: {FileName} ({Scope}, {Version})",
            file.FileName, file.Scope, file.Version);
        _folderOpener.Reveal(file.FullPath);
    }

    /// <summary>Пункт контекстного меню строки "Показать в папке" (хоткей: Ctrl+O).</summary>
    public string ShowInFolderLabel => $"{_localizer["ShowInFolderLabel"]} (Ctrl+O)";

    /// <summary>Пункт контекстного меню строки "Переименовать" (хоткей: F2).</summary>
    public string RenameMenuLabel => $"{_localizer["RenameMenuLabel"]} (F2)";

    private bool CanBeginRename(AddinFileRowViewModel? row) => !IsLocked && (row ?? SelectedFile) is not null;

    /// <summary>
    /// Начинает встроенное переименование строки. Параметр — строка под курсором из контекстного
    /// меню; без параметра (F2) — выбранная строка.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanBeginRename))]
    public void BeginRename(AddinFileRowViewModel? row) => (row ?? SelectedFile)?.BeginRename();

    /// <summary>Пункт меню "Переместить в…".</summary>
    public string MoveMenuLabel => _localizer["MoveMenuLabel"];

    /// <summary>Пункт меню "Копировать в…".</summary>
    public string CopyToMenuLabel => _localizer["CopyToMenuLabel"];

    private bool CanMoveCopy(AddinFileRowViewModel? row) => !IsLocked && (row ?? SelectedFile) is not null;

    /// <summary>Открывает диалог цели и перемещает файл в другую версию/scope.</summary>
    [RelayCommand(CanExecute = nameof(CanMoveCopy))]
    public void MoveTo(AddinFileRowViewModel? row) => MoveCopy(row ?? SelectedFile, MoveCopyKind.Move);

    /// <summary>Открывает диалог цели и копирует файл в другую версию/scope.</summary>
    [RelayCommand(CanExecute = nameof(CanMoveCopy))]
    public void CopyTo(AddinFileRowViewModel? row) => MoveCopy(row ?? SelectedFile, MoveCopyKind.Copy);

    private void MoveCopy(AddinFileRowViewModel? row, MoveCopyKind kind)
    {
        if (row is null)
            return;

        if (!EnsureEditorSaved())
            return;

        var options = _dialogService.PromptMoveCopy(new MoveCopyRequest(row.File, kind));
        if (options is null)
            return;

        try
        {
            if (kind == MoveCopyKind.Move)
            {
                var result = _store.Move(row.File, options.Version, options.Scope, options.EnableAfter, options.Overwrite);
                SetUndo(new UndoMove(result.File, row.File, result.ReplacedPath, result.ReplacedContent));
                RevealTarget(result.File);
                Refresh();
                SelectByIdentity(result.File.FileName, result.File.Scope, result.File.Version);
            }
            else
            {
                var copied = _store.Copy(row.File, options.Version, options.Scope, options.EnableAfter, options.Overwrite);
                Refresh();
                _toastService.Show(_localizer["CopyDoneToMessage", copied.Version, copied.Scope]);
            }
        }
        catch (Exception ex) when (ex is IOException or RevitRunningException or UnauthorizedAccessException)
        {
            row.ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Перед переносом не даёт молча потерять правки открытого файла: сохраняет видимую панель
    /// или спрашивает подтверждение на отбрасывание; отказ отменяет операцию.
    /// </summary>
    private bool EnsureEditorSaved()
    {
        if (_dirtyState is not { IsDirty: true })
            return true;

        if (_dialogService.Confirm(_localizer["SaveBeforeMoveConfirm"]))
            return _dirtyState.TrySave();

        return _dialogService.Confirm(_localizer["DiscardBeforeMoveConfirm"]);
    }

    /// <summary>Подстраивает scope/version/state фильтры, чтобы цель перемещения была видна; поиск не трогаем.</summary>
    private void RevealTarget(AddinFile target)
    {
        if (ScopeFilter != ScopeFilter.All)
            ScopeFilter = target.Scope == AddinScope.User ? ScopeFilter.User : ScopeFilter.Machine;

        if (VersionFilter is not null && VersionFilter != target.Version)
            VersionFilter = target.Version;

        if (EnabledFilter != EnabledFilter.All
            && (EnabledFilter == EnabledFilter.Enabled) != target.Enabled)
        {
            EnabledFilter = target.Enabled ? EnabledFilter.Enabled : EnabledFilter.Disabled;
        }
    }

    /// <summary>Пункт контекстного меню пустого места "Обновить".</summary>
    public string RefreshMenuLabel => _localizer["RefreshMenuLabel"];

    /// <summary>Пункт контекстного меню пустого места "Новый файл".</summary>
    public string AddFileMenuLabel => _localizer["AddFileMenuLabel"];

    /// <summary>Пункт контекстного меню строки "Копировать путь".</summary>
    public string CopyPathsMenuLabel => _localizer["CopyPathsMenuLabel"];

    /// <summary>Пункт контекстного меню строки "Копировать имя файла".</summary>
    public string CopyFileNamesMenuLabel => _localizer["CopyFileNamesMenuLabel"];

    /// <summary>Копирует полные пути выделенных файлов (каждый в кавычках, как «Копировать как путь» в Проводнике).</summary>
    [RelayCommand]
    public void CopyPaths() => CopySelected(row => $"\"{row.FullPath}\"");

    /// <summary>Копирует имена выделенных файлов построчно.</summary>
    [RelayCommand]
    public void CopyFileNames() => CopySelected(row => row.FileName);

    private void CopySelected(Func<AddinFileRowViewModel, string> selector)
    {
        var text = string.Join(Environment.NewLine, Files.Where(file => file.IsSelected).Select(selector));
        if (_clipboard.TrySetText(text))
            _toastService.Show(_localizer["CopyDoneMessage"]);
    }

    /// <summary>Плейсхолдер поиска. "User"/"Machine"/"Scope"/годы/типы записей — технические токены данных, не переводятся.</summary>
    public string SearchPlaceholder => _localizer["ListSearchPlaceholder"];

    /// <summary>Тултип кнопки обновления (хоткей: F5).</summary>
    public string RefreshTooltip => $"{_localizer["RefreshButtonTooltip"]} (F5)";

    /// <summary>Чипс scope "все" со счётчиком файлов, подходящих под поиск.</summary>
    public string ScopeAllChipLabel => WithCount(_localizer["ScopeFilter_AllOption"], SearchMatchedCount);

    /// <summary>Чипс scope "User" со счётчиком.</summary>
    public string ScopeUserChipLabel => WithCount("User", CountScope(AddinScope.User));

    /// <summary>Чипс scope "Machine" со счётчиком.</summary>
    public string ScopeMachineChipLabel => WithCount("Machine", CountScope(AddinScope.Machine));

    /// <summary>Чипс версий "все" со счётчиком.</summary>
    public string VersionAllChipLabel => WithCount(_localizer["VersionFilter_AllVersionsOption"], SearchMatchedCount);

    /// <summary>Чипс версии 2021 со счётчиком.</summary>
    public string Version2021ChipLabel => WithCount("2021", CountVersion("2021"));

    /// <summary>Чипс версии 2022 со счётчиком.</summary>
    public string Version2022ChipLabel => WithCount("2022", CountVersion("2022"));

    /// <summary>Чипс версии 2023 со счётчиком.</summary>
    public string Version2023ChipLabel => WithCount("2023", CountVersion("2023"));

    /// <summary>Чипс версии 2024 со счётчиком.</summary>
    public string Version2024ChipLabel => WithCount("2024", CountVersion("2024"));

    /// <summary>Чипс версии 2025 со счётчиком.</summary>
    public string Version2025ChipLabel => WithCount("2025", CountVersion("2025"));

    /// <summary>Чипс версии 2026 со счётчиком.</summary>
    public string Version2026ChipLabel => WithCount("2026", CountVersion("2026"));

    /// <summary>Чипс версии 2027 со счётчиком.</summary>
    public string Version2027ChipLabel => WithCount("2027", CountVersion("2027"));

    /// <summary>Подпись "Состояние:" перед чипсами enabled/disabled.</summary>
    public string EnabledFilterLabel => _localizer["EnabledFilterLabel"];

    /// <summary>Чипс "все" по состоянию со счётчиком.</summary>
    public string EnabledAllChipLabel => WithCount(_localizer["EnabledFilter_AllOption"], SearchMatchedCount);

    /// <summary>Чипс "включённые" со счётчиком.</summary>
    public string EnabledOnChipLabel => WithCount(_localizer["EnabledFilter_EnabledOption"], CountEnabled(true));

    /// <summary>Чипс "выключенные" со счётчиком.</summary>
    public string EnabledOffChipLabel => WithCount(_localizer["EnabledFilter_DisabledOption"], CountEnabled(false));

    /// <summary>Стрелка текущего направления сортировки.</summary>
    public string SortDirectionGlyph => SortDescending ? "↓" : "↑";

    /// <summary>Тултип кнопки направления сортировки.</summary>
    public string SortDirectionTooltip => _localizer["SortDirectionTooltip"];

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
        RefreshChipLabels();
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

    partial void OnEnabledFilterChanged(EnabledFilter value)
    {
        _filesView.Refresh();
        RefreshSelectionSnapshot();
    }
#pragma warning restore S4144

    partial void OnSortOrderChanged(ListSortOrder value) => _filesView.Refresh();

    partial void OnSortDescendingChanged(bool value) => _filesView.Refresh();

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

        var enabled = EnabledFilter switch
        {
            EnabledFilter.Enabled => true,
            EnabledFilter.Disabled => false,
            _ => (bool?)null,
        };
        if (enabled is { } requiredEnabled && row.IsEnabled != requiredEnabled)
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
        var result = CompareRowsAscending(left, right);
        return SortDescending ? -result : result;
    }

    private int CompareRowsAscending(AddinFileRowViewModel left, AddinFileRowViewModel right)
    {
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
        BeginRenameCommand.NotifyCanExecuteChanged();
        MoveToCommand.NotifyCanExecuteChanged();
        CopyToCommand.NotifyCanExecuteChanged();
        RefreshSnapshot();
    }

    private void RefreshSnapshot() => OnPropertyChanged((string?)null);

    private interface IUndoRecord
    {
    }

    private sealed record UndoToggle(AddinFile After, bool PreviousEnabled) : IUndoRecord;

    private sealed record UndoToggleGroup(IReadOnlyList<UndoToggle> Toggles) : IUndoRecord;

    private sealed record UndoDelete(IReadOnlyList<DeletedFile> Files) : IUndoRecord;

    private sealed record UndoMove(
        AddinFile Moved,
        AddinFile Original,
        string? ReplacedPath,
        byte[]? ReplacedContent) : IUndoRecord;

    private sealed record DeletedFile(AddinFile File, byte[] Content);

    /// <inheritdoc />
    public override string ToString() => $"List(Files={FileCount}, Selected={SelectedFile?.FileName ?? "-"}, Bulk={SelectedCount})";
}
