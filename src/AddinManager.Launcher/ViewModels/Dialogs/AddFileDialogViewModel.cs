using System.Collections.ObjectModel;
using System.IO;
using AddinManager.Core;
using AddinManager.Core.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Localization;

namespace AddinManager.Launcher.ViewModels.Dialogs;

/// <summary>Модель диалога нового файла: собирает параметры, проверяет имя, отдаёт результат.</summary>
public sealed partial class AddFileDialogViewModel : ObservableObject
{
    private readonly IStringLocalizer<AddFileDialogViewModel> _localizer;

    /// <summary>Создает модель с именем по умолчанию.</summary>
    /// <param name="localizer">Подписи и ошибки диалога.</param>
    public AddFileDialogViewModel(IStringLocalizer<AddFileDialogViewModel> localizer)
    {
        _localizer = localizer;
        FileName = _localizer["DefaultFileName"];
        foreach (var version in RevitVersionCatalog.SupportedVersions)
            VersionChips.Add(new FilterChipViewModel(version, version));
        RefreshVersionChips();
    }

    /// <summary>Чипсы версий Revit — из каталога Core, без хардкода в XAML.</summary>
    public ObservableCollection<FilterChipViewModel> VersionChips { get; } = [];

    /// <summary>Имя файла (суффикс <c>.addin</c> допишет стор).</summary>
    [ObservableProperty]
    private string _fileName = string.Empty;

    /// <summary>Область файла.</summary>
    [ObservableProperty]
    private AddinScope _scope = AddinScope.User;

    /// <summary>Версия Revit.</summary>
    [ObservableProperty]
    private string _version = RevitVersionCatalog.Latest;

    /// <summary>Сразу в <c>disabled/</c>.</summary>
    [ObservableProperty]
    private bool _isDisabled;

    /// <summary>Ошибка проверки имени — баннером в диалоге.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Результат: <see langword="true"/> — добавлять, <see langword="false"/>/<see langword="null"/> — нет.</summary>
    [ObservableProperty]
    private bool? _result;

    /// <summary>Подпись "Имя файла".</summary>
    public string FileNameLabel => _localizer["FileNameLabel"];

    /// <summary>Подпись "Scope".</summary>
    public string ScopeLabel => _localizer["ScopeLabel"];

    /// <summary>Подпись "Version".</summary>
    public string VersionLabel => _localizer["VersionLabel"];

    /// <summary>Подпись "Disabled".</summary>
    public string DisabledLabel => _localizer["DisabledLabel"];

    /// <summary>Кнопка "Добавить".</summary>
    public string AddButtonLabel => _localizer["AddButton"];

    /// <summary>Кнопка "Отмена".</summary>
    public string CancelButtonLabel => _localizer["CancelButton"];

    /// <summary>Выбрать область.</summary>
    [RelayCommand]
    private void SetScope(AddinScope scope) => Scope = scope;

    /// <summary>Выбрать версию.</summary>
    [RelayCommand]
    private void SetVersion(string? version)
    {
        if (version is not null)
            Version = version;
    }

    partial void OnVersionChanged(string value) => RefreshVersionChips();

    private void RefreshVersionChips()
    {
        foreach (var chip in VersionChips)
            chip.IsActive = chip.Value == Version;
    }

    /// <summary>Проверить имя и закрыть окно результатом.</summary>
    [RelayCommand]
    private void Add()
    {
        var name = FileName.Trim();
        if (name.Length == 0)
        {
            ErrorMessage = _localizer["AddFileError_EmptyName"];
            return;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            ErrorMessage = _localizer["AddFileError_BadChars"];
            return;
        }

        ErrorMessage = null;
        Result = true;
    }

    /// <summary>Закрыть окно без результата.</summary>
    [RelayCommand]
    private void Cancel() => Result = false;

    /// <inheritdoc />
    public override string ToString() => $"NewFile({FileName}, {Scope}, {Version}, Disabled={IsDisabled})";
}
