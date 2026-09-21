using AddinManager.Launcher.Services;

namespace AddinManager.Launcher.Abstractions.Services;

/// <summary>
/// Шина тостов-подтверждений: издатель (зона списка после ручного обновления) не знает,
/// кто показывает, подписчик (главное окно) — кто просит. Реализация заменяема через DI.
/// </summary>
public interface IToastService
{
    /// <summary>Просят показать тост.</summary>
    event EventHandler<ToastRequestedEventArgs>? ToastRequested;

    /// <summary>Попросить показать тост с готовым текстом.</summary>
    /// <param name="message">Текст тоста (уже локализован издателем).</param>
    void Show(string message);
}
