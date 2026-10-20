using System.ComponentModel;
using AddinManager.Launcher.Abstractions.Services;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// Выбор файла в памяти: тесты общего заголовка редактора проверяют, что имя и мета-строка
/// берутся из выбора, без списка и диска. Смена значения поднимает уведомление, как настоящий
/// <see cref="IFileSelection"/> из зоны списка.
/// </summary>
public sealed class FakeFileSelection : IFileSelection
{
    private AddinFileRowViewModel? _selectedFile;

    /// <inheritdoc />
    public AddinFileRowViewModel? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (ReferenceEquals(_selectedFile, value))
                return;
            _selectedFile = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedFile)));
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;
}
