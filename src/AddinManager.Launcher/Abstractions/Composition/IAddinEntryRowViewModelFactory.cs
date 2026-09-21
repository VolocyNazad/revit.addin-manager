using AddinManager.Core.Manifests;
using AddinManager.Launcher.ViewModels;

namespace AddinManager.Launcher.Abstractions.Composition;

/// <summary>
/// Создаёт строки подпанели записей: записи известны только при чтении выбранного файла,
/// поэтому контейнер собирает фабрику, а та подставляет данные. Реализация заменяема через DI.
/// </summary>
public interface IAddinEntryRowViewModelFactory
{
    /// <summary>Создает строку для записи манифеста.</summary>
    /// <param name="entry">Исходная запись манифеста.</param>
    /// <param name="index">Порядковый номер записи в файле, считая с 1.</param>
    /// <param name="duplicateInFile"><c>AddInId</c> повторяется в том же файле.</param>
    /// <param name="duplicateAcrossFiles"><c>AddInId</c> встречается в других файлах той же версии Revit.</param>
    AddinEntryRowViewModel Create(AddinEntry entry, int index, bool duplicateInFile, bool duplicateAcrossFiles);
}
