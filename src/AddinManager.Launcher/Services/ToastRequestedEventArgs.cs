namespace AddinManager.Launcher.Services;

/// <summary>Просьба показать тост (<see cref="Abstractions.Services.IToastService.ToastRequested"/>).</summary>
public sealed class ToastRequestedEventArgs(string message) : EventArgs
{
    /// <summary>Текст тоста.</summary>
    public string Message { get; } = message;
}
