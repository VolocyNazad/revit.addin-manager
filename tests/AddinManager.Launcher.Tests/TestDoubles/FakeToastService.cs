using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.Services;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// Пишет просьбы в список вместо рассылки: тест убеждается, что зона просит тост, а
/// <see cref="Launcher.ViewModels.MainViewModel"/> — что чужую просьбу показывает.
/// </summary>
public sealed class FakeToastService : IToastService
{
    /// <summary>Тексты, с которыми вызвали <see cref="Show"/>.</summary>
    public List<string> ShownMessages { get; } = [];

    /// <inheritdoc />
    public event EventHandler<ToastRequestedEventArgs>? ToastRequested;

    /// <inheritdoc />
    public void Show(string message) => ShownMessages.Add(message);

    /// <summary>Вручную firing <see cref="ToastRequested"/> — имитирует чужую просьбу.</summary>
    public void RaiseToastRequested(string message) => ToastRequested?.Invoke(this, new ToastRequestedEventArgs(message));
}
