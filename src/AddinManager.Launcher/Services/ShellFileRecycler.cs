using System.IO;
using AddinManager.Core.Abstractions.Storage;
using Microsoft.VisualBasic.FileIO;

namespace AddinManager.Launcher.Services;

/// <summary>
/// Корзина Windows через <see cref="FileSystem"/>: файл исчезает из папки Revit, но остаётся
/// восстановимым средствами Проводника. Ошибки не глотаем и не логируем здесь —
/// файловый store логирует и заворачивает их в <see cref="IOException"/> для строки списка.
/// </summary>
public sealed class ShellFileRecycler : IFileRecycler
{
    /// <inheritdoc />
    public void Recycle(string path)
    {
        try
        {
            FileSystem.DeleteFile(
                path,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin,
                UICancelOption.ThrowException);
        }
        catch (OperationCanceledException ex)
        {
            throw new IOException("The file was not moved to the Recycle Bin.", ex);
        }
    }
}
