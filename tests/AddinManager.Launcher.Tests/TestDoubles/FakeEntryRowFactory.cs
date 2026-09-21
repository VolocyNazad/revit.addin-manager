using AddinManager.Core.Manifests;
using AddinManager.Launcher.Abstractions.Composition;
using AddinManager.Launcher.Composition;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// Считает созданные строки записей и строит настоящие: тест убеждается, что
/// <see cref="EntriesViewModel"/> берёт строки из фабрики, а не собирает сам через <c>new</c>.
/// </summary>
public sealed class FakeEntryRowFactory : IAddinEntryRowViewModelFactory
{
    /// <summary>Пары, для которых вызвали <see cref="Create"/>.</summary>
    public List<(AddinEntry Entry, int Index, bool DuplicateInFile, bool DuplicateAcrossFiles)> CreatedEntries { get; } = [];

    /// <inheritdoc />
    public AddinEntryRowViewModel Create(AddinEntry entry, int index, bool duplicateInFile, bool duplicateAcrossFiles)
    {
        CreatedEntries.Add((entry, index, duplicateInFile, duplicateAcrossFiles));
        return new AddinEntryRowViewModel(entry, index, TestLocalization.For<AddinEntryRowViewModel>(), duplicateInFile, duplicateAcrossFiles);
    }
}
