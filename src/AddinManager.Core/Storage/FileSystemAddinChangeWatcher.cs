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
/// <para>
/// Папки <c>Autodesk\Revit\Addins</c> может ещё не быть на момент <see cref="Start"/> (свежая
/// машина или Revit установили позже), поэтому отсутствующий корень не считается фатальным:
/// повторный таймер пробует подхватить его, пока все корни не окажутся под наблюдением.
/// </para>
/// </remarks>
public sealed class FileSystemAddinChangeWatcher : IAddinChangeWatcher
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan DefaultRetryInterval = TimeSpan.FromSeconds(5);

    private readonly ILogger<FileSystemAddinChangeWatcher> _logger;
    private readonly string _userBaseDirectory;
    private readonly string _machineBaseDirectory;
    private readonly TimeSpan _retryInterval;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly HashSet<string> _watchedRoots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _debounceTimer;
    private readonly Timer _retryTimer;
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
    /// <param name="retryInterval">
    /// Интервал повторных попыток подхватить корень <c>Autodesk\Revit\Addins</c>, которого ещё нет
    /// в момент <see cref="Start"/>; переопределяется в тестах.
    /// </param>
    public FileSystemAddinChangeWatcher(
        ILogger<FileSystemAddinChangeWatcher> logger,
        string? userBaseDirectory = null,
        string? machineBaseDirectory = null,
        TimeSpan? retryInterval = null)
    {
        _logger = logger;
        _userBaseDirectory = userBaseDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _machineBaseDirectory = machineBaseDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        _retryInterval = retryInterval ?? DefaultRetryInterval;

        // Оба таймера стартуют "выключенными": дебаунс — с первым сырым событием, повтор
        // слежения — только если при старте не все корни удалось взять под наблюдение.
        _debounceTimer = new Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);
        _retryTimer = new Timer(OnRetryElapsed, null, Timeout.Infinite, Timeout.Infinite);
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

        TryWatchAllRoots();
    }

    /// <summary>
    /// Пытается взять под наблюдение оба корня и, если хотя бы один ещё не существует, взводит
    /// таймер повтора; когда под наблюдением оба — повтор больше не нужен.
    /// </summary>
    private void TryWatchAllRoots()
    {
        var userRoot = AddinFolderLayout.GetAddinsRoot(_userBaseDirectory);
        var machineRoot = AddinFolderLayout.GetAddinsRoot(_machineBaseDirectory);

        var userWatched = TryWatch(userRoot);
        var machineWatched = TryWatch(machineRoot);

        lock (_gate)
        {
            if (_disposed)
                return;

            if (userWatched && machineWatched)
            {
                _retryTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                return;
            }

            _logger.LogDebug(
                "Watch: не все корни под наблюдением, повтор через {Interval}",
                _retryInterval);
            _retryTimer.Change(_retryInterval, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Ставит наблюдателя на один корень. Возвращает <see langword="true"/>, если корень уже
    /// под наблюдением (в том числе после этой попытки); дублей наблюдателя на один и тот же
    /// корень не бывает.
    /// </summary>
    private bool TryWatch(string addinsRoot)
    {
        lock (_gate)
        {
            if (_disposed)
                return false;

            if (_watchedRoots.Contains(addinsRoot))
                return true;

            // Отсутствующая папка — не ошибка (как и в FileSystemAddinStore.ScanScope): у свежей
            // машины может не быть ни одной версии Revit ни в одном из scope.
            if (!Directory.Exists(addinsRoot))
            {
                _logger.LogDebug("Watch({Root}): папки нет, слежение не запущено", addinsRoot);
                return false;
            }

            FileSystemWatcher watcher;
            try
            {
                watcher = new FileSystemWatcher(addinsRoot, AddinFolderLayout.AddinFilePattern)
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
                return false;
            }

            watcher.Changed += OnRawEvent;
            watcher.Created += OnRawEvent;
            watcher.Deleted += OnRawEvent;
            watcher.Renamed += OnRawEvent;
            watcher.Error += OnError;
            watcher.EnableRaisingEvents = true;

            _watchers.Add(watcher);
            _watchedRoots.Add(addinsRoot);

            _logger.LogInformation("Watch({Root}): слежение запущено", addinsRoot);
            return true;
        }
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

    private void OnRetryElapsed(object? state)
    {
        lock (_gate)
        {
            if (_disposed || !_started)
                return;
        }

        _logger.LogDebug("Watch: повторная попытка запустить слежение");
        TryWatchAllRoots();
    }

    private void OnError(object sender, ErrorEventArgs e) =>
        _logger.LogWarning(e.GetException(), "Watch: FileSystemWatcher сообщил об ошибке (переполнение буфера или доступ пропал)");

    private void OnDebounceElapsed(object? state)
    {
        lock (_gate)
        {
            if (_disposed)
                return;
        }

        _logger.LogDebug("Watch: дебаунс истёк, поднимаем Changed");
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        List<FileSystemWatcher> watchers;
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            watchers = [.. _watchers];
            _watchers.Clear();
            _watchedRoots.Clear();
        }

        // Останавливаем оба таймера до их Dispose: колбэки проверяют _disposed под тем же lock,
        // поэтому ни повторное слежение, ни дебаунс после Dispose уже не взведутся.
        _retryTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _debounceTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        _logger.LogInformation("Dispose: останавливаем слежение ({Count} наблюдателей)", watchers.Count);

        foreach (var watcher in watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnRawEvent;
            watcher.Created -= OnRawEvent;
            watcher.Deleted -= OnRawEvent;
            watcher.Renamed -= OnRawEvent;
            watcher.Error -= OnError;
            watcher.Dispose();
        }

        _retryTimer.Dispose();
        _debounceTimer.Dispose();
    }
}
