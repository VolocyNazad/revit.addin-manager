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
/// Файловая (не по-записи) подпанель редактора: блок <see cref="ManifestSettings"/> —
/// <c>UseRevitContext</c>/<c>ContextName</c> (план, раздел 6 — секция "Isolation", "above the
/// entry tabs"). В отличие от <see cref="FormViewModel"/> (одна запись) правит файл целиком,
/// поэтому читает выбор файла через <see cref="IFileSelection"/>, а не через
/// <see cref="IEntrySelection"/> — те же узкие контракты зон, что у остальных подпанели
/// (docs/architecture.md, раздел MVVM).
/// </summary>
public sealed partial class ManifestSettingsViewModel : ObservableObject
{
    private readonly IFileSelection _fileSelection;
    private readonly IAddinFileCatalog _fileCatalog;
    private readonly IAddinManifestParser _parser;
    private readonly IAddinMarkupService _markupService;
    private readonly IManifestSchema _schema;
    private readonly ILogger<ManifestSettingsViewModel> _logger;
    private readonly ILocalizationService _localizationService;
    private readonly IStringLocalizer<ManifestSettingsViewModel> _localizer;
    private readonly IUiDispatcher _dispatcher;
    private readonly IRevitProcessGuard _guard;

    private ManifestSettings? _originalSettings;
    private bool _loading;

    /// <summary>Создает подпанель и сразу читает блок настроек уже выбранного файла.</summary>
    /// <param name="fileSelection">Выбор файла — источник подпанели.</param>
    /// <param name="parser">Сериализация отредактированного блока обратно в XML файла.</param>
    /// <param name="markupService">Атомарная запись + повторная валидация всего файла.</param>
    /// <param name="schema">Даёт знать, поддерживает ли версия файла <see cref="ManifestSettings"/> (2026+).</param>
    /// <param name="logger">Логгер подпанели.</param>
    /// <param name="localizationService">Сервис языка — смена языка перечитывает подписи через <see cref="RefreshSnapshot"/>.</param>
    /// <param name="localizer">Строки подпанели.</param>
    /// <param name="dispatcher">Маршалинг событий сторожа в поток UI.</param>
    /// <param name="guard">Сторож запущенного Revit — подпанель гаснет, пока он жив.</param>
    /// <param name="fileCatalog">Каталог файлов — обновление списка после сохранения.</param>
    public ManifestSettingsViewModel(
        IFileSelection fileSelection,
        IAddinManifestParser parser,
        IAddinMarkupService markupService,
        IManifestSchema schema,
        ILogger<ManifestSettingsViewModel> logger,
        ILocalizationService localizationService,
        IStringLocalizer<ManifestSettingsViewModel> localizer,
        IUiDispatcher dispatcher,
        IRevitProcessGuard guard,
        IAddinFileCatalog fileCatalog)
    {
        _fileSelection = fileSelection;
        _parser = parser;
        _markupService = markupService;
        _schema = schema;
        _logger = logger;
        _localizationService = localizationService;
        _localizer = localizer;
        _dispatcher = dispatcher;
        _guard = guard;
        _fileCatalog = fileCatalog;

        _fileSelection.PropertyChanged += OnFileSelectionChanged;
        _localizationService.LanguageChanged += (_, _) => RefreshSnapshot();
        _guard.Changed += (_, _) => _dispatcher.Invoke(SyncEditLock);
        LoadFrom(_fileSelection.SelectedFile);
        SyncEditLock();
    }

    [ObservableProperty]
    private AddinFileRowViewModel? _selectedFile;

    /// <summary><see langword="null"/> — тег отсутствует; <see langword="true"/>/<see langword="false"/> — его значение.</summary>
    [ObservableProperty]
    private bool? _useRevitContext;

    [ObservableProperty]
    private string? _contextName;

    [ObservableProperty]
    private bool _isDirty;

    /// <summary>Revit запущен — поля и кнопки гаснут, сохранение запрещено.</summary>
    [ObservableProperty]
    private bool _isLocked;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// <see cref="ManifestSettings"/> — часть схемы только с 2026 версии (см. <see cref="RevitManifestSchema"/>,
    /// исследование в комментарии там же); для более старых версий подпанель показывает
    /// пояснение вместо полей и блокирует сохранение.
    /// </summary>
    public bool IsSupported => SelectedFile is { Version: { } version } && _schema.SupportsManifestSettings(version);

    private void OnFileSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IFileSelection.SelectedFile) or null)
            LoadFrom(_fileSelection.SelectedFile);
    }

    private void LoadFrom(AddinFileRowViewModel? file)
    {
        SelectedFile = file;
        _originalSettings = file?.File.Manifest.Settings;

        _loading = true;
        try
        {
            ErrorMessage = null;
            UseRevitContext = _originalSettings?.UseRevitContext;
            ContextName = _originalSettings?.ContextName;
            IsDirty = false;
        }
        finally
        {
            _loading = false;
        }

        RefreshSnapshot();
    }

    /// <summary>
    /// Переключает <see cref="UseRevitContext"/> чипами "Не задано"/"Да"/"Нет" — теми же
    /// маркерами (<c>"Unset"</c>/<c>"True"</c>/<c>"False"</c>), что читает
    /// <see cref="Converters.NullableBoolStateToOpacityConverter"/> для подсветки активного.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanChange))]
    private void SetUseRevitContext(string state) =>
        UseRevitContext = state switch
        {
            "True" => true,
            "False" => false,
            _ => null,
        };

    private bool CanChange() => !IsLocked;

    private void MarkDirty()
    {
        if (_loading)
            return;

        IsDirty = true;
        ErrorMessage = null;
    }

    private bool CanSave() => SelectedFile is not null && IsSupported && IsDirty && !IsLocked;

    /// <summary>
    /// Собирает <see cref="ManifestSettings"/> из текущих полей и сохраняет файл целиком через
    /// <see cref="IAddinMarkupService.Save"/> — тот же атомарный путь, что у Form/Markup-подпанелей.
    /// Когда оба поля пусты и в исходном блоке не было чужих узлов, блок целиком убирается из XML
    /// (не пишем пустой <c>&lt;ManifestSettings /&gt;</c> без надобности) — иначе чужие узлы и
    /// порядок тегов сохраняются как есть, для честного round-trip.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (SelectedFile?.File is not { } file)
            return;

        var trimmedContextName = string.IsNullOrWhiteSpace(ContextName) ? null : ContextName.Trim();
        var hasUnknownNodes = _originalSettings?.UnknownNodes.Count > 0;
        var hasContent = UseRevitContext is not null || trimmedContextName is not null || hasUnknownNodes;

        var settings = hasContent
            ? new ManifestSettings(
                UseRevitContext,
                trimmedContextName,
                _originalSettings?.TagOrder ?? ["UseRevitContext", "ContextName"],
                _originalSettings?.UnknownNodes ?? [])
            : null;

        var manifest = file.Manifest with { Settings = settings };
        var xml = _parser.ToXml(manifest);

        try
        {
            _markupService.Save(file, xml);
            _logger.LogInformation("Save: ManifestSettings файла {FileName} сохранены", file.FileName);
            _fileCatalog.Refresh();
        }
        catch (Exception ex) when (ex is AddinManifestFormatException or IOException or RevitRunningException)
        {
            ErrorMessage = ex.Message;
        }
    }

    private bool CanDiscard() => IsDirty && !IsLocked;

    /// <summary>Возвращает подпанель к последнему сохранённому блоку, отбрасывая правки.</summary>
    [RelayCommand(CanExecute = nameof(CanDiscard))]
    private void Discard() => LoadFrom(SelectedFile);

    partial void OnUseRevitContextChanged(bool? value) => MarkDirty();

    partial void OnContextNameChanged(string? value) => MarkDirty();

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
        SetUseRevitContextCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Разносит <see cref="IRevitProcessGuard.IsRunning"/> на себя.</summary>
    private void SyncEditLock() => IsLocked = _guard.IsRunning;

    private void RefreshSnapshot() => OnPropertyChanged((string?)null);

    /// <summary>Приглашение выбрать файл, когда ничего не выбрано.</summary>
    public string EmptySelectionPrompt => _localizer["EmptySelection_FilePrompt"];

    /// <summary>Кнопка "Отменить".</summary>
    public string DiscardButtonLabel => _localizer["DiscardButton"];

    /// <summary>Кнопка "Сохранить".</summary>
    public string SaveButtonLabel => _localizer["SaveButton"];

    /// <summary>Пояснение для версий без поддержки ManifestSettings.</summary>
    public string UnsupportedNotice => _localizer["ManifestSettings_UnsupportedNotice"];

    /// <summary>Чип UseRevitContext "не задано".</summary>
    public string UnsetOptionLabel => _localizer["UseRevitContext_UnsetOption"];

    /// <summary>Чип UseRevitContext "да".</summary>
    public string YesOptionLabel => _localizer["UseRevitContext_YesOption"];

    /// <summary>Чип UseRevitContext "нет".</summary>
    public string NoOptionLabel => _localizer["UseRevitContext_NoOption"];

    /// <inheritdoc />
    public override string ToString() =>
        $"ManifestSettings(File={SelectedFile?.FileName ?? "-"}, UseRevitContext={UseRevitContext?.ToString() ?? "-"}, Dirty={IsDirty})";
}
