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

    [ObservableProperty]
    private EditorMode _mode = EditorMode.Form;

    [ObservableProperty]
    private double _entriesFraction = 0.3;

    /// <summary>Создает зону.</summary>
    /// <param name="localizationService">Сервис языка — смена языка перечитывает тултипы режимов.</param>
    /// <param name="localizer">Строки тултипов режимов.</param>
    public EditorViewModel(ILocalizationService localizationService, IStringLocalizer<EditorViewModel> localizer)
    {
        _localizationService = localizationService;
        _localizer = localizer;
        _localizationService.LanguageChanged += (_, _) => OnPropertyChanged((string?)null);
    }

    partial void OnEntriesFractionChanged(double value)
    {
        var clamped = double.IsNaN(value) ? 0.5 : Math.Clamp(value, 0.1, 0.9);
        if (Math.Abs(clamped - value) > 0.0001 || double.IsNaN(value))
            EntriesFraction = clamped;
    }

    partial void OnModeChanged(EditorMode value) => OnPropertyChanged((string?)null);

    /// <summary>Переключить режим панели редактора.</summary>
    [RelayCommand]
    public void SetEditorMode(EditorMode mode) => Mode = mode;

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
