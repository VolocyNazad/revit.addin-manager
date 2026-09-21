using System.Windows;
using AddinManager.Launcher.Abstractions.Services;

namespace AddinManager.Launcher.Services;

/// <summary>Настоящая реализация <see cref="IUiDispatcher"/> поверх <see cref="Application.Current"/>.</summary>
public sealed class WpfDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    public void Invoke(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }
}
