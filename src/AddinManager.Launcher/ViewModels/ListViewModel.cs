using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using AddinManager.Core;
using AddinManager.Core.Abstractions.Guard;
using AddinManager.Core.Abstractions.Installations;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Guard;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.Services;
using AddinManager.Localization.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace AddinManager.Launcher.ViewModels;

/// <summary>Зона списка: файлы всех версий Revit — поиск, фильтр и группировка по scope/версии (план, раздел 6).</summary>
public sealed partial class ListViewModel : ObservableObject, IFileSelection, IAddinFileCatalog
{
    private readonly IAddinChangeWatcher _changeWatcher;
    private readonly IUiDispatcher _dispatcher;
    private readonly IUndoService _undoService;
    private readonly ILocalizationService _localizationService;
    private readonly IStringLocalizer<ListViewModel> _localizer;
    private readonly FileCatalog _catalog;
    private readonly IInstalledRevitVersions _installedVersions;
    private readonly IFileOperations _operations;
    private readonly IToastService _toastService;
    private readonly IClipboardService _clipboard;
    private readonly IRevitProcessGuard _guard;
    private readonly IDialogService _dialogService;
    private readonly IFolderOpener _folderOpener;
    private readonly IEditorDirtyState? _dirtyState;
    private readonly FileListViewState _viewState;
    private readonly ILogger<ListViewModel> _logger;
    private IReadOnlyList<string> _visibleVersions = [];
    private AddinFileRowViewModel? _selectedFile;

    [ObservableProperty]
    private int _fileCount;

    /// <summary>
    /// Выбранная кликом строка (план, раздел 6) — единственная точка смены выбора для всех зон:
    /// подпанели читают её через <see cref="IFileSelection"/>. Перед переходом на другой файл
    /// спрашивает редактор о несохранённых правках (<see cref="EnsureCanLeaveSelectedFile"/>):
    /// «Сохранить / Отбросить / Отмена»; отказ оставляет прежний выбор, а вид возвращается
    /// к нему отложенным уведомлением (синхронное внутри обновления привязки WPF игнорируется).
    /// <see langword="null"/>, если ничего не выбрано.
    /// </summary>
#pragma warning disable S4275 // Сеттер намеренно делегирует запись в SetSelectedFile: перед сменой выбора нужен вопрос о несохранённых правках.
    public AddinFileRowViewModel? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (ReferenceEquals(_selectedFile, value))
                return;

            if (!EnsureCanLeaveSelectedFile())
            {
                _logger.LogInformation(
                    "Выбор файла: переход на {TargetFile} отменён — в редакторе остались несохранённые правки",
                    value?.FileName ?? "-");
                _dispatcher.Post(() => OnPropertyChanged(nameof(SelectedFile)));
                return;
            }

            // Сохранение правок могло пересоздать строки (Refresh после Save), поэтому цель
            // ищем заново по идентичности: старый экземпляр строки уже не входит в Files.
            SetSelectedFile(value is null ? null : FindByIdentity(value.File) ?? value);
        }
    }
#pragma warning restore S4275

    /// <summary>
    /// Меняет выбор без вопроса о несохранённых правках — для программных переходов, которые уже
    /// прошли через <see cref="EnsureCanLeaveSelectedFile"/> или пересоздают строки (<see cref="Refresh"/>,
    /// undo, переименование): спрашивать в этих путях нечего.
    /// </summary>
    private void SetSelectedFile(AddinFileRowViewModel? value)
    {
        if (ReferenceEquals(_selectedFile, value))
            return;

        _selectedFile = value;
        OnPropertyChanged(nameof(SelectedFile));
        ShowInFolderCommand.NotifyCanExecuteChanged();
        BeginRenameCommand.NotifyCanExecuteChanged();
        MoveToCommand.NotifyCanExecuteChanged();
        CopyToCommand.NotifyCanExecuteChanged();
        RefreshSnapshot();
    }

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

    /// <summary><see langword="null"/> — все версии; иначе одна из версий в <see cref="VersionChips"/>.</summary>
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
    /// <param name="operations">Создание, удаление, перемещение и копирование файлов (диалоги, store, undo).</param>
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
    /// <param name="catalog">Каталог строк: сканирование, пересоздание строк и предупреждения.</param>
    /// <param name="installedVersions">Установленные на ПК версии Revit: только их (и версии с файлами-остатками) показывает фильтр.</param>
    /// <param name="toastService">Подтверждение ручного обновления тостом.</param>
    /// <param name="guard">Сторож запущенного Revit — тогглы строк гаснут, пока он жив.</param>
    /// <param name="dialogService">Подтверждение смены файла при несохранённых правках.</param>
    /// <param name="folderOpener">Показ файла в проводнике (контекстное меню строки).</param>
    /// <param name="clipboard">Буфер обмена для пунктов «Копировать путь/имя файла».</param>
    /// <param name="undoService">Одноуровневый стек отмены: команда <c>Ctrl+Z</c> и её состояние.</param>
    /// <param name="dirtyState">Состояние несохранённых правок редактора для проверки перед сменой файла.</param>
    public ListViewModel(
        IFileOperations operations,
        IAddinChangeWatcher changeWatcher,
        IUiDispatcher dispatcher,
        ILogger<ListViewModel> logger,
        ILocalizationService localizationService,
        IStringLocalizer<ListViewModel> localizer,
        FileCatalog catalog,
        IInstalledRevitVersions installedVersions,
        IToastService toastService,
        IRevitProcessGuard guard,
        IDialogService dialogService,
        IFolderOpener folderOpener,
        IClipboardService clipboard,
        IUndoService undoService,
        IEditorDirtyState? dirtyState = null)
    {
        _operations = operations;
        _changeWatcher = changeWatcher;
        _dispatcher = dispatcher;
        _logger = logger;
        _localizationService = localizationService;
        _localizer = localizer;
        _catalog = catalog;
        _installedVersions = installedVersions;
        _toastService = toastService;
        _clipboard = clipboard;
        _guard = guard;
        _dialogService = dialogService;
        _folderOpener = folderOpener;
        _undoService = undoService;
        _dirtyState = dirtyState;

        _viewState = new FileListViewState(Files);

        Refresh();

        // Дребезг сырых событий уже погашен внутри changeWatcher — здесь только маршалинг.
        _changeWatcher.Changed += (_, _) => _dispatcher.Invoke(Refresh);
        _changeWatcher.Start();

        // Смена языка тоже идёт через Refresh: строки пересоздаются с новыми подписями.
        _localizationService.LanguageChanged += (_, _) => _dispatcher.Invoke(Refresh);

        // Событие сторожа может прийти не из потока UI — маршалим, как watcher выше.
        // Сам guard здесь не стартуем (владелец — MainViewModel), только читаем состояние.
        _guard.Changed += (_, _) => _dispatcher.Invoke(SyncEditLock);

        // Стек отмены меняется только с UI-потока, но маршрут через dispatcher тот же, что у
        // сторожа: команда и подпись HasUndo обновляются в одном месте.
        _undoService.Changed += (_, _) => _dispatcher.Invoke(OnUndoStackChanged);
        SyncEditLock();
    }

    /// <summary>Сырые строки списка — всё, что прочитано с диска, без фильтра/сортировки/группировки.</summary>
    public ObservableCollection<AddinFileRowViewModel> Files => _catalog.Files;

    /// <summary>То, что реально показывает вид: <see cref="Files"/> с применённым фильтром, сортировкой и группировкой.</summary>
    public ICollectionView FilesView => _viewState.View;

    /// <summary>
    /// Чипсы версий Revit: «все», установленные на ПК версии и версии, в которых остались файлы
    /// (например, после удаления Revit). Порядок — каталога Core.
    /// </summary>
    public ObservableCollection<FilterChipViewModel> VersionChips { get; } = [];

    /// <summary>Перечитывает файлы всех версий с диска (план, раздел 4 — событийный rescan).</summary>
    [RelayCommand]
    public void Refresh()
    {
        ErrorMessage = null;

        // Каталог пересоздаёт все AddinFileRowViewModel — старый SelectedFile больше не входит в
        // Files, поэтому выбор восстанавливаем по идентичности (FileName, Scope, Version),
        // а пакетную отметку каталог сохраняет сам.
        var previousSelection = SelectedFile?.Identity;
        _catalog.Refresh();
        RefreshVersionChips();
        WireRows();

        FileCount = Files.Count;
        RefreshChipLabels();
        _viewState.Apply(CurrentQuery());

        // Восстановленного по идентичности файла нет — ни прежнего выбора не было (запуск
        // приложения), ни прежний файл не пережил пересканирование — выбираем первый в
        // отображаемом (отфильтрованном/отсортированном) порядке, а не оставляем список без
        // выделения.
        var restored = previousSelection is { } key ? FindByIdentity(key) : null;
        SetSelectedFile(restored ?? _viewState.View.Cast<AddinFileRowViewModel>().FirstOrDefault());
        RefreshSelectionSnapshot();
        SyncEditLock();

        if (previousSelection is { } previous)
        {
            if (restored is not null)
                _logger.LogDebug("Refresh: выбор {FileName} пережил пересканирование", restored.FileName);
            else
                _logger.LogInformation(
                    "Refresh: прежний выбор {FileName} ({Scope}, {Version}) не пережил пересканирование, выбран {NewFileName}",
                    previous.FileName, previous.Scope, previous.Version, SelectedFile?.FileName ?? "-");
        }
    }

    /// <summary>
    /// Подписывает строки каталога на действия зоны списка. Строки пересоздаются при каждом
    /// <see cref="Refresh"/>, поэтому подписки ставятся заново; старые строки уходят вместе с
    /// коллекцией.
    /// </summary>
    private void WireRows()
    {
        foreach (var row in Files)
        {
            row.DeleteRequested += OnRowDeleteRequested;
            row.Renamed += OnRowRenamed;
            row.Toggled += OnRowToggled;
            row.PropertyChanged += OnRowPropertyChanged;
        }
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
        _viewState.View.Cast<AddinFileRowViewModel>();

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
            _undoService.Record(new UndoToggleGroup(toggles));

        RefreshSelectionSnapshot();
    }

    /// <summary>
    /// Удаляет все отмеченные файлы с диска после одного подтверждения. Ошибки отдельных
    /// файлов — в баннер над списком, удалённые уже не восстановить через <c>Refresh</c>.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanBulkEdit))]
    public void DeleteSelected() =>
        ApplyOutcome(_operations.DeleteSelected(Files.Where(row => row.IsSelected).ToList()));

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // IsSelected двигает счётчик и доступность команд; IsEnabled — подпись
        // переключателя "Включить/Выключить" и фильтр состояния.
        if (e.PropertyName == nameof(AddinFileRowViewModel.IsEnabled))
        {
            _viewState.Apply(CurrentQuery());
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

        // Версии отфильтрованы до установленных на ПК: чипсы пересчитывают подписи и признак выбора.
        VersionChips[0].Label = WithCount(_localizer["VersionFilter_AllVersionsOption"], SearchMatchedCount);
        VersionChips[0].IsActive = VersionFilter is null;
        for (var index = 0; index < _visibleVersions.Count; index++)
        {
            var version = _visibleVersions[index];
            var chip = VersionChips[index + 1];
            chip.Label = WithCount(version, CountVersion(version));
            chip.IsActive = VersionFilter == version;
        }

        OnPropertyChanged(nameof(EnabledAllChipLabel));
        OnPropertyChanged(nameof(EnabledOnChipLabel));
        OnPropertyChanged(nameof(EnabledOffChipLabel));
    }

    /// <summary>
    /// Пересобирает чипсы версий: установленные на ПК версии плюс версии, в которых после
    /// сканирования остались файлы (остатки после удаления Revit — их всё ещё можно отфильтровать
    /// и удалить). Коллекция пересоздаётся только при изменении набора, чтобы каждый
    /// <see cref="Refresh"/> не сбрасывал выделение чипса.
    /// </summary>
    private void RefreshVersionChips()
    {
        var candidates = new HashSet<string>(_installedVersions.GetVersions(), StringComparer.Ordinal);
        candidates.UnionWith(Files.Select(row => row.Version));
        List<string> visible = [.. RevitVersionCatalog.SupportedVersions.Where(candidates.Contains)];

        if (VersionChips.Count > 0 && visible.SequenceEqual(_visibleVersions, StringComparer.Ordinal))
            return;

        _visibleVersions = visible;
        VersionChips.Clear();
        VersionChips.Add(new FilterChipViewModel(string.Empty, null));
        foreach (var version in visible)
            VersionChips.Add(new FilterChipViewModel(version, version));

        // Выбранная версия пропала из фильтра (Revit удалён и файлов не осталось) — сбрасываем
        // на «все», иначе вид остался бы отфильтрованным по невидимому чипсу.
        if (VersionFilter is not null && !candidates.Contains(VersionFilter))
            VersionFilter = null;
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
        _undoService.Record(new UndoToggle(e.After, e.Before.Enabled));

    /// <summary>Можно ли отменить последнее действие (есть запись и не мешает живой Revit).</summary>
    public bool HasUndo => _undoService.HasUndo;

    private bool CanUndo() => _undoService.HasUndo && !IsLocked;

    /// <summary>
    /// Отменяет последнее действие через <see cref="IUndoService"/>: сервис выполняет мутации
    /// store-а, список перечитывает диск и возвращает выбор туда, куда указал сервис.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    public void UndoLastAction()
    {
        if (!_undoService.HasUndo)
            return;

        try
        {
            AddinFileIdentity selection = _undoService.Undo(identity => FindByIdentity(identity)?.File);
            Refresh();
            SelectByIdentity(selection);
            _toastService.Show(_localizer["UndoDoneMessage"]);
        }
        catch (Exception ex) when (ex is IOException or RevitRunningException or UnauthorizedAccessException)
        {
            // Сервис/store уже залогировали причину; наверх уходит только сообщение для баннера.
            ErrorMessage = ex.Message;
            _toastService.Show(_localizer["UndoFailedMessage"]);
        }
    }

    /// <summary>Стек отмены изменился: подпись <see cref="HasUndo"/> и команда меняются вместе.</summary>
    private void OnUndoStackChanged()
    {
        OnPropertyChanged(nameof(HasUndo));
        UndoLastActionCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Строка списка по идентичности файла <c>(FileName, Scope, Version)</c>.</summary>
    private AddinFileRowViewModel? FindByIdentity(AddinFileIdentity identity) =>
        Files.FirstOrDefault(row => row.Identity == identity);

    /// <summary>Строка списка по текущей записи файла.</summary>
    private AddinFileRowViewModel? FindByIdentity(AddinFile file) => FindByIdentity(file.Identity);

    private void SelectByIdentity(AddinFileIdentity identity)
    {
        var row = FindByIdentity(identity);
        if (row is null)
            return;

        SetSelectedFile(row);
        row.IsSelected = true;
        RefreshSelectionSnapshot();
    }

    /// <summary>
    /// Возвращается после успешного переименования строки: перечитывает список (файл на диске уже
    /// с новым именем) и восстанавливает выбор на переименованном файле, а не на первой строке.
    /// </summary>
    private void OnRowRenamed(object? sender, FileRenamedEventArgs e)
    {
        if (sender is not AddinFileRowViewModel)
            return;

        _undoService.Record(new UndoRename(e.Before, e.After));
        var renamedIdentity = e.After.Identity;
        Refresh();

        var renamed = FindByIdentity(renamedIdentity);
        if (renamed is null)
            return;

        SetSelectedFile(renamed);
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

    private void OnRowDeleteRequested(object? sender, EventArgs e)
    {
        if (sender is AddinFileRowViewModel row)
            ApplyOutcome(_operations.DeleteRow(row));
    }

    /// <summary>Тултип кнопки добавления файла (хоткей: Ctrl+N).</summary>
    public string AddFileTooltip => $"{_localizer["AddFileTooltip"]} (Ctrl+N)";

    private bool CanAddFile() => !IsLocked;

    /// <summary>
    /// Создаёт файл по параметрам из диалога. Ошибка (уже есть, нет прав, живой Revit) —
    /// в баннер над списком.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddFile))]
    public void AddFile() => ApplyOutcome(_operations.AddFile());

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
    public void MoveTo(AddinFileRowViewModel? row)
    {
        if ((row ?? SelectedFile) is { } target)
            ApplyOutcome(_operations.MoveCopy(target, MoveCopyKind.Move));
    }

    /// <summary>Открывает диалог цели и копирует файл в другую версию/scope.</summary>
    [RelayCommand(CanExecute = nameof(CanMoveCopy))]
    public void CopyTo(AddinFileRowViewModel? row)
    {
        if ((row ?? SelectedFile) is { } target)
            ApplyOutcome(_operations.MoveCopy(target, MoveCopyKind.Copy));
    }

    /// <summary>
    /// Не даёт молча потерять правки открытого файла перед сменой выбранного файла: сохраняет
    /// видимую панель или спрашивает подтверждение на отбрасывание; отказ оставляет прежний выбор.
    /// </summary>
    private bool EnsureCanLeaveSelectedFile()
    {
        if (_dirtyState is not { IsDirty: true })
            return true;

        if (_dialogService.Confirm(_localizer["SaveBeforeSwitchConfirm"]))
            return _dirtyState.TrySave();

        return _dialogService.Confirm(_localizer["DiscardBeforeSwitchConfirm"]);
    }

    /// <summary>
    /// Применяет результат файловой операции к виду: сначала фильтры (чтобы цель попала в
    /// видимый набор), затем перечитывание, выбор, баннер и тост.
    /// </summary>
    /// <param name="outcome">Результат операции из <see cref="IFileOperations"/>.</param>
    private void ApplyOutcome(FileOperationOutcome outcome)
    {
        if (outcome.RevealTarget is { } target)
            RevealTarget(target);

        if (outcome.RefreshNeeded)
            Refresh();

        if (outcome.SelectIdentity is { } identity)
            SelectByIdentity(identity);

        if (outcome.BannerError is { } error)
            ErrorMessage = error;

        if (outcome.Toast is { } toast)
            _toastService.Show(toast);
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
        _viewState.Apply(CurrentQuery());
        RefreshChipLabels();
        RefreshSelectionSnapshot();
    }

    partial void OnScopeFilterChanged(ScopeFilter value)
    {
        _viewState.Apply(CurrentQuery());
        RefreshSelectionSnapshot();
    }

    partial void OnVersionFilterChanged(string? value)
    {
        _viewState.Apply(CurrentQuery());
        RefreshChipLabels();
        RefreshSelectionSnapshot();
    }

    partial void OnEnabledFilterChanged(EnabledFilter value)
    {
        _viewState.Apply(CurrentQuery());
        RefreshSelectionSnapshot();
    }
#pragma warning restore S4144

    partial void OnSortOrderChanged(ListSortOrder value) => _viewState.Apply(CurrentQuery());

    partial void OnSortDescendingChanged(bool value) => _viewState.Apply(CurrentQuery());

    partial void OnGroupByScopeChanged(bool value)
    {
        // Взаимоисключающе: включение одного гасит другой, чей OnChanged применит состояние.
        if (value && GroupByVersion)
            GroupByVersion = false;
        else
            _viewState.Apply(CurrentQuery());
    }

    partial void OnGroupByVersionChanged(bool value)
    {
        if (value && GroupByScope)
            GroupByScope = false;
        else
            _viewState.Apply(CurrentQuery());
    }

    /// <summary>Снимок текущих осей фильтра/сортировки/группировки для <see cref="FileListViewState"/>.</summary>
    private FileListQuery CurrentQuery() => new(
        ScopeFilter,
        VersionFilter,
        EnabledFilter,
        SearchText,
        SortOrder,
        SortDescending,
        GroupByScope,
        GroupByVersion);

    partial void OnFileCountChanged(int value) => RefreshSnapshot();

    private void RefreshSnapshot() => OnPropertyChanged((string?)null);

    /// <inheritdoc />
    public override string ToString() => $"List(Files={FileCount}, Selected={SelectedFile?.FileName ?? "-"}, Bulk={SelectedCount})";
}
