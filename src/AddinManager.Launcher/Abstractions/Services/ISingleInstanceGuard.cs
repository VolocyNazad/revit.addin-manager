namespace AddinManager.Launcher.Abstractions.Services;

/// <summary>Запрет одновременного запуска двух копий приложения.</summary>
public interface ISingleInstanceGuard : IDisposable
{
    /// <summary>Признак того, что текущий процесс — первый экземпляр.</summary>
    bool IsFirstInstance { get; }

    /// <summary>
    /// Запрошена активация первого экземпляра: второй экземпляр уже сигнализировал о
    /// своём запуске. Событие приходит не из потока интерфейса.
    /// </summary>
    event EventHandler? AnotherInstanceStarted;

    /// <summary>Просит первый экземпляр вывести своё окно на передний план.</summary>
    void SignalFirstInstance();
}
