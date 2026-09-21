using AddinManager.Core.Manifests;
using AddinManager.Launcher.Abstractions.Composition;
using AddinManager.Launcher.ViewModels;
using Microsoft.Extensions.Localization;

namespace AddinManager.Launcher.Composition;

/// <summary>Фабрика строк записей: зависимости — из контейнера, запись — параметрами.</summary>
/// <param name="localizer">Строки строк записей.</param>
public sealed class AddinEntryRowViewModelFactory(
    IStringLocalizer<AddinEntryRowViewModel> localizer) : IAddinEntryRowViewModelFactory
{
    /// <inheritdoc />
    public AddinEntryRowViewModel Create(AddinEntry entry, int index, bool duplicateInFile, bool duplicateAcrossFiles) =>
        new(entry, index, localizer, duplicateInFile, duplicateAcrossFiles);
}
