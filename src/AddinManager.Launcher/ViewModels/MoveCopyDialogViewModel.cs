using AddinManager.Core.Abstractions.Manifests;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Localization;

namespace AddinManager.Launcher.ViewModels;

/// <summary>
/// Диалог выбора цели перемещения/копирования: версия, scope, состояние enabled и замена
/// существующего файла. Целевой путь и конфликт считает <see cref="IAddinStore"/>, поэтому
/// диалог не знает про %APPDATA%/%PROGRAMDATA% и тестируется через fake-store.
/// </summary>
public sealed partial class MoveCopyDialogViewModel : ObservableObject
{
    private readonly IAddinStore _store;
    private readonly IManifestSchema _schema;
    private readonly IStringLocalizer<MoveCopyDialogViewModel> _localizer;
    private AddinFile? _file;

    /// <summary>Создает модель диалога.</summary>
    /// <param name="store">Даёт путь цели и наличие конфликта.</param>
    /// <param name="schema">Поддерживает ли целевая версия ManifestSettings.</param>
    /// <param name="localizer">Подписи и предупреждения.</param>
    public MoveCopyDialogViewModel(
        IAddinStore store,
        IManifestSchema schema,
        IStringLocalizer<MoveCopyDialogViewModel> localizer)
    {
        _store = store;
        _schema = schema;
        _localizer = localizer;
    }

    /// <summary>Версия Revit, куда переносим/копируем.</summary>
    [ObservableProperty]
    private string _version = "2027";

    /// <summary>Целевая область.</summary>
    [ObservableProperty]
    private AddinScope _scope = AddinScope.User;

    /// <summary>Включить файл после операции; по умолчанию сохраняется текущее состояние.</summary>
    [ObservableProperty]
    private bool _enableAfter;

    /// <summary>Заменить существующий файл в цели (старый уйдёт в Корзину).</summary>
    [ObservableProperty]
    private bool _overwrite;

    /// <summary>Ошибка валидации под полями.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Результат диалога: <see langword="true"/> — подтвердили.</summary>
    [ObservableProperty]
    private bool? _result;

    /// <summary>Перемещение ли это (иначе копирование).</summary>
    public bool IsMove { get; private set; }

    /// <summary>Заголовок диалога.</summary>
    public string Title => IsMove ? _localizer["MoveTitle"] : _localizer["CopyTitle"];

    /// <summary>Кнопка подтверждения.</summary>
    public string ConfirmButtonLabel => IsMove ? _localizer["MoveButton"] : _localizer["CopyButton"];

    /// <summary>Кнопка отмены.</summary>
    public string CancelButtonLabel => _localizer["CancelButton"];

    /// <summary>Подпись исходного файла.</summary>
    public string SourceLabel => _localizer["SourceLabel"];

    /// <summary>Подпись целевого пути.</summary>
    public string TargetLabel => _localizer["TargetLabel"];

    /// <summary>Подпись "Версия".</summary>
    public string VersionLabel => _localizer["VersionLabel"];

    /// <summary>Подпись "Scope".</summary>
    public string ScopeLabel => _localizer["ScopeLabel"];

    /// <summary>Чекбокс "Включить после операции".</summary>
    public string EnableAfterLabel => _localizer["EnableAfterLabel"];

    /// <summary>Чекбокс "Заменить существующий".</summary>
    public string OverwriteLabel => _localizer["OverwriteLabel"];

    /// <summary>Имя исходного файла.</summary>
    public string FileName => _file?.FileName ?? string.Empty;

    /// <summary>Полный путь, куда попадёт файл.</summary>
    public string TargetPath => _file is null ? string.Empty : _store.GetTargetPath(_file.FileName, Version, Scope, EnableAfter);

    /// <summary>В цели уже есть файл с таким именем.</summary>
    public bool TargetExists => _file is not null && _store.TargetExists(_file.FileName, Version, Scope, EnableAfter);

    /// <summary>Цель совпадает с текущей папкой файла.</summary>
    public bool IsSameFolder => _file is not null
        && string.Equals(TargetPath, _file.FullPath, StringComparison.OrdinalIgnoreCase);

    /// <summary>Нужно показать блок конфликта.</summary>
    public bool ShowConflict => TargetExists && !IsSameFolder;

    /// <summary>Текст предупреждения о занятой цели.</summary>
    public string? ConflictWarning => ShowConflict ? _localizer["ConflictWarning"].Value : null;

    /// <summary>Текст ошибки, если выбрана та же папка.</summary>
    public string? SameFolderError => IsSameFolder ? _localizer["SameFolderError"].Value : null;

    /// <summary>Предупреждение о переносе между версиями.</summary>
    public string? CrossVersionWarning => _file is not null && !string.Equals(Version, _file.Version, StringComparison.Ordinal)
        ? _localizer["CrossVersionWarning"].Value
        : null;

    /// <summary>Предупреждение о ManifestSettings, которые не поддерживает целевая версия.</summary>
    public string? SettingsWarning => _file is not null
        && _file.Manifest.Settings is not null
        && !_schema.SupportsManifestSettings(Version)
            ? _localizer["SettingsWarning"].Value
            : null;

    /// <summary>Есть ли предупреждения для блока над кнопками.</summary>
    public bool HasWarnings => CrossVersionWarning is not null || SettingsWarning is not null;

    /// <summary>Можно ли подтвердить выбор.</summary>
    public bool CanConfirm => _file is not null && !IsSameFolder && (!TargetExists || Overwrite);

    /// <summary>Подготавливает диалог под конкретный файл и тип операции.</summary>
    public void Initialize(MoveCopyRequest request)
    {
        _file = request.File;
        IsMove = request.Kind == MoveCopyKind.Move;
        Version = request.File.Version;
        Scope = request.File.Scope;
        EnableAfter = request.File.Enabled;
        Overwrite = false;
        ErrorMessage = null;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ConfirmButtonLabel));
        OnPropertyChanged(nameof(FileName));
        RefreshTarget();
    }

    /// <summary>Выбрать версию.</summary>
    [RelayCommand]
    private void SetVersion(string? version)
    {
        if (!string.IsNullOrEmpty(version))
            Version = version;
    }

    /// <summary>Выбрать область.</summary>
    [RelayCommand]
    private void SetScope(AddinScope scope) => Scope = scope;

    /// <summary>Подтвердить выбор.</summary>
    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (IsSameFolder)
        {
            ErrorMessage = _localizer["SameFolderError"];
            return;
        }

        ErrorMessage = null;
        Result = true;
    }

    /// <summary>Закрыть без операции.</summary>
    [RelayCommand]
    private void Cancel() => Result = false;

    partial void OnVersionChanged(string value) => RefreshTarget();

    partial void OnScopeChanged(AddinScope value) => RefreshTarget();

    partial void OnEnableAfterChanged(bool value) => RefreshTarget();

    partial void OnOverwriteChanged(bool value)
    {
        ErrorMessage = null;
        ConfirmCommand.NotifyCanExecuteChanged();
    }

    private void RefreshTarget()
    {
        OnPropertyChanged(nameof(TargetPath));
        OnPropertyChanged(nameof(TargetExists));
        OnPropertyChanged(nameof(IsSameFolder));
        OnPropertyChanged(nameof(ShowConflict));
        OnPropertyChanged(nameof(ConflictWarning));
        OnPropertyChanged(nameof(SameFolderError));
        OnPropertyChanged(nameof(CrossVersionWarning));
        OnPropertyChanged(nameof(SettingsWarning));
        OnPropertyChanged(nameof(HasWarnings));
        ConfirmCommand.NotifyCanExecuteChanged();
    }
}
