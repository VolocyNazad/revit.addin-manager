using System.Collections.ObjectModel;
using AddinManager.Launcher.ViewModels;

namespace AddinManager.Launcher.Abstractions.Services;

/// <summary>
/// Каталог файлов зоны списка: живые строки и их перечитывание с диска.
/// Имплементирует зона списка; потребителям (кросс-файловые дубли, обновление
/// после сохранений) не нужна вся зона целиком — только этот срез.
/// </summary>
public interface IAddinFileCatalog
{
    /// <summary>Сырые строки списка — всё, что прочитано с диска, без фильтра.</summary>
    ObservableCollection<AddinFileRowViewModel> Files { get; }

    /// <summary>Перечитывает файлы всех версий с диска.</summary>
    void Refresh();
}
