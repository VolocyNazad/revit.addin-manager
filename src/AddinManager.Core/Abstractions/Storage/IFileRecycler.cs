namespace AddinManager.Core.Abstractions.Storage;

/// <summary>
/// Помещает файл в Корзину вместо безвозвратного удаления. Реализация платформозависима;
/// <see cref="AddinManager.Core.Storage.FileSystemAddinStore"/> использует её, если она передана.
/// </summary>
public interface IFileRecycler
{
    /// <summary>Перемещает файл по указанному пути в Корзину.</summary>
    /// <param name="path">Полный путь к существующему файлу.</param>
    /// <exception cref="IOException">Файл не удалось поместить в Корзину.</exception>
    void Recycle(string path);
}
