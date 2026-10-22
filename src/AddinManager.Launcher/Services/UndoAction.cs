using AddinManager.Core.Storage;

namespace AddinManager.Launcher.Services;

/// <summary>
/// Одно отменяемое действие зоны списка: что именно вернуть при <c>Ctrl+Z</c>. Запись хранит
/// только данные, необходимые для отмены; выполнение — в <see cref="UndoService"/>.
/// </summary>
public abstract record UndoAction
{
    /// <summary>Имя действия для логов и диагностики.</summary>
    public string ActionName => GetType().Name;
}

/// <summary>Переключение enabled/disabled одного файла.</summary>
/// <param name="After">Запись после переключения — идентичность действия.</param>
/// <param name="PreviousEnabled">Прежнее состояние, которое вернёт отмена.</param>
public sealed record UndoToggle(AddinFile After, bool PreviousEnabled) : UndoAction;

/// <summary>Пакетное переключение enabled/disabled: отмена возвращает состояние каждого файла.</summary>
/// <param name="Toggles">Переключённые файлы и их прежние состояния.</param>
public sealed record UndoToggleGroup(IReadOnlyList<UndoToggle> Toggles) : UndoAction;

/// <summary>Удаление одного или нескольких файлов — отмена восстанавливает их байты.</summary>
/// <param name="Files">Удалённые файлы и их содержимое.</param>
public sealed record UndoDelete(IReadOnlyList<DeletedFile> Files) : UndoAction;

/// <summary>Файл, удалённый в Корзину, и его байты, сохранённые для восстановления.</summary>
/// <param name="File">Запись файла на момент удаления.</param>
/// <param name="Content">Содержимое файла до удаления.</param>
public sealed record DeletedFile(AddinFile File, byte[] Content);

/// <summary>Перемещение файла в другую версию/область, возможно с заменой цели.</summary>
/// <param name="Moved">Запись файла после перемещения.</param>
/// <param name="Original">Запись до перемещения — куда вернуть файл.</param>
/// <param name="ReplacedPath">Путь заменённого целевого файла, если он был.</param>
/// <param name="ReplacedContent">Байты заменённого целевого файла, если он был.</param>
public sealed record UndoMove(
    AddinFile Moved,
    AddinFile Original,
    string? ReplacedPath,
    byte[]? ReplacedContent) : UndoAction;

/// <summary>Копирование файла в другую версию/область: отмена удаляет копию.</summary>
/// <param name="Source">Исходная запись — на неё возвращается выбор.</param>
/// <param name="Copy">Созданная копия, которую нужно удалить.</param>
/// <param name="ReplacedPath">Путь заменённого целевого файла, если он был.</param>
/// <param name="ReplacedContent">Байты заменённого целевого файла, если он был.</param>
public sealed record UndoCopy(
    AddinFile Source,
    AddinFile Copy,
    string? ReplacedPath,
    byte[]? ReplacedContent) : UndoAction;

/// <summary>Переименование файла: отмена возвращает прежнее имя.</summary>
/// <param name="Before">Запись до переименования — прежнее имя и выбор после отмены.</param>
/// <param name="After">Запись после переименования — что искать в актуальном списке.</param>
public sealed record UndoRename(AddinFile Before, AddinFile After) : UndoAction;
