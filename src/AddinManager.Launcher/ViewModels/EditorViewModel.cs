using System.ComponentModel;
using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.Services;
using AddinManager.Localization;
using AddinManager.Localization.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Localization;

namespace AddinManager.Launcher.ViewModels;

/// <summary>
/// Зона редактора: режимы панели и общий заголовок. Заголовок один на все режимы (в том числе
/// комбинированные «Записи и форма» / «Записи и разметка»): имя файла, мета-строка, бейдж
/// несохранённых правок, кнопки Save/Discard и баннер ошибки берутся из активной панели, а не
/// повторяются в каждой подпанели (см. docs/architecture.md, "Editor").
/// </summary>
public sealed partial class EditorViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;
    private readonly IStringLocalizer<EditorViewModel> _localizer;
    private readonly IFileSelection _fileSelection;
    private readonly Func<EditorMode, ISavablePane?> _activePane;
    private readonly IEditorDirtyState? _dirtyState;
    private ISavablePane? _subscribedPane;

    [ObservableProperty]
    private EditorMode _mode = EditorMode.Form;

    [ObservableProperty]
    private double _entriesFraction = 0.35;

    /// <summary>Создает зону.</summary>
    /// <param name="localizationService">Сервис языка — смена языка перечитывает тултипы режимов и заголовок.</param>
    /// <param name="localizer">Строки тултипов режимов и заголовка.</param>
    /// <param name="fileSelection">Выбор файла — источник имени и мета-строки в заголовке.</param>
    /// <param name="activePane">Видимая панель с сохранением для режима (см. корень композиции).</param>
    /// <param name="dirtyState">Общее состояние правок для списка перед переносом файла.</param>
    public EditorViewModel(
        ILocalizationService localizationService,
        IStringLocalizer<EditorViewModel> localizer,
        IFileSelection fileSelection,
        Func<EditorMode, ISavablePane?> activePane,
        IEditorDirtyState? dirtyState = null)
    {
        _localizationService = localizationService;
        _localizer = localizer;
        _fileSelection = fileSelection;
        _activePane = activePane;
        _dirtyState = dirtyState;
        _localizationService.LanguageChanged += (_, _) => OnPropertyChanged((string?)null);
        _fileSelection.PropertyChanged += OnFileSelectionChanged;
        AttachActivePane();
    }

    /// <summary>Файл, чьи имя и мета-строка показаны в заголовке; <see langword="null"/>, если файла нет.</summary>
    public AddinFileRowViewModel? SelectedFile => _fileSelection.SelectedFile;

    /// <summary>Показывать ли кнопки Save/Discard: в режиме одних записей редактировать нечего.</summary>
    public bool ShowHeaderActions => ActivePane is not null;

    /// <summary>Есть ли несохранённые правки у видимой панели — для бейджа в заголовке.</summary>
    public bool IsDirty => ActivePane?.IsDirty ?? false;

    /// <summary>Бейдж несохранённых изменений рядом с именем файла.</summary>
    public string UnsavedBadge => _localizer["Editor_UnsavedBadge"];

    /// <summary>Ошибка видимой панели; <see langword="null"/>, если её нет.</summary>
    public string? HeaderError => ActivePane?.ErrorMessage;

    /// <summary>Показывать ли баннер ошибки под заголовком.</summary>
    public bool HasHeaderError => !string.IsNullOrEmpty(HeaderError);

    /// <summary>Подпись общей кнопки сохранения (с хоткеем видимой панели).</summary>
    public string SaveLabel => ActivePane?.SaveButtonLabel ?? string.Empty;

    /// <summary>Подпись общей кнопки отмены правок (с хоткеем видимой панели).</summary>
    public string DiscardLabel => ActivePane?.DiscardButtonLabel ?? string.Empty;

    private ISavablePane? ActivePane => _activePane(Mode);

    partial void OnEntriesFractionChanged(double value)
    {
        var clamped = double.IsNaN(value) ? 0.5 : Math.Clamp(value, 0.1, 0.9);
        if (Math.Abs(clamped - value) > 0.0001 || double.IsNaN(value))
            EntriesFraction = clamped;
    }

    partial void OnModeChanged(EditorMode value)
    {
        AttachActivePane();
        OnPropertyChanged((string?)null);
    }

    /// <summary>Переключить режим панели редактора.</summary>
    [RelayCommand]
    public void SetEditorMode(EditorMode mode) => Mode = mode;

    private bool CanSaveActive() => ActivePane?.SaveCommand.CanExecute(null) == true;

    /// <summary>
    /// Сохраняет видимую панель текущего режима (хоткей Ctrl+S и кнопка заголовка): проверки
    /// готовности — те же <c>CanExecute</c> её собственной кнопки сохранения (файл выбран, есть
    /// изменения, текст валиден, Revit закрыт). В режиме одних записей сохранять нечего.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveActive))]
    public void SaveActive()
    {
        var pane = ActivePane;
        if (pane?.SaveCommand is { } save && save.CanExecute(null))
            save.Execute(null);
    }

    private bool CanDiscardActive() => ActivePane?.DiscardCommand.CanExecute(null) == true;

    /// <summary>Отбрасывает правки видимой панели — кнопка заголовка.</summary>
    [RelayCommand(CanExecute = nameof(CanDiscardActive))]
    public void DiscardActive()
    {
        var pane = ActivePane;
        if (pane?.DiscardCommand is { } discard && discard.CanExecute(null))
            discard.Execute(null);
    }

    private void OnFileSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(IFileSelection.SelectedFile) or null))
            return;

        OnPropertyChanged(nameof(SelectedFile));
        RefreshActiveCommands();
    }

    /// <summary>
    /// Подписывается на уведомления видимой панели: её правки, ошибка и подписи кнопок меняются
    /// без смены режима, а заголовок должен обновиться сразу.
    /// </summary>
    private void AttachActivePane()
    {
        if (_subscribedPane is INotifyPropertyChanged previous)
            previous.PropertyChanged -= OnActivePanePropertyChanged;

        _subscribedPane = ActivePane;

        if (_subscribedPane is INotifyPropertyChanged current)
            current.PropertyChanged += OnActivePanePropertyChanged;

        RefreshHeader();
    }

    private void OnActivePanePropertyChanged(object? sender, PropertyChangedEventArgs e) => RefreshHeader();

    private void RefreshHeader()
    {
        OnPropertyChanged(nameof(ShowHeaderActions));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(HeaderError));
        OnPropertyChanged(nameof(HasHeaderError));
        OnPropertyChanged(nameof(SaveLabel));
        OnPropertyChanged(nameof(DiscardLabel));
        RefreshActiveCommands();
        _dirtyState?.Update(IsDirty, SaveActiveAndCheck);
    }

    private bool SaveActiveAndCheck()
    {
        SaveActive();
        return !IsDirty;
    }

    private void RefreshActiveCommands()
    {
        SaveActiveCommand.NotifyCanExecuteChanged();
        DiscardActiveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Тултип режима "Записи манифеста" (хоткей: Alt+1).</summary>
    public string EntriesTooltip => $"{_localizer["EditorMode_EntriesTooltip"]} (Alt+1)";

    /// <summary>Тултип режима "Форма" (хоткей: Alt+2).</summary>
    public string FormTooltip => $"{_localizer["EditorMode_FormTooltip"]} (Alt+2)";

    /// <summary>Тултип режима "Разметка" (хоткей: Alt+3).</summary>
    public string MarkupTooltip => $"{_localizer["EditorMode_MarkupTooltip"]} (Alt+3)";

    /// <summary>Тултип режима "Записи и форма".</summary>
    public string EntriesFormTooltip => _localizer["EditorMode_EntriesFormTooltip"];

    /// <summary>Тултип режима "Записи и разметка".</summary>
    public string EntriesMarkupTooltip => _localizer["EditorMode_EntriesMarkupTooltip"];

    /// <summary>Тултип режима "Настройки файла" (хоткей: Alt+4).</summary>
    public string SettingsTooltip => $"{_localizer["EditorMode_SettingsTooltip"]} (Alt+4)";

    /// <inheritdoc />
    public override string ToString() =>
        $"Editor(Mode={Mode}, Split={EntriesFraction:0.##}, File={SelectedFile?.FileName ?? "-"}, Dirty={IsDirty})";
}
