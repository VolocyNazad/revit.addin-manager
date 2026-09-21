using System.IO;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Guard;
using AddinManager.Core.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace AddinManager.Launcher.ViewModels;

/// <summary>Строка списка: один <see cref="AddinFile"/> (план, раздел 6 — "Row").</summary>
public sealed partial class AddinFileRowViewModel : ObservableObject
{
    private readonly IAddinStore _store;
    private readonly ILogger<AddinFileRowViewModel> _logger;
    private readonly IStringLocalizer<AddinFileRowViewModel> _localizer;

    /// <summary>
    /// Текущее состояние файла. Не readonly: <see cref="IAddinStore.SetEnabled"/> возвращает
    /// актуальную запись после переноса (новые <see cref="AddinFile.Enabled"/>/<see cref="AddinFile.FullPath"/>),
    /// и её нужно сохранить сюда — иначе следующий тоггл опирается на устаревший путь к файлу
    /// (баг: второй клик считает файл уже в нужном месте, третий пытается перенести из папки,
    /// откуда он давно уехал, и падает с FileNotFoundException).
    /// </summary>
    private AddinFile _file;

    /// <summary>
    /// Взводится на время программного отката <see cref="IsEnabled"/> после неудачного
    /// <see cref="IAddinStore.SetEnabled"/> — не даёт <see cref="AddinFileRowViewModel.OnIsEnabledChanged(bool)"/> повторно
    /// уйти в перенос файла (откат сам по себе не должен пытаться что-то переносить: он лишь
    /// возвращает чекбокс к состоянию, которое уже совпадает с <see cref="_file"/>.Enabled).
    /// </summary>
    private bool _isReverting;

    /// <summary>Создает строку для файла, уже прочитанного со диска.</summary>
    public AddinFileRowViewModel(
        IAddinStore store,
        AddinFile file,
        ILogger<AddinFileRowViewModel> logger,
        IStringLocalizer<AddinFileRowViewModel> localizer)
    {
        _store = store;
        _file = file;
        _logger = logger;
        _localizer = localizer;
        _isEnabled = file.Enabled;
        UpdateWarnings(new HashSet<Guid>());
    }

    /// <summary>Toggle строки: файл в корне версии (true) или в <c>disabled/</c> (false).</summary>
    [ObservableProperty]
    private bool _isEnabled;

    /// <summary>Revit запущен — тоггл гаснет (выставляет <see cref="ListViewModel"/>, см. его SyncEditLock).</summary>
    [ObservableProperty]
    private bool _isLocked;

    /// <summary>
    /// В файле есть проблемы (пустые обязательные поля, неизвестный тип, дубли <c>AddInId</c>
    /// внутри файла или в других файлах той же версии Revit) — вид показывает иконку
    /// предупреждения с <see cref="WarningMessage"/> в тултипе. Пересчитывается в конструкторе
    /// (внутрифайловое) и в <see cref="UpdateWarnings"/> (плюс кросс-файловые дубли от списка).
    /// </summary>
    [ObservableProperty]
    private bool _hasWarning;

    /// <summary>Причины <see cref="HasWarning"/> человеческим языком; <c>null</c>, когда всё чисто.</summary>
    [ObservableProperty]
    private string? _warningMessage;

    /// <summary>Заголовок карточки-тултипа иконки предупреждения.</summary>
    public string WarningTooltipTitle => _localizer["WarningTooltipTitle"];

    /// <summary>Просьба удалить файл — обрабатывает <see cref="ListViewModel"/> (подтверждение, удаление, обновление).</summary>
    public event EventHandler? DeleteRequested;

    /// <summary>Крестик строки: просит список удалить файл.</summary>
    [RelayCommand(CanExecute = nameof(CanDelete))]
    public void Delete() => DeleteRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Тултип крестика.</summary>
    public string DeleteButtonTooltip => _localizer["DeleteButtonTooltip"];

    private bool CanDelete() => !IsLocked;

    /// <summary>
    /// Сообщение о последней неудачной попытке переключить <see cref="IsEnabled"/> — например,
    /// нет прав на запись в область Machine (см. <see cref="AddinFileRowViewModel.OnIsEnabledChanged(bool)"/>). <c>null</c>,
    /// когда последняя попытка (или ещё ни одной не было) прошла успешно — тот же паттерн
    /// "видимость по null/empty", что и у <c>ErrorMessage</c> в <c>FormViewModel</c>/
    /// <c>MarkupViewModel</c> (другая сборка того же слоя, отсюда без <c>cref</c>), только
    /// здесь ошибка привязана к конкретной строке списка, а не к целой панели.
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    partial void OnIsLockedChanged(bool value) => DeleteCommand.NotifyCanExecuteChanged();

    partial void OnIsEnabledChanged(bool value)
    {
        if (_isReverting)
        {
            _logger.LogDebug(
                "Toggle {FileName} ({Scope}, {Version}): программный откат до IsEnabled={Value}, SetEnabled не вызываем",
                _file.FileName, _file.Scope, _file.Version, value);
            return;
        }

        _logger.LogInformation(
            "Toggle {FileName} ({Scope}, {Version}): пользователь выставил IsEnabled={Value}",
            _file.FileName, _file.Scope, _file.Version, value);

        try
        {
            _file = _store.SetEnabled(_file, value);
            ErrorMessage = null;
            _logger.LogInformation(
                "Toggle {FileName} ({Scope}, {Version}): успешно, теперь IsEnabled={Value}",
                _file.FileName, _file.Scope, _file.Version, value);
        }
        catch (IOException ex)
        {
            _logger.LogError(
                ex,
                "Toggle {FileName} ({Scope}, {Version}): не удалось выставить IsEnabled={Value}, откатываем чекбокс",
                _file.FileName, _file.Scope, _file.Version, value);

            ErrorMessage = BuildErrorMessage(ex, _file.FileName);
            RevertToggle(value);
        }
        catch (RevitRunningException ex)
        {
            _logger.LogWarning(
                ex,
                "Toggle {FileName} ({Scope}, {Version}): Revit запущен, перенос запрещён, откатываем чекбокс",
                _file.FileName, _file.Scope, _file.Version, value);

            ErrorMessage = ex.Message;
            RevertToggle(value);
        }
    }

    /// <summary>
    /// Возвращает чекбокс к состоянию файла синхронно, чтобы он не показывал состояние,
    /// которого на самом деле нет на диске. Флаг отката гасит рекурсивный заход в обработчик.
    /// </summary>
    private void RevertToggle(bool value)
    {
        // _file не менялся (SetEnabled бросает до возврата новой записи) — откат IsEnabled
        // обратно к _file.Enabled синхронно. _isReverting гасит рекурсивный заход в этот же метод.
        _isReverting = true;
        try
        {
            IsEnabled = !value;
        }
        finally
        {
            _isReverting = false;
        }
    }

    /// <summary>
    /// Разворачивает <see cref="IAddinStore.SetEnabled"/>'s <see cref="IOException"/> в
    /// понятный пользователю текст: если внутри лежит <see cref="UnauthorizedAccessException"/>
    /// (типичная причина — область Machine под %PROGRAMDATA%, куда обычный пользователь без
    /// прав администратора писать не может), подсказываем конкретное решение вместо голого
    /// "Access to the path is denied.".
    /// </summary>
    private string BuildErrorMessage(IOException ex, string fileName) =>
        ex.InnerException is UnauthorizedAccessException
            ? _localizer["ToggleNoPermission", fileName]
            : ex.Message;

    /// <summary>
    /// Пересчитывает <see cref="HasWarning"/>/<see cref="WarningMessage"/>: внутрифайловые проблемы
    /// (нет записей, пустые <c>Assembly</c>/<c>FullClassName</c>, неизвестный тип, дубли <c>AddInId</c>)
    /// плюс кросс-файловые дубли — <paramref name="duplicatedInVersion"/> содержит идентификаторы,
    /// встречающиеся минимум в двух файлах этой версии Revit (считает <see cref="ListViewModel"/>).
    /// </summary>
    /// <param name="duplicatedInVersion">Дублирующиеся в версии идентификаторы.</param>
    public void UpdateWarnings(IReadOnlySet<Guid> duplicatedInVersion)
    {
        var reasons = new List<string>();
        var entries = _file.Manifest.Entries;

        if (entries.Count == 0)
            reasons.Add(_localizer["WarningNoEntries"]);

        if (entries.Any(e => string.IsNullOrWhiteSpace(e.AssemblyPath) || string.IsNullOrWhiteSpace(e.FullClassName)))
            reasons.Add(_localizer["WarningEmptyFields"]);

        if (entries.Any(e => e.Type == Core.Manifests.AddinEntryType.Unknown))
            reasons.Add(_localizer["WarningUnknownType"]);

        if (entries.GroupBy(e => e.AddInId).Any(group => group.Count() > 1))
            reasons.Add(_localizer["WarningDuplicateInFile"]);

        if (entries.Any(e => duplicatedInVersion.Contains(e.AddInId)))
            reasons.Add(_localizer["WarningDuplicateAcrossFiles", _file.Version]);

        WarningMessage = reasons.Count == 0 ? null : string.Join("; ", reasons);
        HasWarning = reasons.Count > 0;
    }

    /// <summary>
    /// Актуальная запись файла — нужна подпанели разметки для чтения/валидации/сохранения сырого XML
    /// (<see cref="IAddinMarkupService"/> принимает <see cref="AddinFile"/>, а не голый путь).
    /// </summary>
    public AddinFile File => _file;

    /// <summary>Имя файла — заголовок строки.</summary>
    public string FileName => _file.FileName;

    /// <summary>Полный путь на диске — вторая строка карточки-тултипа строки списка.</summary>
    public string FullPath => _file.FullPath;

    /// <summary>Карточка-тултип: подпись "Путь".</summary>
    public string FilePathLabel => _localizer["FileTooltip_PathLabel"];

    /// <summary>Карточка-тултип: подпись "Версия".</summary>
    public string FileVersionLabel => _localizer["FileTooltip_VersionLabel"];

    /// <summary>Карточка-тултип: подпись "Область".</summary>
    public string FileScopeLabel => _localizer["FileTooltip_ScopeLabel"];

    /// <summary>Карточка-тултип: подпись "Тип".</summary>
    public string FileTypeLabel => _localizer["FileTooltip_TypeLabel"];

    /// <summary>Карточка-тултип: подпись "Вендор".</summary>
    public string FileVendorLabel => _localizer["FileTooltip_VendorLabel"];

    /// <summary>Карточка-тултип: заголовок "Содержимое".</summary>
    public string FileContentsHeader => _localizer["FileTooltip_ContentsHeader"];

    /// <summary>User/Machine — входит в <see cref="MetaLine"/>, ось фильтра и группировки в списке.</summary>
    public AddinScope Scope => _file.Scope;

    /// <summary>Версия Revit ("2021".."2027") — входит в <see cref="MetaLine"/>, ось фильтра и группировки в списке.</summary>
    public string Version => _file.Version;

    /// <summary>Записей в файле; &gt;1 добавляет "N entries" в <see cref="MetaLine"/>.</summary>
    public int EntryCount => _file.Manifest.Entries.Count;

    /// <summary>Саммари типов записей вида "Application + Command" — никогда "mixed" (план, раздел 6).</summary>
    public string TypeSummary => string.Join(" + ", _file.Manifest.Entries.Select(e => e.Type).Distinct());

    /// <summary>Строка метаданных под именем файла: "Version · Scope · TypeSummary[ · N entries]".</summary>
    public string MetaLine
    {
        get
        {
            var parts = new List<string> { Version, Scope.ToString(), TypeSummary };
            if (EntryCount > 1)
                parts.Add(EntriesCountText(EntryCount));

            return string.Join(" · ", parts);
        }
    }

    /// <summary>
    /// "N entries" с учётом плюрализации: английскому хватает two forms, русскому нужны три
    /// (1 запись, 3 записи, 5 записей) — ключ выбирается по числу, а не по языку.
    /// </summary>
    private string EntriesCountText(int count)
    {
        string key;
        if (count % 10 == 1 && count % 100 != 11)
            key = "FileMeta_EntriesOne";
        else if (count % 10 is >= 2 and <= 4 && (count % 100 < 12 || count % 100 > 14))
            key = "FileMeta_EntriesFew";
        else
            key = "FileMeta_EntriesMany";

        return _localizer[key, count];
    }

    /// <summary>
    /// Подзаголовок "Vendor • VendorDescription", когда у ВСЕХ записей файла один и тот же
    /// вендор (VendorId и VendorDescription совпадают у каждой) — <c>"—"</c>, когда записей
    /// несколько и вендор у них расходится, и <c>null</c>, когда вендор нигде не задан.
    /// В отличие от <see cref="TypeSummary"/> (никогда "mixed", всегда честный список через
    /// " + "), для вендора нет смысла перечислять все варианты в одной строке списка — это
    /// метаданные производителя плагина, обычно одни на файл, поэтому расхождение просто
    /// помечается прочерком, а не разворачивается.
    /// </summary>
    public string? VendorSubtitle
    {
        get
        {
            var entries = _file.Manifest.Entries;
            if (entries.Count == 0)
                return null;

            var first = entries[0];
            var allSame = entries.All(e => e.VendorId == first.VendorId && e.VendorDescription == first.VendorDescription);
            if (!allSame)
                return "—";

            var parts = new[] { first.VendorId, first.VendorDescription }.Where(p => !string.IsNullOrWhiteSpace(p));
            var joined = string.Join(" • ", parts);
            return joined.Length == 0 ? null : joined;
        }
    }

    /// <summary>
    /// Строки "Type — DisplayName" по каждой записи файла (тот же расчёт отображаемого
    /// имени, что и у <see cref="AddinEntryRowViewModel.DisplayName"/>) — для карточки-тултипа
    /// строки списка, не для основного отображения (там это подпанель записей).
    /// </summary>
    public IEnumerable<string> EntrySummaries =>
        _file.Manifest.Entries
            .Select((e, i) => $"{e.Type} — {e.Name ?? e.Text ?? _localizer["EntryFallbackName", i + 1]}");

    /// <inheritdoc />
    public override string ToString() =>
        $"File({FileName}, {Version}, {Scope}, Enabled={IsEnabled}, Entries={EntryCount})";
}
