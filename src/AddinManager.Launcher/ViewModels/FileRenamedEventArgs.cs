using AddinManager.Core.Storage;

namespace AddinManager.Launcher.ViewModels;

/// <summary>Успешное переименование: запись до и после — для отмены через <c>Ctrl+Z</c>.</summary>
/// <param name="before">Запись до переименования.</param>
/// <param name="after">Запись после переименования.</param>
public sealed class FileRenamedEventArgs(AddinFile before, AddinFile after) : EventArgs
{
    /// <summary>Запись до переименования.</summary>
    public AddinFile Before { get; } = before;

    /// <summary>Запись после переименования.</summary>
    public AddinFile After { get; } = after;
}
