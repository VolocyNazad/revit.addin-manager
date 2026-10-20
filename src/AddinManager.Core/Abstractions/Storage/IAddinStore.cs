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
    /// Удаляет файл с диска. Реализация по умолчанию перемещает его в Корзину (если передан
    /// <see cref="IFileRecycler"/>), а без неё удаляет безвозвратно. Подтверждение — за вызывающей стороной.
    /// </summary>
    /// <param name="file">Удаляемый файл.</param>
    /// <returns>Содержимое файла до удаления — для отмены через <c>Ctrl+Z</c>.</returns>
    /// <exception cref="RevitRunningException">Revit запущен — удаление запрещено.</exception>
    /// <exception cref="IOException">Файл не удалился (нет прав, занят).</exception>
    byte[] Delete(AddinFile file);

    /// <summary>
    /// Переименовывает файл на месте, сохраняя папку (корень версии или <c>disabled/</c>), scope и
    /// состояние enabled/disabled. Расширение <c>.addin</c> дописывается, если его нет.
    /// </summary>
    /// <param name="file">Текущая запись файла.</param>
    /// <param name="newFileName">Новое имя файла (без пути).</param>
    /// <returns>Обновлённая запись с новыми <see cref="AddinFile.FileName"/> и <see cref="AddinFile.FullPath"/>.</returns>
    /// <exception cref="RevitRunningException">Revit запущен — переименование запрещено.</exception>
    /// <exception cref="IOException">Имя пустое/недопустимое или файл с таким именем уже есть.</exception>
    AddinFile Rename(AddinFile file, string newFileName);

    /// <summary>Вычисляет путь, куда попадёт файл при переносе/копировании (без обращения к диску).</summary>
    /// <param name="fileName">Имя файла.</param>
    /// <param name="version">Версия Revit.</param>
    /// <param name="scope">Область.</param>
    /// <param name="enabled">Состояние: <see langword="true"/> — корень версии, иначе <c>disabled/</c>.</param>
    string GetTargetPath(string fileName, string version, AddinScope scope, bool enabled);

    /// <summary>Проверяет, занят ли путь, куда попадёт файл; используется диалогом выбора цели.</summary>
    bool TargetExists(string fileName, string version, AddinScope scope, bool enabled);

    /// <summary>
    /// Перемещает файл в другую версию/scope, сохраняя имя. Недостающие папки создаются.
    /// При <paramref name="overwrite"/> существующий целевой файл сначала уходит в Корзину,
    /// а его содержимое возвращается в результате для отмены.
    /// </summary>
    /// <exception cref="RevitRunningException">Revit запущен — перенос запрещён.</exception>
    /// <exception cref="IOException">Цель занята без <paramref name="overwrite"/>, нет прав или перенос не удался.</exception>
    FileMoveResult Move(AddinFile file, string targetVersion, AddinScope targetScope, bool enabled, bool overwrite);

    /// <summary>
    /// Копирует файл в другую версию/scope, сохраняя имя и исходник. Недостающие папки создаются.
    /// При <paramref name="overwrite"/> существующий целевой файл сначала уходит в Корзину.
    /// </summary>
    /// <returns>Новая запись и данные заменённого целевого файла для отмены копирования.</returns>
    /// <exception cref="RevitRunningException">Revit запущен — копирование запрещено.</exception>
    /// <exception cref="IOException">Цель занята без <paramref name="overwrite"/>, нет прав или копирование не удалось.</exception>
    FileMoveResult Copy(AddinFile file, string targetVersion, AddinScope targetScope, bool enabled, bool overwrite);

    /// <summary>
    /// Создаёт пустой манифест (без записей) и возвращает его запись. Суффикс
    /// <c>.addin</c> дописывается, недостающие папки создаются.
    /// </summary>
    /// <exception cref="RevitRunningException">Revit запущен — создание запрещено.</exception>
    /// <exception cref="IOException">Файл уже есть или не записался.</exception>
    AddinFile CreateFile(string fileName, string version, AddinScope scope, bool disabled);
}
