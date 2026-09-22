using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using AddinManager.Core.Abstractions.Guard;
using AddinManager.Core.Abstractions.Manifests;
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
using Microsoft.Extensions.Logging;

namespace AddinManager.Launcher.ViewModels;

/// <summary>
/// Подпанель формы: структурное редактирование одной записи AddIn, выбранной в
/// <see cref="EntriesViewModel"/> (план, раздел 6 — "the active entry form covers the full
/// .addin schema"). Какие поля показывать, решает <see cref="IManifestSchema"/> по паре
/// (версия файла, <see cref="EntryType"/>) — план, раздел 6: "Foreign-Type fields are fully
/// hidden (the form rebuilds per Type, no disabling)"; версийное ограничение в нашем
/// поддерживаемом диапазоне 2021-2027 на практике не встречается нигде, кроме файлового
/// ManifestSettings (см. <see cref="RevitManifestSchema"/>) — тот в эту подпанель не входит, он
/// "above the entry tabs" по плану: своя подпанель и режим (<see cref="ManifestSettingsViewModel"/>,
/// <see cref="EditorMode.Settings"/>), а не часть формы одной записи.
/// </summary>
public sealed partial class FormViewModel : ObservableObject
{
    private readonly IEntrySelection _entrySelection;
    private readonly IFileSelection _fileSelection;
    private readonly IAddinFileCatalog _fileCatalog;
    private readonly IAddinManifestParser _parser;
    private readonly IAddinMarkupService _markupService;
    private readonly IManifestSchema _schema;
    private readonly ILogger<FormViewModel> _logger;
    private readonly ILocalizationService _localizationService;
    private readonly IStringLocalizer<FormViewModel> _localizer;
    private readonly ISelectableOptionViewModelFactory _optionFactory;
    private readonly IUiDispatcher _dispatcher;
    private readonly IRevitProcessGuard _guard;

    private static readonly IReadOnlySet<AddinEntryField> NoFields = new HashSet<AddinEntryField>();

    private AddinEntry? _originalEntry;
    private bool _loading;

    /// <summary>
    /// Создает подпанель и сразу строит форму по тому, что уже выбрано в <see cref="IEntrySelection"/>.
    /// </summary>
    /// <param name="entrySelection">
    /// Выбор записи — источник выбранной записи. Прямая зависимость только на узкий
    /// контракт, а не на зону целиком (docs/architecture.md, раздел MVVM).
    /// </param>
    /// <param name="fileSelection">Выбор файла — источник файла формы.</param>
    /// <param name="fileCatalog">
    /// Каталог файлов — после успешного сохранения запускает <see cref="IAddinFileCatalog.Refresh"/>,
    /// чтобы список, записи и разметка перечитали файл с диска и не разошлись с тем, что форма
    /// только что записала (та же простота "diсk is the truth", что и остальной проект — см.
    /// docs/architecture.md, раздел "Form zone").
    /// </param>
    /// <param name="parser">Сериализация отредактированной записи обратно в XML файла.</param>
    /// <param name="markupService">Атомарная запись + повторная валидация всего файла.</param>
    /// <param name="schema">Таблица версия×тип: какие поля показывать.</param>
    /// <param name="logger">Логгер подпанели.</param>
    /// <param name="localizationService">Сервис языка — смена языка перечитывает подписи через <see cref="RefreshSnapshot"/>.</param>
    /// <param name="localizer">Строки подпанели и её ошибок.</param>
    /// <param name="optionFactory">Создание пунктов мульти-наборов (значения известны только из схемы).</param>
    /// <param name="dispatcher">Маршалинг событий сторожа в поток UI.</param>
    /// <param name="guard">Сторож запущенного Revit — форма гаснет, пока он жив.</param>
    public FormViewModel(
        IEntrySelection entrySelection,
        IFileSelection fileSelection,
        IAddinFileCatalog fileCatalog,
        IAddinManifestParser parser,
        IAddinMarkupService markupService,
        IManifestSchema schema,
        ILogger<FormViewModel> logger,
        ILocalizationService localizationService,
        IStringLocalizer<FormViewModel> localizer,
        ISelectableOptionViewModelFactory optionFactory,
        IUiDispatcher dispatcher,
        IRevitProcessGuard guard)
    {
        _entrySelection = entrySelection;
        _fileSelection = fileSelection;
        _fileCatalog = fileCatalog;
        _parser = parser;
        _markupService = markupService;
        _schema = schema;
        _logger = logger;
        _localizationService = localizationService;
        _localizer = localizer;
        _optionFactory = optionFactory;
        _dispatcher = dispatcher;
        _guard = guard;

        _entrySelection.PropertyChanged += OnSelectionChanged;
        _fileSelection.PropertyChanged += OnSelectionChanged;
        _localizationService.LanguageChanged += (_, _) => RefreshSnapshot();
        _guard.Changed += (_, _) => _dispatcher.Invoke(SyncEditLock);
        LoadFrom(_fileSelection.SelectedFile, _entrySelection.SelectedEntry);
        SyncEditLock();
    }

    [ObservableProperty]
    private AddinFileRowViewModel? _selectedFile;

    [ObservableProperty]
    private AddinEntryRowViewModel? _selectedEntry;

    [ObservableProperty]
    private AddinEntryType _entryType;

    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
    private string? _text;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _longDescription;

    [ObservableProperty]
    private string? _assemblyPath;

    [ObservableProperty]
    private string? _addInIdText;

    [ObservableProperty]
    private string? _fullClassName;

    [ObservableProperty]
    private string? _availabilityClassName;

    [ObservableProperty]
    private string? _vendorId;

    [ObservableProperty]
    private string? _vendorDescription;

    [ObservableProperty]
    private string? _largeImage;

    [ObservableProperty]
    private string? _smallImage;

    [ObservableProperty]
    private string? _toolTipImage;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Revit запущен — поля и кнопки гаснут, сохранение запрещено.</summary>
    [ObservableProperty]
    private bool _isLocked;

    /// <summary>Чипы VisibilityMode — видимы только когда <see cref="ShowVisibilityMode"/>.</summary>
    public ObservableCollection<SelectableOptionViewModel> VisibilityModeOptions { get; } = [];

    /// <summary>Чипы Discipline — видимы только когда <see cref="ShowDiscipline"/>.</summary>
    public ObservableCollection<SelectableOptionViewModel> DisciplineOptions { get; } = [];

    private IReadOnlySet<AddinEntryField> CurrentFields =>
        SelectedFile is { Version: { } version } ? _schema.Fields(version, EntryType) : NoFields;

    /// <summary>Application/DBApplication — показывать поле "Имя".</summary>
    public bool ShowName => CurrentFields.Contains(AddinEntryField.Name);

    /// <summary>Command — показывать поле "Подпись кнопки".</summary>
    public bool ShowText => CurrentFields.Contains(AddinEntryField.Text);

    /// <summary>Command — показывать краткое описание.</summary>
    public bool ShowDescription => CurrentFields.Contains(AddinEntryField.Description);

    /// <summary>Command — показывать расширенное описание.</summary>
    public bool ShowLongDescription => CurrentFields.Contains(AddinEntryField.LongDescription);

    /// <summary>Command — показывать класс доступности.</summary>
    public bool ShowAvailabilityClassName => CurrentFields.Contains(AddinEntryField.AvailabilityClassName);

    /// <summary>Command — показывать чипы VisibilityMode.</summary>
    public bool ShowVisibilityMode => CurrentFields.Contains(AddinEntryField.VisibilityMode);

    /// <summary>Command — показывать чипы Discipline.</summary>
    public bool ShowDiscipline => CurrentFields.Contains(AddinEntryField.Discipline);

    /// <summary>Command — показывать поле иконки External Tools.</summary>
    public bool ShowLargeImage => CurrentFields.Contains(AddinEntryField.LargeImage);

    /// <summary>Command — показывать поле иконки панели быстрого доступа.</summary>
    public bool ShowSmallImage => CurrentFields.Contains(AddinEntryField.SmallImage);

    /// <summary>Command — показывать поле изображения расширенного тултипа.</summary>
    public bool ShowToolTipImage => CurrentFields.Contains(AddinEntryField.ToolTipImage);

    /// <summary>Инлайн-ошибка поля Assembly: пустое значение блокирует сохранение.</summary>
    public string? AssemblyPathError =>
        string.IsNullOrWhiteSpace(AssemblyPath) ? _localizer["AssemblyPathRequired"].Value : null;

    /// <summary>Инлайн-ошибка поля AddInId: не GUID — блокирует сохранение.</summary>
    public string? AddInIdError =>
        Guid.TryParse(AddInIdText?.Trim(), out _) ? null : _localizer["FormError_InvalidAddInId", AddInIdText ?? string.Empty].Value;

    /// <summary>Инлайн-ошибка поля FullClassName: пустое значение блокирует сохранение.</summary>
    public string? FullClassNameError =>
        string.IsNullOrWhiteSpace(FullClassName) ? _localizer["FullClassNameRequired"].Value : null;

    private void OnSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IFileSelection.SelectedFile) or nameof(IEntrySelection.SelectedEntry) or null)
            LoadFrom(_fileSelection.SelectedFile, _entrySelection.SelectedEntry);
    }

    private void LoadFrom(AddinFileRowViewModel? file, AddinEntryRowViewModel? entryRow)
    {
        if (IsDirty && entryRow?.Entry.AddInId != _originalEntry?.AddInId)
        {
            _logger.LogWarning(
                "Переключение записи с несохранёнными изменениями формы ({From} → {To}) — правки отброшены",
                _originalEntry?.AddInId.ToString() ?? "-", entryRow?.Entry.AddInId.ToString() ?? "-");
        }

        SelectedFile = file;
        SelectedEntry = entryRow;
        _originalEntry = entryRow?.Entry;

        _loading = true;
        try
        {
            ErrorMessage = null;

            ApplyEntry(_originalEntry);
            IsDirty = false;
        }
        finally
        {
            _loading = false;
        }

        RefreshValidation();
    }

    private void ApplyEntry(AddinEntry? entry)
    {
        EntryType = entry?.Type ?? AddinEntryType.Application;
        Name = entry?.Name;
        Text = entry?.Text;
        Description = entry?.Description;
        LongDescription = entry?.LongDescription;
        AssemblyPath = entry?.AssemblyPath;
        AddInIdText = entry?.AddInId.ToString();
        FullClassName = entry?.FullClassName;
        AvailabilityClassName = entry?.AvailabilityClassName;
        VendorId = entry?.VendorId;
        VendorDescription = entry?.VendorDescription;
        LargeImage = entry?.LargeImage;
        SmallImage = entry?.SmallImage;
        ToolTipImage = entry?.ToolTipImage;

        ApplyOptions(VisibilityModeOptions, _schema.VisibilityModeValues, entry?.VisibilityModes);
        ApplyOptions(DisciplineOptions, _schema.DisciplineValues, entry?.Disciplines);
    }

    private void ApplyOptions(
        ObservableCollection<SelectableOptionViewModel> target,
        IReadOnlyList<string> known,
        IReadOnlyList<string>? selected)
    {
        foreach (var existing in target)
            existing.PropertyChanged -= OnOptionChanged;
        target.Clear();

        foreach (var value in known)
        {
            var option = _optionFactory.Create(value);
            option.IsSelected = selected?.Contains(value) ?? false;
            option.IsLocked = IsLocked;
            option.PropertyChanged += OnOptionChanged;
            target.Add(option);
        }
    }

    private void OnOptionChanged(object? sender, PropertyChangedEventArgs e) => MarkDirty();

    private void MarkDirty()
    {
        if (_loading)
            return;

        IsDirty = true;
        ErrorMessage = null;
        RefreshValidation();
    }

    /// <summary>Перечитывает инлайн-ошибки полей и доступность сохранения.</summary>
    private void RefreshValidation()
    {
        OnPropertyChanged(nameof(AssemblyPathError));
        OnPropertyChanged(nameof(AddInIdError));
        OnPropertyChanged(nameof(FullClassNameError));
        SaveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Заполняет <see cref="AddInIdText"/> новым GUID (план, раздел 6 — "AddInId* (GUID + generate button)").</summary>
    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private void GenerateAddInId() => AddInIdText = Guid.NewGuid().ToString();

    private bool CanGenerate() => !IsLocked;

    private bool CanSave() => SelectedFile is not null && _originalEntry is not null && IsDirty && ErrorMessage is null && !IsLocked
        && AssemblyPathError is null && AddInIdError is null && FullClassNameError is null;

    /// <summary>
    /// Собирает отредактированную запись, подставляет её в манифест файла (по <c>AddInId</c>
    /// исходной записи) и сохраняет весь файл через <see cref="IAddinMarkupService.Save"/> —
    /// тот же атомарный путь (temp + <c>.bak</c>) и та же повторная структурная валидация
    /// (включая уникальность <c>AddInId</c> по всему файлу), что и у Markup-подпанели, вместо
    /// отдельного, параллельного пути записи только одной записи.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (SelectedFile?.File is not { } file || _originalEntry is not { } original)
            return;

        if (!Guid.TryParse(AddInIdText?.Trim(), out var addInId))
        {
            ErrorMessage = _localizer["FormError_InvalidAddInId", AddInIdText ?? string.Empty];
            _logger.LogWarning("Save: отклонено — некорректный AddInId '{AddInIdText}'", AddInIdText);
            return;
        }

        if (string.IsNullOrWhiteSpace(AssemblyPath) || string.IsNullOrWhiteSpace(FullClassName))
        {
            ErrorMessage = _localizer["FormError_AssemblyAndClassRequired"];
            _logger.LogWarning(
                "Save: отклонено — Assembly/FullClassName не заданы (файл {FileName})",
                SelectedFile?.File.FileName);
            return;
        }

        var fields = CurrentFields;

        // Тип записи в форме не меняется — RawType (точное написание Type= из исходного
        // файла) всегда переживает сохранение как есть (см. LinqToXmlAddinManifestParser.WriteEntry:
        // RawType побеждает Type).
        var updated = original with
        {
            Type = EntryType,
            RawType = original.RawType,
            Name = fields.Contains(AddinEntryField.Name) ? Name : null,
            Text = fields.Contains(AddinEntryField.Text) ? Text : null,
            Description = fields.Contains(AddinEntryField.Description) ? Description : null,
            LongDescription = fields.Contains(AddinEntryField.LongDescription) ? LongDescription : null,
            AssemblyPath = AssemblyPath,
            AddInId = addInId,
            FullClassName = FullClassName,
            AvailabilityClassName = fields.Contains(AddinEntryField.AvailabilityClassName) ? AvailabilityClassName : null,
            VendorId = VendorId,
            VendorDescription = VendorDescription,
            VisibilityModes = fields.Contains(AddinEntryField.VisibilityMode) ? SelectedValues(VisibilityModeOptions) : [],
            Disciplines = fields.Contains(AddinEntryField.Discipline) ? SelectedValues(DisciplineOptions) : [],
            LargeImage = fields.Contains(AddinEntryField.LargeImage) ? LargeImage : null,
            SmallImage = fields.Contains(AddinEntryField.SmallImage) ? SmallImage : null,
            ToolTipImage = fields.Contains(AddinEntryField.ToolTipImage) ? ToolTipImage : null,
        };

        var entries = file.Manifest.Entries.Select(e => e.AddInId == original.AddInId ? updated : e).ToList();
        // Несохранённая новая запись: в манифесте её ещё нет — дописываем.
        if (!file.Manifest.Entries.Any(e => e.AddInId == original.AddInId))
            entries.Add(updated);
        var manifest = file.Manifest with { Entries = entries };
        var xml = _parser.ToXml(manifest);

        try
        {
            _markupService.Save(file, xml);
            _logger.LogInformation(
                "Save: запись {AddInId} файла {FileName} сохранена", addInId, file.FileName);
            _fileCatalog.Refresh();
        }
        catch (Exception ex) when (ex is AddinManifestFormatException or IOException or RevitRunningException)
        {
            // Не логируем здесь повторно — см. тот же комментарий в MarkupViewModel.Save:
            // IAddinMarkupService.Save уже залогировал причину сбоя.
            ErrorMessage = ex.Message;
        }
    }

    private static IReadOnlyList<string> SelectedValues(IEnumerable<SelectableOptionViewModel> options) =>
        options.Where(o => o.IsSelected).Select(o => o.Value).ToList();

    private bool CanDiscard() => IsDirty && !IsLocked;

    /// <summary>Возвращает форму к последней сохранённой записи, отбрасывая правки.</summary>
    [RelayCommand(CanExecute = nameof(CanDiscard))]
    private void Discard()
    {
        _loading = true;
        try
        {
            ErrorMessage = null;
            ApplyEntry(_originalEntry);
            IsDirty = false;
        }
        finally
        {
            _loading = false;
        }

        RefreshValidation();
    }

    partial void OnEntryTypeChanged(AddinEntryType value) => RefreshSnapshot();

    partial void OnNameChanged(string? value) => MarkDirty();

    partial void OnTextChanged(string? value) => MarkDirty();

    partial void OnDescriptionChanged(string? value) => MarkDirty();

    partial void OnLongDescriptionChanged(string? value) => MarkDirty();

    partial void OnAssemblyPathChanged(string? value) => MarkDirty();

    partial void OnAddInIdTextChanged(string? value) => MarkDirty();

    partial void OnFullClassNameChanged(string? value) => MarkDirty();

    partial void OnAvailabilityClassNameChanged(string? value) => MarkDirty();

    partial void OnVendorIdChanged(string? value) => MarkDirty();

    partial void OnVendorDescriptionChanged(string? value) => MarkDirty();

    partial void OnLargeImageChanged(string? value) => MarkDirty();

    partial void OnSmallImageChanged(string? value) => MarkDirty();

    partial void OnToolTipImageChanged(string? value) => MarkDirty();

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
        GenerateAddInIdCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Разносит <see cref="IRevitProcessGuard.IsRunning"/> по себе и пунктам.</summary>
    private void SyncEditLock()
    {
        IsLocked = _guard.IsRunning;
        foreach (var option in VisibilityModeOptions.Concat(DisciplineOptions))
            option.IsLocked = IsLocked;
    }

    partial void OnErrorMessageChanged(string? value) => SaveCommand.NotifyCanExecuteChanged();

    private void RefreshSnapshot() => OnPropertyChanged((string?)null);

    /// <summary>Приглашение выбрать запись, когда ничего не выбрано.</summary>
    public string EmptySelectionPrompt => _localizer["Form_EmptySelectionPrompt"];

    /// <summary>Кнопка "Отменить".</summary>
    public string DiscardButtonLabel => _localizer["DiscardButton"];

    /// <summary>Кнопка "Сохранить".</summary>
    public string SaveButtonLabel => _localizer["SaveButton"];

    /// <summary>Подпись "Тип записи *". Имена типов/тегов — технические токены, не переводятся.</summary>
    public string EntryTypeLabel => _localizer["Form_EntryTypeLabel"];

    /// <summary>Подпись "Имя *".</summary>
    public string NameLabel => _localizer["Form_NameLabel"];

    /// <summary>Подпись "Подпись кнопки *".</summary>
    public string ButtonTextLabel => _localizer["Form_ButtonTextLabel"];

    /// <summary>Подпись "Описание".</summary>
    public string DescriptionLabel => _localizer["Form_DescriptionLabel"];

    /// <summary>Подпись "Расширенное описание".</summary>
    public string LongDescriptionLabel => _localizer["Form_LongDescriptionLabel"];

    /// <summary>Кнопка "Сгенерировать".</summary>
    public string GenerateButtonLabel => _localizer["GenerateAddInIdButton"];

    /// <summary>Подпись "Режимы видимости".</summary>
    public string VisibilityModesLabel => _localizer["Form_VisibilityModesLabel"];

    /// <summary>Подпись "Дисциплины".</summary>
    public string DisciplinesLabel => _localizer["Form_DisciplinesLabel"];

    /// <inheritdoc />
    public override string ToString() =>
        $"Form(File={SelectedFile?.FileName ?? "-"}, Entry={_originalEntry?.AddInId.ToString() ?? "-"}, Type={EntryType}, Dirty={IsDirty})";
}
