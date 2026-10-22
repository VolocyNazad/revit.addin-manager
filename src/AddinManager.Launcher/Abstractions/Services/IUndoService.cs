using System.IO;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Guard;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Services;

namespace AddinManager.Launcher.Abstractions.Services;

/// <summary>
/// Одноуровневый стек отмены зоны списка: хранит последнее действие и выполняет его отмену
/// через <see cref="IAddinStore"/>. Перечитывание списка и восстановление выбора остаются
/// за зоной списка — сервис не знает про строки, <c>ICollectionView</c> и тосты.
/// </summary>
public interface IUndoService
{
    /// <summary>Есть ли действие, которое можно отменить.</summary>
    bool HasUndo { get; }

    /// <summary>Стек изменился: действие записано или отменено.</summary>
    event EventHandler? Changed;

    /// <summary>
    /// Запоминает действие как последнее отменяемое. Предыдущее действие забывается —
    /// отмена одноуровневая.
    /// </summary>
    /// <param name="action">Действие для <c>Ctrl+Z</c>.</param>
    void Record(UndoAction action);

    /// <summary>
    /// Отменяет последнее записанное действие и очищает стек. Строки списка могли быть
    /// пересозданы с тех пор, поэтому текущая запись файла ищется через
    /// <paramref name="resolveCurrentFile"/> по идентичности действия.
    /// </summary>
    /// <param name="resolveCurrentFile">
    /// Возвращает актуальную запись файла по идентичности; <see langword="null"/>, если файла
    /// в текущем списке больше нет.
    /// </param>
    /// <returns>Идентичность файла, на который нужно вернуть выбор после отмены.</returns>
    /// <exception cref="IOException">Файл для отмены больше не существует или операция не удалась.</exception>
    /// <exception cref="RevitRunningException">Revit запущен — отмена запрещена.</exception>
    AddinFileIdentity Undo(Func<AddinFileIdentity, AddinFile?> resolveCurrentFile);
}
