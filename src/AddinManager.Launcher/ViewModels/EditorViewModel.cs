using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Localization;
using AddinManager.Localization.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Localization;

namespace AddinManager.Launcher.ViewModels;

/// <summary>Зона редактора: режимы панели.</summary>
public sealed partial class EditorViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;
    private readonly IStringLocalizer<EditorViewModel> _localizer;
    private readonly Func<EditorMode, ISavablePane?> _activePane;

    [ObservableProperty]
    private EditorMode _mode = EditorMode.Form;

    [ObservableProperty]
    private double _entriesFraction = 0.35;

    /// <summary>Создает зону.</summary>
    /// <param name="localizationService">Сервис языка — смена языка перечитывает тултипы режимов.</param>
    /// <param name="localizer">Строки тултипов режимов.</param>
    /// <param name="activePane">Видимая панель с сохранением для режима (см. корень композиции).</param>
    public EditorViewModel(
        ILocalizationService localizationService,
        IStringLocalizer<EditorViewModel> localizer,
        Func<EditorMode, ISavablePane?> activePane)
    {
        _localizationService = localizationService;
        _localizer = localizer;
        _activePane = activePane;
        _localizationService.LanguageChanged += (_, _) => OnPropertyChanged((string?)null);
    }

    partial void OnEntriesFractionChanged(double value)
    {
        var clamped = double.IsNaN(value) ? 0.5 : Math.Clamp(value, 0.1, 0.9);
        if (Math.Abs(clamped - value) > 0.0001 || double.IsNaN(value))
            EntriesFraction = clamped;
    }

    partial void OnModeChanged(EditorMode value)
    {
        SaveActiveCommand.NotifyCanExecuteChanged();
        OnPropertyChanged((string?)null);
    }

    /// <summary>Переключить режим панели редактора.</summary>
    [RelayCommand]
    public void SetEditorMode(EditorMode mode) => Mode = mode;

    private bool CanSaveActive() => _activePane(Mode)?.SaveCommand.CanExecute(null) == true;

    /// <summary>
    /// Сохраняет видимую панель текущего режима (хоткей Ctrl+S): проверки готовности —
    /// те же <c>CanExecute</c> её собственной кнопки сохранения (файл выбран, есть изменения,
    /// текст валиден, Revit закрыт). В режиме одних записей сохранять нечего — команда гаснет.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveActive))]
    public void SaveActive()
    {
        if (_activePane(Mode)?.SaveCommand is { } save && save.CanExecute(null))
            save.Execute(null);
    }

    /// <summary>Тултип режима "Записи манифеста".</summary>
    public string EntriesTooltip => _localizer["EditorMode_EntriesTooltip"];

    /// <summary>Тултип режима "Форма".</summary>
    public string FormTooltip => _localizer["EditorMode_FormTooltip"];

    /// <summary>Тултип режима "Разметка".</summary>
    public string MarkupTooltip => _localizer["EditorMode_MarkupTooltip"];

    /// <summary>Тултип режима "Записи и форма".</summary>
    public string EntriesFormTooltip => _localizer["EditorMode_EntriesFormTooltip"];

    /// <summary>Тултип режима "Записи и разметка".</summary>
    public string EntriesMarkupTooltip => _localizer["EditorMode_EntriesMarkupTooltip"];

    /// <summary>Тултип режима "Настройки файла".</summary>
    public string SettingsTooltip => _localizer["EditorMode_SettingsTooltip"];

    /// <inheritdoc />
    public override string ToString() => $"Editor(Mode={Mode}, Split={EntriesFraction:0.##})";
}
