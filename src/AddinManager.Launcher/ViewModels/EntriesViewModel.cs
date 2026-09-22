using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using AddinManager.Core.Abstractions.Guard;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Guard;
using AddinManager.Core.Manifests;
using AddinManager.Core.Parsing;
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

namespace AddinManager.Launcher.ViewModels;

/// <summary>
/// Подпанель записей манифеста: список записей внутри файла, выбранного в зоне списка (план,
/// раздел 6). От зон-соседей зависит только через узкие контракты (<see cref="IFileSelection"/>,
/// <see cref="IAddinFileCatalog"/>), которые имплементирует сама зона списка, — прямых ссылок
/// на соседние модели нет (docs/architecture.md, раздел MVVM).
/// Только показ и клик-выбор записи; структурное редактирование выбранной записи — отдельная
/// зона (<see cref="FormViewModel"/>), которая читает <see cref="SelectedEntry"/> отсюда через
/// <see cref="IEntrySelection"/> так же, как эта модель читает выбор файла.
/// </summary>
public sealed partial class EntriesViewModel : ObservableObject, IEntrySelection
{
    private readonly IFileSelection _fileSelection;
    private readonly IAddinFileCatalog _fileCatalog;
    private readonly ILocalizationService _localizationService;
    private readonly IStringLocalizer<EntriesViewModel> _localizer;
    private readonly IAddinEntryRowViewModelFactory _rowFactory;
    private readonly IAddinManifestParser _parser;
    private readonly IAddinMarkupService _markupService;
    private readonly IDialogService _dialogService;
    private readonly IUiDispatcher _dispatcher;
    private readonly IRevitProcessGuard _guard;
    private readonly ICollectionView _entriesView;

    /// <summary>Создает подпанель и сразу строит список по тому, что уже выбрано в списке.</summary>
    /// <param name="fileSelection">Выбор файла — источник <see cref="SelectedFile"/>.</param>
    /// <param name="fileCatalog">Каталог файлов — обновление после добавлений/удалений и кросс-файловые дубли.</param>
    /// <param name="localizationService">Сервис языка — смена языка перечитывает подписи.</param>
    /// <param name="localizer">Строки подпанели.</param>
    /// <param name="rowFactory">Создание строк записей (записи известны только при чтении файла).</param>
    /// <param name="parser">Сериализация манифеста без удалённой записи обратно в XML.</param>
    /// <param name="markupService">Атомарная запись + повторная валидация всего файла.</param>
    /// <param name="dialogService">Подтверждение удаления.</param>
    /// <param name="dispatcher">Маршалинг событий сторожа в поток UI.</param>
    /// <param name="guard">Сторож запущенного Revit — удаление запрещено, пока он жив.</param>
    public EntriesViewModel(
        IFileSelection fileSelection,
        IAddinFileCatalog fileCatalog,
        ILocalizationService localizationService,
        IStringLocalizer<EntriesViewModel> localizer,
        IAddinEntryRowViewModelFactory rowFactory,
        IAddinManifestParser parser,
        IAddinMarkupService markupService,
        IDialogService dialogService,
        IUiDispatcher dispatcher,
        IRevitProcessGuard guard)
    {
        _fileSelection = fileSelection;
        _fileCatalog = fileCatalog;
        _localizationService = localizationService;
        _localizer = localizer;
        _rowFactory = rowFactory;
        _parser = parser;
        _markupService = markupService;
        _dialogService = dialogService;
        _dispatcher = dispatcher;
        _guard = guard;
        _entriesView = CollectionViewSource.GetDefaultView(Entries);
        _entriesView.Filter = MatchesSearch;
        _fileSelection.PropertyChanged += OnFileSelectionChanged;
        _localizationService.LanguageChanged += (_, _) => RefreshSnapshot();
        _guard.Changed += (_, _) => _dispatcher.Invoke(SyncEditLock);
        LoadFrom(_fileSelection.SelectedFile);
        SyncEditLock();
    }

    /// <summary>Выбранный в списке файл — источник <see cref="Entries"/>. <see langword="null"/>, если ничего не выбрано.</summary>
    [ObservableProperty]
    private AddinFileRowViewModel? _selectedFile;

    /// <summary>Записи текущего <see cref="SelectedFile"/>, в порядке из файла.</summary>
    public ObservableCollection<AddinEntryRowViewModel> Entries { get; } = [];

    /// <summary>То, что реально показывает вид: <see cref="Entries"/> с применённым поиском.</summary>
    public ICollectionView EntriesView => _entriesView;

    /// <summary>Выбранная кликом запись. <see langword="null"/>, если ничего не выбрано.</summary>
    [ObservableProperty]
    private AddinEntryRowViewModel? _selectedEntry;

    /// <summary>Текст поиска по записям — фильтрует <see cref="EntriesView"/>.</summary>
    [ObservableProperty]
    private string? _searchText;

    /// <summary>Revit запущен — удаление запрещено.</summary>
    [ObservableProperty]
    private bool _isLocked;

    /// <summary>Сколько записей отмечено чекбоксом пакетного выбора.</summary>
    [ObservableProperty]
    private int _selectedCount;

    /// <summary>Есть ли хоть одна отмеченная запись — видимость bulk-панели.</summary>
    [ObservableProperty]
    private bool _hasSelection;

    /// <summary>Ошибка последнего удаления — баннером под заголовком, как у формы.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Тултип крестика строки.</summary>
    public string DeleteEntryTooltip => _localizer["DeleteEntryTooltip"];

    /// <summary>
    /// Добавляет запись выбранного в диалоге типа как отдельное изменение в файле: генерирует
    /// новый AddInId и заполняет заголовок (Name — Application/DBApplication, Text — Command),
    /// остальные поля пустые — их заполнит форма, — и сразу пишет файл тем же атомарным путём,
    /// что удаление. Ошибка сохранения — баннером, запись не добавляется.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddEntry))]
    public void AddEntry()
    {
        if (SelectedFile is not { } file)
            return;

        if (_dialogService.PromptNewEntry() is not { } type)
            return;

        var displayName = _localizer["NewEntryDisplayName"].Value;
        string? name = type == AddinEntryType.Command ? null : displayName;
        string? text = type == AddinEntryType.Command ? displayName : null;
        var added = new AddinEntry(
            type, null, name, text, null, null, string.Empty, Guid.NewGuid(), string.Empty,
            null, null, null, [], [], null, null, null, [], []);

        var manifest = file.File.Manifest;
        var xml = _parser.ToXml(manifest with { Entries = manifest.Entries.Append(added).ToList() });

        try
        {
            _markupService.Save(file.File, xml);
            ErrorMessage = null;
            _fileCatalog.Refresh();
            SelectedEntry = Entries.FirstOrDefault(e => e.Entry.AddInId == added.AddInId);
        }
        catch (Exception ex) when (ex is AddinManifestFormatException or IOException or RevitRunningException)
        {
            ErrorMessage = ex.Message;
        }
    }

    private bool CanAddEntry() => SelectedFile is not null && !IsLocked;

    private bool CanDeleteEntry(AddinEntryRowViewModel? row) => row is not null && !IsLocked;

    /// <summary>
    /// Переключатель "Выбрать все/Снять выбор": все видимые (после поиска) отмечены —
    /// сбрасывает весь выбор, иначе отмечает все видимые.
    /// </summary>
    [RelayCommand]
    public void ToggleSelectAll()
    {
        var visible = _entriesView.Cast<AddinEntryRowViewModel>().ToList();
        if (visible.Count != 0 && !visible.All(e => e.IsSelected))
        {
            foreach (var row in visible)
                row.IsSelected = true;
        }
        else
        {
            foreach (var row in Entries)
                row.IsSelected = false;
        }
    }

    private bool CanBulkDelete() => HasSelection && !IsLocked;

    /// <summary>
    /// Удаляет все отмеченные записи одним сохранением файла (после одного подтверждения).
    /// Ошибка сохранения — в баннер, записи остаются.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanBulkDelete))]
    public void DeleteSelected()
    {
        var targets = Entries.Where(e => e.IsSelected).ToList();
        if (targets.Count == 0 || SelectedFile?.File is not { } file)
            return;

        if (!_dialogService.Confirm(_localizer["DeleteEntriesConfirm", targets.Count]))
            return;

        var condemned = new HashSet<Guid>(targets.Select(e => e.Entry.AddInId));
        var remaining = file.Manifest.Entries.Where(e => !condemned.Contains(e.AddInId)).ToList();
        var xml = _parser.ToXml(file.Manifest with { Entries = remaining });

        try
        {
            _markupService.Save(file, xml);
            ErrorMessage = null;
            _fileCatalog.Refresh();
        }
        catch (Exception ex) when (ex is AddinManifestFormatException or IOException or RevitRunningException)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Удаляет запись из файла навсегда (после подтверждения): выкидывает её из манифеста,
    /// пишет файл целиком тем же атомарным путём, что форма, и обновляет список.
    /// </summary>
    /// <param name="row">Строка с крестика (может быть невыбранной).</param>
    [RelayCommand(CanExecute = nameof(CanDeleteEntry))]
    public void DeleteEntry(AddinEntryRowViewModel? row)
    {
        if (row?.Entry is not { } entry || SelectedFile?.File is not { } file)
            return;

        if (!_dialogService.Confirm(_localizer["DeleteEntryConfirm", row.DisplayName]))
            return;

        var remaining = file.Manifest.Entries.Where(e => e.AddInId != entry.AddInId).ToList();
        var xml = _parser.ToXml(file.Manifest with { Entries = remaining });

        try
        {
            _markupService.Save(file, xml);
            ErrorMessage = null;
            _fileCatalog.Refresh();
        }
        catch (Exception ex) when (ex is AddinManifestFormatException or IOException or RevitRunningException)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void OnFileSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IFileSelection.SelectedFile) or null)
            LoadFrom(((IFileSelection)sender!).SelectedFile);
    }

    private void LoadFrom(AddinFileRowViewModel? file)
    {
        // Восстанавливаем выбор по AddInId, а не сбрасываем его в null безусловно: LoadFrom
        // пересоздаёт все AddinEntryRowViewModel не только при смене файла, но и когда файл
        // просто перечитан после сохранения в форме (FormViewModel.Save дергает
        // IAddinFileCatalog.Refresh, а он транслируется сюда той же PropertyChanged-подпиской).
        // Без восстановления по идентичности сохранение в форме само закрывало бы только что
        // открытую форму, сбрасывая SelectedEntry — тот же приём, что зона списка уже
        // применяет к SelectedFile по (FileName, Scope, Version).
        var previousEntryId = SelectedEntry?.Entry.AddInId;
        var previousBulkSelection = new HashSet<Guid>(Entries.Where(e => e.IsSelected).Select(e => e.Entry.AddInId));

        SelectedFile = file;
        foreach (var row in Entries)
            row.PropertyChanged -= OnRowPropertyChanged;
        Entries.Clear();
        ErrorMessage = null;

        if (file is null)
        {
            SelectedEntry = null;
            RefreshSelectionSnapshot();
            return;
        }

        var entries = file.File.Manifest.Entries;
        var inFileCounts = entries.GroupBy(e => e.AddInId).ToDictionary(g => g.Key, g => g.Count());
        var acrossFiles = FindCrossFileIds(file);

        var index = 1;
        foreach (var entry in entries)
        {
            var duplicateInFile = inFileCounts.TryGetValue(entry.AddInId, out var count) && count > 1;
            var row = _rowFactory.Create(entry, index++, duplicateInFile, acrossFiles.Contains(entry.AddInId));
            row.PropertyChanged += OnRowPropertyChanged;
            row.IsSelected = previousBulkSelection.Contains(entry.AddInId);
            Entries.Add(row);
        }

        _entriesView.Refresh();

        // Восстановленной по AddInId записи нет — ни прежнего выбора не было (запуск
        // приложения, только что выбранный файл), ни прежняя запись не пережила перечитывание
        // файла — выбираем первую видимую (отфильтрованную поиском) по умолчанию.
        var restored = previousEntryId is { } id ? Entries.FirstOrDefault(e => e.Entry.AddInId == id) : null;
        SelectedEntry = restored ?? _entriesView.Cast<AddinEntryRowViewModel>().FirstOrDefault();
        RefreshSelectionSnapshot();
    }

    partial void OnSelectedFileChanged(AddinFileRowViewModel? value)
    {
        AddEntryCommand.NotifyCanExecuteChanged();
        RefreshSnapshot();
    }

    partial void OnSelectedEntryChanged(AddinEntryRowViewModel? value)
    {
        DeleteEntryCommand.NotifyCanExecuteChanged();
        RefreshSnapshot();
    }

    partial void OnIsLockedChanged(bool value)
    {
        AddEntryCommand.NotifyCanExecuteChanged();
        DeleteEntryCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchTextChanged(string? value) => _entriesView.Refresh();

    /// <summary>
    /// Идентификаторы других файлов той же версии Revit (тот же scope правила, что у списка:
    /// дубли считаются только внутри версии). Текущий файл исключён по идентичности
    /// (<c>FileName</c>/<c>Scope</c>/<c>Version</c>), а не по ссылке — строки пересоздаются.
    /// </summary>
    private HashSet<Guid> FindCrossFileIds(AddinFileRowViewModel file)
    {
        var others = new HashSet<Guid>();
        foreach (var row in _fileCatalog.Files)
        {
            if (row.Version != file.Version)
                continue;
            if (row.FileName == file.FileName && row.Scope == file.Scope)
                continue;
            foreach (var entry in row.File.Manifest.Entries)
                others.Add(entry.AddInId);
        }

        return others;
    }

    /// <summary>Разносит <see cref="IRevitProcessGuard.IsRunning"/> на себя.</summary>
    private void SyncEditLock() => IsLocked = _guard.IsRunning;

    /// <summary>Приглашение выбрать файл, когда ничего не выбрано.</summary>
    public string EmptySelectionPrompt => _localizer["EmptySelection_FilePrompt"];

    /// <summary>Кнопка "Отменить" (и её скрытый placeholder для выравнивания заголовков).</summary>
    public string DiscardButtonLabel => _localizer["DiscardButton"];

    /// <summary>Кнопка "Сохранить" (и её скрытый placeholder для выравнивания заголовков).</summary>
    public string SaveButtonLabel => _localizer["SaveButton"];

    /// <summary>Тултип иконки "Добавить запись".</summary>
    public string AddEntryTooltip => _localizer["AddEntryTooltip"];

    /// <summary>Плейсхолдер поиска по записям.</summary>
    public string SearchPlaceholder => _localizer["EntriesSearchPlaceholder"];

    /// <summary>"Выбрано: N" в bulk-панели.</summary>
    public string SelectionSummary => _localizer["SelectionSummary", SelectedCount];

    /// <summary>
    /// Подпись переключателя "Выбрать все/Снять выбор": все видимые отмечены (или выбор
    /// спрятан за поиском) — "Снять выбор", иначе "Выбрать все".
    /// </summary>
    public string SelectAllToggleLabel
    {
        get
        {
            var visible = _entriesView.Cast<AddinEntryRowViewModel>().ToList();
            var showClear = visible.Count == 0 ? HasSelection : visible.All(e => e.IsSelected);
            return showClear ? _localizer["ClearSelectionLabel"] : _localizer["SelectAllLabel"];
        }
    }

    /// <summary>Кнопка bulk-панели "Удалить".</summary>
    public string DeleteSelectedLabel => _localizer["DeleteSelectedLabel"];

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AddinEntryRowViewModel.IsSelected))
            RefreshSelectionSnapshot();
    }

    /// <summary>
    /// Пересчитывает <see cref="SelectedCount"/>/<see cref="HasSelection"/> и доступность
    /// bulk-удаления. Вызывается при смене отметок и после каждой пересборки <see cref="Entries"/>.
    /// </summary>
    private void RefreshSelectionSnapshot()
    {
        SelectedCount = Entries.Count(e => e.IsSelected);
        HasSelection = SelectedCount > 0;

        DeleteSelectedCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(SelectAllToggleLabel));
    }

    /// <summary>Предикат <see cref="ICollectionView.Filter"/>: запись проходит, если текст поиска пуст или встречается в её полях.</summary>
    private bool MatchesSearch(object obj)
    {
        if (obj is not AddinEntryRowViewModel row)
            return false;

        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        var needle = SearchText.Trim();
        return Contains(row.DisplayName, needle)
            || Contains(row.Type.ToString(), needle)
            || Contains(row.VendorSubtitle, needle)
            || Contains(row.AddInIdText, needle)
            || Contains(row.Entry.AssemblyPath, needle)
            || Contains(row.Entry.FullClassName, needle);
    }

    private static bool Contains(string? value, string needle) =>
        value?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true;

    private void RefreshSnapshot() => OnPropertyChanged((string?)null);

    /// <inheritdoc />
    public override string ToString() =>
        $"Entries(File={SelectedFile?.FileName ?? "-"}, Count={Entries.Count}, Selected={SelectedEntry?.DisplayName ?? "-"}, Bulk={SelectedCount})";
}
