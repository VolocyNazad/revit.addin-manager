using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Localization;

namespace AddinManager.Launcher.ViewModels.Dialogs;

/// <summary>Модель диалога обновления: показывает, что вышла новая версия, и предлагает скачать.</summary>
public sealed partial class UpdateDialogViewModel(IStringLocalizer<UpdateDialogViewModel> localizer) : ObservableObject
{
    /// <summary>Текст новости (выставляет сервис перед показом).</summary>
    [ObservableProperty]
    private string _message = string.Empty;

    /// <summary>Результат: <see langword="true"/> — скачать, <see langword="false"/>/<see langword="null"/> — нет.</summary>
    [ObservableProperty]
    private bool? _result;

    /// <summary>Заголовок "Доступно обновление".</summary>
    public string TitleLabel => localizer["UpdateTitle"];

    /// <summary>Кнопка "Скачать".</summary>
    public string DownloadLabel => localizer["DownloadButton"];

    /// <summary>Кнопка "Позже".</summary>
    public string LaterLabel => localizer["LaterButton"];

    /// <summary>Скачать и закрыть окно.</summary>
    [RelayCommand]
    private void Download() => Result = true;

    /// <summary>Отложить и закрыть окно.</summary>
    [RelayCommand]
    private void Later() => Result = false;

    /// <inheritdoc />
    public override string ToString() => $"Update(Result={Result?.ToString() ?? "-"})";
}
