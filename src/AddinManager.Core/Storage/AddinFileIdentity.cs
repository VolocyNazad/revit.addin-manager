namespace AddinManager.Core.Storage;

/// <summary>
/// Идентичность <c>.addin</c> файла: имя, область и версия Revit. Строки сравниваются
/// посимвольно (<see cref="StringComparison.Ordinal"/>), как и раньше в списке: идентичность —
/// это ключ стабильности строк между пересканированиями, а не путь и не состояние на диске.
/// Полный путь, <see cref="AddinFile.Enabled"/> и содержимое манифеста в идентичность не входят:
/// они меняются при переключении и сохранении, а файл остаётся тем же.
/// </summary>
/// <param name="FileName">Имя файла без пути.</param>
/// <param name="Scope">Пользовательская или машинная область.</param>
/// <param name="Version">Версия Revit, например <c>"2025"</c>.</param>
public sealed record AddinFileIdentity(string FileName, AddinScope Scope, string Version)
{
    /// <summary>Снимает идентичность с текущей записи файла.</summary>
    /// <param name="file">Запись файла.</param>
    public static AddinFileIdentity From(AddinFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new AddinFileIdentity(file.FileName, file.Scope, file.Version);
    }
}
