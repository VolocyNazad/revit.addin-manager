namespace AddinManager.Core.Storage;

/// <summary>
/// Правило имени <c>.addin</c>-файла, общее для реального store и тестовых двойников: обрезать
/// пробелы и дописать расширение, если его нет. Пустая строка остаётся пустой — каждый вызов
/// сам решает, каким сообщением и в каком порядке её отклонять.
/// </summary>
public static class AddinFileNames
{
    /// <summary>Обрезает имя и обеспечивает расширение <c>.addin</c>.</summary>
    /// <param name="fileName">Запрошенное имя файла; <see langword="null"/> трактуется как пустое.</param>
    /// <returns>Нормализованное имя; пустая строка, если имя пустое или состояло из пробелов.</returns>
    public static string Normalize(string? fileName)
    {
        var normalizedName = (fileName ?? string.Empty).Trim();
        if (normalizedName.Length == 0)
            return string.Empty;

        return normalizedName.EndsWith(AddinFolderLayout.AddinFileExtension, StringComparison.OrdinalIgnoreCase)
            ? normalizedName
            : normalizedName + AddinFolderLayout.AddinFileExtension;
    }
}
