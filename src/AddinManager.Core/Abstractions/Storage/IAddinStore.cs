using AddinManager.Core.Guard;
using AddinManager.Core.Storage;

namespace AddinManager.Core.Abstractions.Storage;

/// <summary>
/// Каталог .addin файлов конкретной версии Revit: чтение с диска и переключение
/// enabled/disabled переносом файла (план, разделы 2 и 4). Реализации заменяемы через DI.
/// </summary>
public interface IAddinStore
{
    /// <summary>
    /// Сканирует пользовательскую и машинную папки версии (корень + <c>disabled/</c>).
    /// Отсутствующая папка — пустой результат, а не ошибка. Файлы, которые не удалось
    /// прочитать или распарсить, в результат не попадают.
    /// </summary>
    IReadOnlyList<AddinFile> ScanVersion(string version);

    /// <summary>
    /// Переносит файл между корнем версии и <c>disabled/</c>. Не действует, если состояние уже
    /// совпадает. Возвращает актуальный <see cref="AddinFile"/> (новые <see cref="AddinFile.Enabled"/>
    /// и <see cref="AddinFile.FullPath"/>) — вызывающая сторона обязана заменить им свою копию,
    /// иначе следующий вызов будет опираться на устаревшее состояние (см. AGENTS/architecture.md, Logging).
    /// </summary>
    /// <exception cref="RevitRunningException">Revit запущен — перенос запрещён.</exception>
    AddinFile SetEnabled(AddinFile file, bool enabled);

    /// <summary>
    /// Удаляет файл с диска безвозвратно. Подтверждение — за вызывающей стороной.
    /// </summary>
    /// <exception cref="RevitRunningException">Revit запущен — удаление запрещено.</exception>
    /// <exception cref="IOException">Файл не удалился (нет прав, занят).</exception>
    void Delete(AddinFile file);

    /// <summary>
    /// Создаёт пустой манифест (без записей) и возвращает его запись. Суффикс
    /// <c>.addin</c> дописывается, недостающие папки создаются.
    /// </summary>
    /// <exception cref="RevitRunningException">Revit запущен — создание запрещено.</exception>
    /// <exception cref="IOException">Файл уже есть или не записался.</exception>
    AddinFile CreateFile(string fileName, string version, AddinScope scope, bool disabled);
}
