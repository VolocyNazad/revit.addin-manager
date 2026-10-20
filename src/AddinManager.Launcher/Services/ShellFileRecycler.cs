using System.IO;
using AddinManager.Core.Abstractions.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;

namespace AddinManager.Launcher.Services;

/// <summary>
/// Корзина Windows через <see cref="FileSystem"/>: файл исчезает из папки Revit, но остаётся
/// восстановимым средствами Проводника. Ошибки не глотаем — файловый store логирует и
/// заворачивает их в <see cref="IOException"/> для строки списка.
/// </summary>
/// <param name="logger">Логгер сервиса.</param>
public sealed class ShellFileRecycler(ILogger<ShellFileRecycler> logger) : IFileRecycler
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
            logger.LogWarning(ex, "Recycle({Path}): перемещение в Корзину отменено", path);
            throw new IOException("Не удалось переместить файл в Корзину.", ex);
        }
    }
}
