using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Storage;
using AddinManager.Launcher.ViewModels;

namespace AddinManager.Launcher.Abstractions.Composition;

/// <summary>
/// Создаёт строки списка: файлы известны только во время сканирования диска, поэтому
/// контейнер не может собрать их сам — вместо этого он собирает фабрику, а та подставляет
/// данные. Реализация заменяема через DI.
/// </summary>
public interface IAddinFileRowViewModelFactory
{
    /// <summary>Создает строку для файла, уже прочитанного со диска.</summary>
    /// <param name="file">Файл из <see cref="IAddinStore.ScanVersion"/>.</param>
    AddinFileRowViewModel Create(AddinFile file);
}
