using AddinManager.Core.Manifests;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Localization;

namespace AddinManager.Launcher.ViewModels;

/// <summary>Модель диалога новой записи: тип выбирается чипами, результат закрывает окно.</summary>
public sealed partial class AddEntryDialogViewModel : ObservableObject
{
    private readonly IStringLocalizer<AddEntryDialogViewModel> _localizer;

    /// <summary>Создает модель.</summary>
    /// <param name="localizer">Подписи диалога.</param>
    public AddEntryDialogViewModel(IStringLocalizer<AddEntryDialogViewModel> localizer)
    {
        _localizer = localizer;
    }

    /// <summary>Выбранный тип новой записи.</summary>
    [ObservableProperty]
    private AddinEntryType _entryType = AddinEntryType.Application;

    /// <summary>Результат: <see langword="true"/> — добавлять, <see langword="false"/>/<see langword="null"/> — нет.</summary>
    [ObservableProperty]
    private bool? _result;

    /// <summary>Подпись "Type".</summary>
    public string TypeLabel => _localizer["TypeLabel"];

    /// <summary>Кнопка "Добавить".</summary>
    public string AddButtonLabel => _localizer["AddButton"];

    /// <summary>Кнопка "Отмена".</summary>
    public string CancelButtonLabel => _localizer["CancelButton"];

    /// <summary>Выбрать тип.</summary>
    [RelayCommand]
    private void SetEntryType(AddinEntryType type) => EntryType = type;

    /// <summary>Закрыть окно результатом.</summary>
    [RelayCommand]
    private void Add() => Result = true;

    /// <summary>Закрыть окно без результата.</summary>
    [RelayCommand]
    private void Cancel() => Result = false;

    /// <inheritdoc />
    public override string ToString() => $"NewEntry({EntryType})";
}
