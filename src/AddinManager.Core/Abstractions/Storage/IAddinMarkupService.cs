using AddinManager.Core.Guard;
using AddinManager.Core.Manifests;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;

namespace AddinManager.Core.Abstractions.Storage;

/// <summary>
/// Сырой текст .addin файла для вкладки "Разметка" (план, раздел 6 — "The raw XML tab shows
/// the whole file"): чтение, проверка и атомарная запись, в обход структурной модели
/// <see cref="AddinManifest"/> — вкладка редактирует текст напрямую. Реализации заменяемы через DI.
/// </summary>
public interface IAddinMarkupService
{
    /// <summary>Читает файл как есть, без парсинга.</summary>
    string ReadRaw(AddinFile file);

    /// <summary>
    /// Проверяет XML, не сохраняя. <see langword="null"/> — валиден; иначе текст ошибки для UI
    /// (в частности, структурные ошибки и повторяющиеся <c>AddInId</c> — план, раздел 6).
    /// </summary>
    string? Validate(string xml);

    /// <summary>
    /// Проверяет и атомарно сохраняет (temp + <see cref="File.Replace(string, string, string?)"/>,
    /// предыдущее содержимое остаётся в <c>.bak</c>). Невалидный XML не пишется — бросает
    /// <see cref="AddinManifestFormatException"/> с тем же сообщением, что и <see cref="Validate"/>.
    /// </summary>
    /// <exception cref="RevitRunningException">Revit запущен — запись запрещена.</exception>
    void Save(AddinFile file, string xml);
}
