using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using AddinManager.Core.Abstractions.Guard;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Guard;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.Services;
using AddinManager.Localization;
using AddinManager.Localization.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace AddinManager.Launcher.ViewModels;

/// <summary>
/// Подпанель разметки: сырой XML выбранного в списке файла — просмотр, правка, валидация и
/// сохранение (план, раздел 6 — "The raw XML tab shows the whole file").
/// </summary>
public sealed partial class MarkupViewModel : ObservableObject
{
    private readonly IFileSelection _fileSelection;
    private readonly IEntrySelection _entrySelection;
    private readonly IAddinFileCatalog _fileCatalog;
    private readonly IAddinMarkupService _markupService;
    private readonly ILogger<MarkupViewModel> _logger;
    private readonly ILocalizationService _localizationService;
    private readonly IStringLocalizer<MarkupViewModel> _localizer;
    private readonly IUiDispatcher _dispatcher;
    private readonly IRevitProcessGuard _guard;
    private AddinFile? _boundFile;
    private string? _savedXml;
    private bool _loading;

    [ObservableProperty]
    private AddinFileRowViewModel? _selectedFile;

    [ObservableProperty]
    private string? _rawXml;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isDirty;

    /// <summary>Revit запущен — редактор только для чтения, сохранение запрещено.</summary>
    [ObservableProperty]
    private bool _isLocked;

    /// <summary>
    /// Срез блока выбранной записи в <see cref="RawXml"/> для подсветки в редакторе.
    /// <see langword="null"/> — записи нет или её блока нет в текущем тексте.
    /// </summary>
    [ObservableProperty]
    private TextSpan? _selectedEntrySpan;

    /// <summary>
    /// Диагностические срезы в <see cref="RawXml"/> для подсветки в редакторе: пустые
    /// обязательные поля (<c>Assembly</c>/<c>AddInId</c>/<c>FullClassName</c>) и
    /// повторяющиеся значения <c>AddInId</c> — то же, что блокирует сохранение.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<TextSpan> _diagnosticSpans = [];

    /// <summary>
    /// Создает подпанель и сразу загружает то, что уже выбрано в списке.
    /// </summary>
    /// <param name="fileSelection">
    /// Выбор файла — источник разметки. Прямая зависимость только на узкий контракт,
    /// а не на зону целиком (docs/architecture.md, раздел MVVM).
    /// </param>
    /// <param name="markupService">Чтение/валидация/сохранение сырого XML.</param>
    /// <param name="logger">Логгер подпанели.</param>
    /// <param name="localizationService">Сервис языка — смена языка перечитывает подписи через <see cref="RefreshSnapshot"/>.</param>
    /// <param name="localizer">Строки подпанели и её ошибок.</param>
    /// <param name="dispatcher">Маршалинг событий сторожа в поток UI.</param>
    /// <param name="guard">Сторож запущенного Revit — редактор гаснет, пока он жив.</param>
    /// <param name="entrySelection">
    /// Выбор записи — источник подсвечиваемого блока. Тот же узкий контракт,
    /// что уже есть у <see cref="FormViewModel"/>.
    /// </param>
    /// <param name="fileCatalog">Каталог файлов — обновление после сохранений и кросс-файловые дубли.</param>
    public MarkupViewModel(
        IFileSelection fileSelection,
        IAddinMarkupService markupService,
        ILogger<MarkupViewModel> logger,
        ILocalizationService localizationService,
        IStringLocalizer<MarkupViewModel> localizer,
        IUiDispatcher dispatcher,
        IRevitProcessGuard guard,
        IEntrySelection entrySelection,
        IAddinFileCatalog fileCatalog)
    {
        _fileSelection = fileSelection;
        _markupService = markupService;
        _logger = logger;
        _localizationService = localizationService;
        _localizer = localizer;
        _dispatcher = dispatcher;
        _guard = guard;
        _entrySelection = entrySelection;
        _fileCatalog = fileCatalog;
        _fileSelection.PropertyChanged += OnFileSelectionChanged;
        _fileCatalog.Files.CollectionChanged += (_, _) => UpdateDiagnosticSpans();
        _localizationService.LanguageChanged += (_, _) => RefreshSnapshot();
        _guard.Changed += (_, _) => _dispatcher.Invoke(SyncEditLock);
        _entrySelection.PropertyChanged += OnEntrySelectionChanged;
        LoadFrom(_fileSelection.SelectedFile);
        SyncEditLock();
    }

    private void OnFileSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IFileSelection.SelectedFile) or null)
            LoadFrom(((IFileSelection)sender!).SelectedFile);
    }

    private void OnEntrySelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IEntrySelection.SelectedEntry) or null)
            UpdateSelectedEntrySpan();
    }

    private void LoadFrom(AddinFileRowViewModel? row)
    {
        if (IsDirty && row != SelectedFile)
        {
            _logger.LogWarning(
                "Переключение с {From} на {To} с несохранёнными изменениями — правки отброшены",
                SelectedFile?.FileName ?? "-", row?.FileName ?? "-");
        }

        SelectedFile = row;
        _boundFile = row?.File;

        _loading = true;
        try
        {
            ErrorMessage = null;
            RawXml = _boundFile is { } file ? _markupService.ReadRaw(file) : null;
            _savedXml = RawXml;
            IsDirty = false;
            UpdateSelectedEntrySpan();
            UpdateDiagnosticSpans();
        }
        catch (IOException ex)
        {
            RawXml = null;
            _savedXml = null;
            ErrorMessage = _localizer["MarkupError_ReadFailed", ex.Message];
            UpdateSelectedEntrySpan();
            UpdateDiagnosticSpans();
            _logger.LogError(ex, "Не удалось прочитать {FileName}", row?.FileName);
        }
        finally
        {
            _loading = false;
        }
    }

    partial void OnRawXmlChanged(string? value)
    {
        if (_loading)
            return;

        // Срез НЕ пересчитываем: иначе каждое нажатие клавиши заново выделяло бы блок и
        // уводило каретку в его начало. Свежий срез посчитается при следующей смене выбора.
        // Диагностику, наоборот, пересчитываем на каждое нажатие: маркеры не трогают
        // выделение и каретку, а пустые поля и дубли должны подсвечиваться сразу при вводе.
        IsDirty = value != _savedXml;
        ErrorMessage = value is null ? null : _markupService.Validate(value);
        UpdateDiagnosticSpans();
    }

    /// <summary>Можно сохранить: файл выбран, есть несохранённые изменения, текст валиден, Revit закрыт.</summary>
    private bool CanSave() => _boundFile is not null && IsDirty && ErrorMessage is null && !IsLocked;

    /// <summary>
    /// Сохраняет отредактированный XML (атомарно, с <c>.bak</c> — см. <see cref="IAddinMarkupService.Save"/>),
    /// затем перечитывает диск через <see cref="IAddinFileCatalog.Refresh"/> — тот же "disk is the
    /// truth" путь, что и у <see cref="FormViewModel.Save"/>: без этого список, панель записей
    /// и форма продолжали бы показывать манифест, прочитанный до правки в разметке, пока кто-то
    /// не нажмёт "Обновить" вручную.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (_boundFile is not { } file || RawXml is not { } xml)
            return;

        try
        {
            _markupService.Save(file, xml);
            _savedXml = xml;
            IsDirty = false;
            _logger.LogInformation(
                "Save: файл {FileName} сохранён из разметки, {Length} симв.", file.FileName, xml.Length);
            _fileCatalog.Refresh();
        }
        catch (Exception ex) when (ex is AddinManifestFormatException or IOException or RevitRunningException)
        {
            // Не логируем здесь повторно — IAddinMarkupService.Save уже залогировал причину
            // (LogWarning на отклонённой валидации, LogError на сбое записи) в момент, когда
            // она стала известна; здесь только реакция UI (см. тот же принцип у FormViewModel.Save).
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>Можно отменить: есть несохранённые изменения и Revit закрыт.</summary>
    private bool CanDiscard() => IsDirty && !IsLocked;

    /// <summary>Возвращает последний сохранённый текст, отбрасывая правки.</summary>
    [RelayCommand(CanExecute = nameof(CanDiscard))]
    private void Discard()
    {
        _loading = true;
        try
        {
            RawXml = _savedXml;
            ErrorMessage = null;
            IsDirty = false;
        }
        finally
        {
            _loading = false;
        }

        UpdateDiagnosticSpans();
    }

    partial void OnIsDirtyChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        DiscardCommand.NotifyCanExecuteChanged();
        RefreshSnapshot();
    }

    partial void OnIsLockedChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        DiscardCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Разносит <see cref="IRevitProcessGuard.IsRunning"/> на себя.</summary>
    private void SyncEditLock() => IsLocked = _guard.IsRunning;

    /// <summary>
    /// Пересчитывает <see cref="SelectedEntrySpan"/> по текущей записи и тексту. Вызывается
    /// только при смене выбора/файла — не при правке текста, иначе каждое нажатие клавиши
    /// заново выделяло бы блок и уводило каретку.
    /// </summary>
    private void UpdateSelectedEntrySpan()
    {
        SelectedEntrySpan = RawXml is { } xml && _entrySelection.SelectedEntry?.Entry.AddInId is { } id
            ? FindEntrySpan(xml, id)
            : null;
    }

    /// <summary>
    /// Пересчитывает <see cref="DiagnosticSpans"/> по текущему тексту плюс идентификаторам
    /// других файлов той же версии: повтор <c>AddInId</c> подсвечивается, даже если второе
    /// вхождение лежит в другом файле (внутрифайловые дубли — как раньше, кросс-файловые —
    /// новое). Внешнее множество собирается здесь же, чтобы правка текста не требовала
    /// отдельного уведомления от списка.
    /// </summary>
    private void UpdateDiagnosticSpans() => DiagnosticSpans = FindDiagnosticSpans(RawXml, FindExternalIds());

    /// <summary>
    /// Идентификаторы других файлов той же версии, что выбранный (<c>null</c> — файл не выбран).
    /// Дубли считаются только внутри версии: Revit грузит версии независимо.
    /// </summary>
    private HashSet<Guid> FindExternalIds()
    {
        var external = new HashSet<Guid>();
        if (_boundFile is not { } bound)
            return external;

        foreach (var row in _fileCatalog.Files)
        {
            if (row.Version != bound.Version)
                continue;
            if (row.FileName == bound.FileName && row.Scope == bound.Scope)
                continue;
            foreach (var entry in row.File.Manifest.Entries)
                external.Add(entry.AddInId);
        }

        return external;
    }

    /// <summary>
    /// Срезы проблемных мест текста: пустые обязательные элементы, каждое вхождение
    /// повторяющегося значения <c>AddInId</c> внутри текста и каждое вхождение значения из
    /// <paramref name="externalIds"/> (дубль в другом файле той же версии). Пустые невалидные
    /// значения пропускаем в поиске дублей: они уже помечены как пустые. Регистр тегов — точный,
    /// как у парсера (чужой регистр для него — отсутствие элемента, а не пустота).
    /// </summary>
    internal static IReadOnlyList<TextSpan> FindDiagnosticSpans(string? xml, IReadOnlySet<Guid>? externalIds = null)
    {
        if (string.IsNullOrEmpty(xml))
            return [];

        var spans = new List<TextSpan>();
        foreach (Match empty in EmptyFieldPattern().Matches(xml))
            spans.Add(new TextSpan(empty.Index, empty.Length));

        var byId = new Dictionary<string, List<Match>>(StringComparer.OrdinalIgnoreCase);
        foreach (Match candidate in EntryIdPattern().Matches(xml))
        {
            var id = candidate.Groups["id"].Value.Trim();
            if (id.Length == 0)
                continue;

            if (!byId.TryGetValue(id, out var list))
                byId[id] = list = [];
            list.Add(candidate);
        }

        var marked = new HashSet<Match>();
        foreach (var list in byId.Values)
        {
            if (list.Count > 1)
            {
                foreach (var duplicate in list)
                {
                    spans.Add(new TextSpan(duplicate.Index, duplicate.Length));
                    marked.Add(duplicate);
                }
            }
        }

        if (externalIds is { Count: > 0 })
        {
            foreach (var list in byId.Values)
            {
                foreach (var candidate in list)
                {
                    if (marked.Contains(candidate))
                        continue;
                    if (Guid.TryParse(candidate.Groups["id"].Value.Trim(), out var guid) && externalIds.Contains(guid))
                        spans.Add(new TextSpan(candidate.Index, candidate.Length));
                }
            }
        }

        return spans;
    }

    /// <summary>
    /// Срез от открывающего <c>&lt;AddIn</c> до закрывающего <c>&lt;/AddIn&gt;</c> через
    /// <c>AddInId</c> записи. Вложенных <c>AddIn</c> не бывает, первое закрытие после начала —
    /// наше. <see langword="null"/>, если идентификатор или границы блока не нашлись
    /// (пробелы вокруг значения терпим, атрибуты на самом теге <c>AddInId</c> — нет).
    /// </summary>
    private static TextSpan? FindEntrySpan(string xml, Guid addInId)
    {
        Match? idMatch = null;
        foreach (Match candidate in EntryIdPattern().Matches(xml))
        {
            if (Guid.TryParse(candidate.Groups["id"].Value, out var found) && found == addInId)
            {
                idMatch = candidate;
                break;
            }
        }

        if (idMatch is null)
            return null;

        var openMatch = OpenEntryPattern().Match(xml, idMatch.Index);
        if (!openMatch.Success)
            return null;

        const string closeTag = "</AddIn>";
        var closeIndex = xml.IndexOf(closeTag, openMatch.Index, StringComparison.Ordinal);
        if (closeIndex < 0)
            return null;

        return new TextSpan(openMatch.Index, closeIndex + closeTag.Length - openMatch.Index);
    }

    [GeneratedRegex("<AddInId>\\s*(?<id>[^<]*?)\\s*</AddInId>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EntryIdPattern();

    [GeneratedRegex("<(Assembly|FullClassName|AddInId)\\s*(?:/\\s*>|>\\s*</\\1\\s*>)", RegexOptions.CultureInvariant)]
    private static partial Regex EmptyFieldPattern();

    [GeneratedRegex("<AddIn(?=[\\s>])", RegexOptions.RightToLeft | RegexOptions.CultureInvariant)]
    private static partial Regex OpenEntryPattern();

    partial void OnErrorMessageChanged(string? value) => SaveCommand.NotifyCanExecuteChanged();

    partial void OnSelectedFileChanged(AddinFileRowViewModel? value) => RefreshSnapshot();

    private void RefreshSnapshot() => OnPropertyChanged((string?)null);

    /// <summary>Приглашение выбрать файл, когда ничего не выбрано.</summary>
    public string EmptySelectionPrompt => _localizer["EmptySelection_FilePrompt"];

    /// <summary>Кнопка "Отменить".</summary>
    public string DiscardButtonLabel => _localizer["DiscardButton"];

    /// <summary>Кнопка "Сохранить".</summary>
    public string SaveButtonLabel => _localizer["SaveButton"];

    /// <summary>Бейдж несохранённых изменений рядом с именем файла.</summary>
    public string UnsavedBadge => _localizer["Markup_UnsavedBadge"];

    /// <inheritdoc />
    public override string ToString() =>
        $"Markup(File={SelectedFile?.FileName ?? "-"}, Dirty={IsDirty}, Error={ErrorMessage is not null})";
}
