using AddinManager.Core.Abstractions.Storage;
using AddinManager.Launcher.Services;
using AddinManager.Launcher.ViewModels;

namespace AddinManager.Launcher.Abstractions.Services;

/// <summary>
/// Операции с файлами списка: создание, удаление, перемещение и копирование. Сервис владеет
/// диалогами, мутациями <see cref="IAddinStore"/> и записью undo; зона списка только применяет
/// <see cref="FileOperationOutcome"/> к виду.
/// </summary>
public interface IFileOperations
{
    /// <summary>Создаёт файл по параметрам из диалога.</summary>
    /// <returns>Результат для вида.</returns>
    FileOperationOutcome AddFile();

    /// <summary>Удаляет файл строки в Корзину после подтверждения.</summary>
    /// <param name="row">Строка списка.</param>
    /// <returns>Результат для вида.</returns>
    FileOperationOutcome DeleteRow(AddinFileRowViewModel row);

    /// <summary>Удаляет отмеченные файлы после одного подтверждения.</summary>
    /// <param name="rows">Отмеченные строки.</param>
    /// <returns>Результат для вида, включая ошибку частичного удаления.</returns>
    FileOperationOutcome DeleteSelected(IReadOnlyList<AddinFileRowViewModel> rows);

    /// <summary>Перемещает или копирует файл в выбранную цель после диалога.</summary>
    /// <param name="row">Строка списка.</param>
    /// <param name="kind">Перемещение или копирование.</param>
    /// <returns>Результат для вида.</returns>
    FileOperationOutcome MoveCopy(AddinFileRowViewModel row, MoveCopyKind kind);
}
