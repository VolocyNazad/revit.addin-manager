using AddinManager.Launcher.Abstractions.Services;

namespace AddinManager.Launcher.Services;

/// <summary>
/// Одиночный запуск поверх именованного <see cref="Mutex"/>: первый экземпляр владеет
/// мьютексом и слушает именованное событие активации, второй сигнализирует и завершается.
/// </summary>
public sealed class SingleInstanceGuard : ISingleInstanceGuard
{
    private const string MutexName = @"Local\Volocy.Revit.AddinManager.SingleInstance";
    private const string ActivationEventName = @"Local\Volocy.Revit.AddinManager.ShowFirstInstance";

    private readonly string _mutexName;
    private readonly string _activationEventName;
    private readonly Mutex? _mutex;
    private readonly EventWaitHandle? _activationEvent;
    private readonly CancellationTokenSource _cancellation = new();
    private bool _disposed;

    /// <summary>Создаёт защиту с именами объектов по умолчанию для приложения.</summary>
    public SingleInstanceGuard()
        : this(MutexName, ActivationEventName)
    {
    }

    /// <summary>Создаёт защиту с заданными именами объектов (шов для тестов).</summary>
    /// <param name="mutexName">Имя мьютекса одиночного экземпляра.</param>
    /// <param name="activationEventName">Имя события запроса активации.</param>
    public SingleInstanceGuard(string mutexName, string activationEventName)
    {
        _mutexName = mutexName;
        _activationEventName = activationEventName;

        Mutex mutex;
        bool createdNew;
        try
        {
            mutex = new Mutex(true, _mutexName, out createdNew);
        }
        catch (AbandonedMutexException)
        {
            // Предыдущий процесс упал, не освободив мьютекс: владение перешло нам.
            mutex = new Mutex(true, _mutexName, out createdNew);
            createdNew = true;
        }

        IsFirstInstance = createdNew;

        if (IsFirstInstance)
        {
            _mutex = mutex;
            _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, _activationEventName);
            _ = Task.Run(ListenForActivationRequests, _cancellation.Token);
        }
        else
        {
            mutex.Dispose();
        }
    }

    /// <inheritdoc />
    public bool IsFirstInstance { get; }

    /// <inheritdoc />
    public event EventHandler? AnotherInstanceStarted;

    /// <inheritdoc />
    public void SignalFirstInstance()
    {
        try
        {
            using var signal = EventWaitHandle.OpenExisting(_activationEventName);
            signal.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // Первый экземпляр уже закрылся между проверкой мьютекса и сигналом: сигнализировать некому.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();

        if (IsFirstInstance)
        {
            try
            {
                _activationEvent?.Set();
            }
            catch (ObjectDisposedException)
            {
                // Событие уже закрыто: слушателю больше нечего ждать.
            }

            _activationEvent?.Dispose();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }

        _cancellation.Dispose();
    }

    private void ListenForActivationRequests()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            try
            {
                _activationEvent?.WaitOne();
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (!_cancellation.IsCancellationRequested)
            {
                AnotherInstanceStarted?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
