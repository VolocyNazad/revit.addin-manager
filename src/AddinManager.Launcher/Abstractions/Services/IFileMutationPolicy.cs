using AddinManager.Core.Storage;

namespace AddinManager.Launcher.Abstractions.Services;

/// <summary>
/// Решает, можно ли менять файл текущим процессом. Файл машинной области (<c>%PROGRAMDATA%</c>)
/// меняется только приложением с правами администратора; пользовательские файлы доступны всегда.
/// </summary>
public interface IFileMutationPolicy
{
    /// <summary>
    /// Возвращает <see langword="true"/>, когда файл нельзя изменять, перемещать, переименовывать
    /// или удалять без перезапуска приложения от имени администратора.
    /// </summary>
    /// <param name="file">Файл, для которого проверяется доступ на изменение.</param>
    bool IsReadOnly(AddinFile file);
}
