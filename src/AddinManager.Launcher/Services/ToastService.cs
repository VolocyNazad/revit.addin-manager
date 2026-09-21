using AddinManager.Launcher.Abstractions.Services;

namespace AddinManager.Launcher.Services;

/// <summary>Шина тостов: только рассылает просьбы подписчикам, ничего не показывает сама.</summary>
public sealed class ToastService : IToastService
{
    /// <inheritdoc />
    public event EventHandler<ToastRequestedEventArgs>? ToastRequested;

    /// <inheritdoc />
    public void Show(string message) => ToastRequested?.Invoke(this, new ToastRequestedEventArgs(message));
}
