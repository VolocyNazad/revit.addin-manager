using System.Diagnostics;
using System.Globalization;
using AddinManager.Core.Abstractions.Guard;

namespace AddinManager.Core.Guard;

/// <summary>
/// Опрос запущенных версий: событие — только когда множество изменилось. Делегат вместо
/// прямого опроса процессов — шов для тестов (как делегаты ThemeService/LocalizationService).
/// </summary>
public sealed class PollingRevitProcessGuard : IRevitProcessGuard
{
    /// <summary>Интервал опроса по умолчанию (план, раздел 5 — "poll every ~3s").</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(3);

    private readonly Func<IReadOnlySet<string>> _getRunningVersions;
    private readonly TimeSpan _interval;
    private Timer? _timer;
    private IReadOnlySet<string> _versions = new HashSet<string>(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>Создает сторожа.</summary>
    /// <param name="getRunningVersions">Делегат: годы запущенных версий Revit.</param>
    /// <param name="interval">Интервал опроса. По умолчанию <see cref="DefaultInterval"/>.</param>
    public PollingRevitProcessGuard(Func<IReadOnlySet<string>> getRunningVersions, TimeSpan? interval = null)
    {
        _getRunningVersions = getRunningVersions;
        _interval = interval ?? DefaultInterval;
    }

    /// <inheritdoc />
    public bool IsRunning => _versions.Count != 0;

    /// <inheritdoc />
    public IReadOnlySet<string> RunningVersions => _versions;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_timer is not null)
            return;

        CheckNow();
        _timer = new Timer(static state => ((PollingRevitProcessGuard)state!).CheckNow(), this, _interval, _interval);
    }

    /// <inheritdoc />
    public void CheckNow()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        IReadOnlySet<string> versions;
        try
        {
            versions = _getRunningVersions();
        }
        catch (Exception)
        {
            // Опрос не должен ронять приложение (и тем более таймер): при сбое считаем,
            // что ничего не изменилось, следующая попытка — по расписанию.
            return;
        }

        if (versions.SetEquals(_versions))
            return;

        _versions = versions;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Год версии Revit по пути к <c>Revit.exe</c>: major версии файла плюс 2000
    /// (21 → 2021, ..., 27 → 2027 — схема держится десятилетиями, сработает и для будущих).
    /// <see langword="null"/>, когда путь пуст или версию прочитать нельзя.
    /// </summary>
    /// <param name="executablePath">Полный путь к исполняемому файлу.</param>
    public static string? TryFormatRevitYear(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            return null;

        try
        {
            var major = FileVersionInfo.GetVersionInfo(executablePath).ProductMajorPart;
            return major > 0 ? (2000 + major).ToString(CultureInfo.InvariantCulture) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer?.Dispose();
        _timer = null;
    }
}
