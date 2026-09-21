using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.Services;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// Выполняет действие немедленно и синхронно (тестам не нужен настоящий WPF
/// <see cref="System.Windows.Threading.Dispatcher"/>/поток UI), но считает вызовы — так тест
/// может убедиться, что маршалинг вообще происходит через этот сервис, а не мимо него.
/// </summary>
public sealed class RecordingUiDispatcher : IUiDispatcher
{
    /// <summary>Сколько раз вызвали <see cref="Invoke"/>.</summary>
    public int InvokeCount { get; private set; }

    /// <inheritdoc />
    public void Invoke(Action action)
    {
        InvokeCount++;
        action();
    }
}
