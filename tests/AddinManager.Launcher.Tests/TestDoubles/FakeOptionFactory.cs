using AddinManager.Launcher.Abstractions.Composition;
using AddinManager.Launcher.Composition;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// Считает созданные пункты мульти-наборов и строит настоящие: тест убеждается, что
/// <see cref="FormViewModel"/> берёт пункты из фабрики, а не собирает сам через <c>new</c>.
/// </summary>
public sealed class FakeOptionFactory : ISelectableOptionViewModelFactory
{
    /// <summary>Значения, для которых вызвали <see cref="Create"/>.</summary>
    public List<string> CreatedValues { get; } = [];

    /// <inheritdoc />
    public SelectableOptionViewModel Create(string value)
    {
        CreatedValues.Add(value);
        return new SelectableOptionViewModel(value);
    }
}
