namespace AddinManager.Launcher.Services;

/// <summary>Просьба показать тост (<see cref="Abstractions.Services.IToastService.ToastRequested"/>).</summary>
public sealed class ToastRequestedEventArgs : EventArgs
{
    /// <summary>Создает просьбу.</summary>
    /// <param name="message">Текст тоста (уже локализован издателем).</param>
    public ToastRequestedEventArgs(string message) => Message = message;

    /// <summary>Текст тоста.</summary>
    public string Message { get; }
}
