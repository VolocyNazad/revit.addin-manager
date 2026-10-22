using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Localization;

namespace AddinManager.Launcher.ViewModels.Dialogs;

/// <summary>Модель окна подтверждения: текст вопроса задаёт сервис, кнопки закрывают окно результатом.</summary>
public sealed partial class ConfirmDialogViewModel(IStringLocalizer<ConfirmDialogViewModel> localizer) : ObservableObject
{
    /// <summary>Текст вопроса (выставляет сервис перед каждым показом).</summary>
    [ObservableProperty]
    private string _message = string.Empty;

    /// <summary>Результат: вид пишет его в DialogResult и закрывается; <see langword="null"/> — ещё не ответили.</summary>
    [ObservableProperty]
    private bool? _result;

    /// <summary>Кнопка согласия.</summary>
    public string YesLabel => localizer["ConfirmYes"];

    /// <summary>Кнопка отказа.</summary>
    public string NoLabel => localizer["ConfirmNo"];

    /// <summary>Согласиться и закрыть окно.</summary>
    [RelayCommand]
    private void Yes() => Result = true;

    /// <summary>Отказаться и закрыть окно.</summary>
    [RelayCommand]
    private void No() => Result = false;

    /// <inheritdoc />
    public override string ToString() => $"Confirm(Result={Result?.ToString() ?? "-"})";
}
