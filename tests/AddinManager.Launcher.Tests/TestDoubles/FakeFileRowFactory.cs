using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Abstractions.Composition;
using AddinManager.Launcher.Composition;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// Считает созданные строки списка и строит настоящие (та же фабрика, что продовая, плюс
/// учёт): тест убеждается, что <see cref="ListViewModel"/> берёт строки из фабрики, а не
/// собирает сам через <c>new</c>.
/// </summary>
public sealed class FakeFileRowFactory : IAddinFileRowViewModelFactory
{
    private readonly IAddinStore _store;

    /// <summary>Файлы, для которых вызвали <see cref="Create"/>.</summary>
    public List<AddinFile> CreatedFiles { get; } = [];

    /// <summary>Создает фейк поверх стора (тот же стор уйдёт и в строки).</summary>
    public FakeFileRowFactory(IAddinStore store) => _store = store;

    /// <inheritdoc />
    public AddinFileRowViewModel Create(AddinFile file)
    {
        CreatedFiles.Add(file);
        return new AddinFileRowViewModel(
            _store, file, NullLogger<AddinFileRowViewModel>.Instance, TestLocalization.For<AddinFileRowViewModel>());
    }
}
