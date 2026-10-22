using System.Collections.ObjectModel;
using AddinManager.Core;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Abstractions.Composition;
using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.ViewModels;
using Microsoft.Extensions.Logging;

namespace AddinManager.Launcher.Services;

/// <summary>
/// Каталог .addin-файлов зоны списка: сканирует все поддерживаемые версии Revit, пересоздаёт
/// строки через фабрику, сохраняет пакетную отметку по идентичности и пересчитывает
/// кросс-файловые предупреждения. Выбор файла и представление остаются у
/// <see cref="ViewModels.ListViewModel"/> — каталог их не трогает.
/// </summary>
public sealed class FileCatalog(
    IAddinStore store,
    IAddinFileRowViewModelFactory rowFactory,
    ILogger<FileCatalog> logger) : IAddinFileCatalog
{
    /// <inheritdoc />
    public ObservableCollection<AddinFileRowViewModel> Files { get; } = [];

    /// <inheritdoc />
    public void Refresh()
    {
        logger.LogDebug("Refresh: пересканирование всех версий начато");

        // Строки пересоздаются, а пакетная отметка должна пережить пересканирование:
        // запоминаем идентичности отмеченных файлов до очистки.
        var previousBulkSelection = new HashSet<AddinFileIdentity>(
            Files.Where(row => row.IsSelected).Select(row => row.Identity));

        Files.Clear();
        foreach (string version in RevitVersionCatalog.SupportedVersions)
        {
            var filesForVersion = store.ScanVersion(version);
            if (filesForVersion.Count > 0)
                logger.LogDebug("Refresh({Version}): {Count} файлов", version, filesForVersion.Count);

            foreach (AddinFile file in filesForVersion)
            {
                AddinFileRowViewModel row = rowFactory.Create(file);
                row.IsSelected = previousBulkSelection.Contains(row.Identity);
                Files.Add(row);
            }
        }

        UpdateRowWarnings();
        logger.LogInformation("Refresh: найдено {Count} файлов по всем версиям", Files.Count);
    }

    /// <summary>
    /// Пересчитывает предупреждения строк: дубли <c>AddInId</c> считаются только внутри одной
    /// версии Revit (идентификатор в двух файлах разных версий — не конфликт: Revit грузит
    /// версии независимо). Дубли внутри файла строка видит сама; сюда передаётся лишь множество
    /// идентификаторов, встречающихся минимум в двух файлах версии.
    /// </summary>
    private void UpdateRowWarnings()
    {
        Dictionary<string, HashSet<Guid>> duplicatedByVersion =
            AddinFileDuplicates.FindDuplicatedByVersion(Files.Select(row => row.File));
        foreach (AddinFileRowViewModel row in Files)
        {
            duplicatedByVersion.TryGetValue(row.Version, out HashSet<Guid>? duplicated);
            row.UpdateWarnings(duplicated ?? (IReadOnlySet<Guid>)new HashSet<Guid>());
        }
    }
}
