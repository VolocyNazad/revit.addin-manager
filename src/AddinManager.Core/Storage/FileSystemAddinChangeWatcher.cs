using AddinManager.Core.Abstractions.Storage;
using Microsoft.Extensions.Logging;

namespace AddinManager.Core.Storage;

/// <summary>
/// <see cref="FileSystemWatcher"/>-реализация <see cref="IAddinChangeWatcher"/>: следит за теми
/// же двумя базовыми папками (User/Machine), что сканирует <see cref="FileSystemAddinStore"/>, с
/// <see cref="FileSystemWatcher.IncludeSubdirectories"/> — одного наблюдателя на папку достаточно,
/// чтобы разом покрыть все семь версий Revit и их <c>disabled/</c> (см.
/// <see cref="FileSystemAddinStore.ScanScope"/> — та же пара корней, тот же принцип).
/// </summary>
/// <remarks>
/// Одно логическое изменение на диске обычно поднимает несколько сырых событий подряд
/// (например, наш собственный <see cref="FileAddinMarkupService.Save"/> — это temp-файл,
/// затем <see cref="File.Replace(string, string, string?)"/>, то есть Created/Changed/Renamed
/// почти одновременно). Наружу это должно выглядеть как одно уведомление, а не пачка, поэтому
/// сырые события гасятся одним таймером-дебаунсом: <see cref="Changed"/> поднимается не раньше,
/// чем через <see cref="DebounceInterval"/> после последнего замеченного сырого события.
/// Собственные же сохранения приложения (<see cref="IAddinStore.SetEnabled"/>,
/// <see cref="IAddinMarkupService.Save"/>) этот сервис не отличает от внешних — вызывающая
/// сторона и так вызывает свой explicit refresh сразу после них (см. потребителей), а лишний,
/// но безобидный повторный пересчёт от совпавшего по времени <see cref="Changed"/> — приемлемая
/// цена за то, что не пришлось городить отдельный "это не наша ли запись" фильтр.
/// </remarks>
public sealed class FileSystemAddinChangeWatcher : IAddinChangeWatcher
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(300);

    private readonly ILogger<FileSystemAddinChangeWatcher> _logger;
    private readonly string _userBaseDirectory;
    private readonly string _machineBaseDirectory;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Timer _debounceTimer;
    private readonly Lock _gate = new();

    private bool _started;
    private bool _disposed;

    /// <summary>Создает сервис.</summary>
    /// <param name="logger">Логгер слежения.</param>
    /// <param name="userBaseDirectory">
    /// Корень пользовательской области (обычно %APPDATA%); переопределяется в тестах — тот же
    /// приём, что и у <see cref="FileSystemAddinStore"/>.
    /// </param>
    /// <param name="machineBaseDirectory">Корень машинной области (обычно %PROGRAMDATA%); переопределяется в тестах.</param>
    public FileSystemAddinChangeWatcher(
        ILogger<FileSystemAddinChangeWatcher> logger,
        string? userBaseDirectory = null,
        string? machineBaseDirectory = null)
    {
        _logger = logger;
        _userBaseDirectory = userBaseDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _machineBaseDirectory = machineBaseDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        // Стартует "выключенным" (Timeout.Infinite), первый реальный запуск — из OnRawEvent.
        _debounceTimer = new Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public void Start()
    {
        lock (_gate)
        {
            if (_started || _disposed)
            {
                _logger.LogDebug(
                    "Start: повторный вызов проигнорирован (started={Started}, disposed={Disposed})",
                    _started, _disposed);
                return;
            }

            _started = true;
        }

        TryWatch(Path.Combine(_userBaseDirectory, "Autodesk", "Revit", "Addins"));
        TryWatch(Path.Combine(_machineBaseDirectory, "Autodesk", "Revit", "Addins"));
    }

    private void TryWatch(string addinsRoot)
    {
        // Отсутствующая папка — не ошибка (как и в FileSystemAddinStore.ScanScope): у свежей
        // машины может не быть ни одной версии Revit ни в одном из scope.
        if (!Directory.Exists(addinsRoot))
        {
            _logger.LogDebug("Watch({Root}): папки нет, слежение не запущено", addinsRoot);
            return;
        }

        FileSystemWatcher watcher;
        try
        {
            watcher = new FileSystemWatcher(addinsRoot, "*.addin")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
            };
        }
        catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or IOException)
        {
            // Папка пропала между Directory.Exists и созданием watcher'а, либо путь недоступен —
            // не роняем запуск приложения из-за слежения за диском.
            _logger.LogWarning(ex, "Watch({Root}): не удалось запустить слежение", addinsRoot);
            return;
        }

        watcher.Changed += OnRawEvent;
        watcher.Created += OnRawEvent;
        watcher.Deleted += OnRawEvent;
        watcher.Renamed += OnRawEvent;
        watcher.Error += OnError;
        watcher.EnableRaisingEvents = true;

        lock (_gate)
            _watchers.Add(watcher);

        _logger.LogInformation("Watch({Root}): слежение запущено", addinsRoot);
    }

    private void OnRawEvent(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("Watch: сырое событие {ChangeType} {Path}", e.ChangeType, e.FullPath);

        lock (_gate)
        {
            if (_disposed)
                return;
            _debounceTimer.Change(DebounceInterval, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnError(object sender, ErrorEventArgs e) =>
        _logger.LogWarning(e.GetException(), "Watch: FileSystemWatcher сообщил об ошибке (переполнение буфера или доступ пропал)");

    private void OnDebounceElapsed(object? state)
    {
        _logger.LogDebug("Watch: дебаунс истёк, поднимаем Changed");
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        _logger.LogInformation("Dispose: останавливаем слежение ({Count} наблюдателей)", _watchers.Count);

        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnRawEvent;
            watcher.Created -= OnRawEvent;
            watcher.Deleted -= OnRawEvent;
            watcher.Renamed -= OnRawEvent;
            watcher.Error -= OnError;
            watcher.Dispose();
        }

        _watchers.Clear();
        _debounceTimer.Dispose();
    }
}
