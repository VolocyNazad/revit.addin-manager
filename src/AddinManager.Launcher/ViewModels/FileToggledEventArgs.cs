using AddinManager.Core.Storage;

namespace AddinManager.Launcher.ViewModels;

/// <summary>
/// Успешное переключение enabled/disabled: содержит файл до и после переноса, чтобы
/// <see cref="ListViewModel"/> мог запомнить действие для <c>Ctrl+Z</c>.
/// </summary>
/// <param name="before">Запись до переноса.</param>
/// <param name="after">Запись после переноса.</param>
public sealed class FileToggledEventArgs(AddinFile before, AddinFile after) : EventArgs
{
    /// <summary>Запись до переноса.</summary>
    public AddinFile Before { get; } = before;

    /// <summary>Запись после переноса.</summary>
    public AddinFile After { get; } = after;
}
