using AddinManager.Launcher.Abstractions.Composition;
using AddinManager.Launcher.ViewModels;

namespace AddinManager.Launcher.Composition;

/// <summary>Фабрика пунктов мульти-наборов: пункт не зависит от сервисов, только от значения.</summary>
public sealed class SelectableOptionViewModelFactory : ISelectableOptionViewModelFactory
{
    /// <inheritdoc />
    public SelectableOptionViewModel Create(string value) => new(value);
}
