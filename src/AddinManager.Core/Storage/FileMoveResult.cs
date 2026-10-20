namespace AddinManager.Core.Storage;

/// <summary>
/// Результат перемещения файла: новая запись плюс данные о файле, который был в цели и заменён
/// (если пользователь выбрал замену) — для восстановления через <c>Ctrl+Z</c>.
/// </summary>
/// <param name="File">Файл в новой папке.</param>
/// <param name="ReplacedPath">Путь заменённого целевого файла; <see langword="null"/>, если цели не было.</param>
/// <param name="ReplacedContent">Содержимое заменённого файла до перемещения в Корзину.</param>
public sealed record FileMoveResult(AddinFile File, string? ReplacedPath, byte[]? ReplacedContent);
