using System.ComponentModel;
using System.Windows.Input;
using AddinManager.Launcher.Abstractions.Services;
using CommunityToolkit.Mvvm.Input;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// Панель редактора с сохранением для тестов общего заголовка: хранит состояние, которое заголовок
/// показывает, и считает вызовы Save/Discard. Доступность команд привязана к тем же признакам,
/// что у настоящих панелей (есть изменения, нет ошибки).
/// </summary>
public sealed class FakeSavablePane : ISavablePane, INotifyPropertyChanged
{
    private readonly RelayCommand _saveCommand;
    private readonly RelayCommand _discardCommand;
    private bool _isDirty;
    private string? _errorMessage;
    private string _saveButtonLabel = "Save (Ctrl+S)";
    private string _discardButtonLabel = "Discard (Esc)";

    /// <summary>Создает панель с командами, зависящими от <see cref="IsDirty"/> и <see cref="ErrorMessage"/>.</summary>
    public FakeSavablePane()
    {
        _saveCommand = new RelayCommand(
            () => SaveCalls++,
            () => IsDirty && ErrorMessage is null);
        _discardCommand = new RelayCommand(
            () => DiscardCalls++,
            () => IsDirty);
        SaveCommand = _saveCommand;
        DiscardCommand = _discardCommand;
    }

    /// <summary>Сколько раз вызывали <see cref="SaveCommand"/>.</summary>
    public int SaveCalls { get; private set; }

    /// <summary>Сколько раз вызывали <see cref="DiscardCommand"/>.</summary>
    public int DiscardCalls { get; private set; }

    /// <inheritdoc />
    public ICommand SaveCommand { get; }

    /// <inheritdoc />
    public ICommand DiscardCommand { get; }

    /// <inheritdoc />
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty == value)
                return;
            _isDirty = value;
            Notify(nameof(IsDirty));
            _saveCommand.NotifyCanExecuteChanged();
            _discardCommand.NotifyCanExecuteChanged();
        }
    }

    /// <inheritdoc />
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage == value)
                return;
            _errorMessage = value;
            Notify(nameof(ErrorMessage));
            _saveCommand.NotifyCanExecuteChanged();
        }
    }

    /// <inheritdoc />
    public string SaveButtonLabel
    {
        get => _saveButtonLabel;
        set
        {
            if (_saveButtonLabel == value)
                return;
            _saveButtonLabel = value;
            Notify(nameof(SaveButtonLabel));
        }
    }

    /// <inheritdoc />
    public string DiscardButtonLabel
    {
        get => _discardButtonLabel;
        set
        {
            if (_discardButtonLabel == value)
                return;
            _discardButtonLabel = value;
            Notify(nameof(DiscardButtonLabel));
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
