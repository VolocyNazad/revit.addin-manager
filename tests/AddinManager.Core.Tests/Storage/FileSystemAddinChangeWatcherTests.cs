using AddinManager.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Core.Tests.Storage;

/// <summary>
/// Реальные временные папки + реальный <see cref="FileSystemWatcher"/> (тот же приём,
/// что и <see cref="FileSystemAddinStoreTests"/> — настоящий диск, а не подменённая абстракция
/// файловой системы), поэтому у каждого теста, ожидающего событие, есть щедрый таймаут вместо
/// точной синхронизации: слежение по своей природе асинхронно.
/// </summary>
public sealed class FileSystemAddinChangeWatcherTests : IDisposable
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private readonly TempDirectorySet _tempDirectories = new();
    private readonly List<IDisposable> _watchers = [];

    [Fact]
    public void Changed_FiresAfterNewFileWrittenToUserVersionFolder()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);

        var watcher = NewWatcher(userRoot, NewRoot());
        using var signal = new ManualResetEventSlim(false);
        watcher.Changed += (_, _) => signal.Set();
        watcher.Start();

        File.WriteAllText(Path.Combine(versionDir, "New.addin"), TestManifestXml.ValidAddin("New"));

        Assert.True(signal.Wait(WaitTimeout, TestContext.Current.CancellationToken), "Changed не сработал после появления нового .addin файла.");
    }

    [Fact]
    public void Changed_FiresAfterEditInDisabledFolder()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        var disabledDir = Path.Combine(versionDir, "disabled");
        Directory.CreateDirectory(disabledDir);
        var path = Path.Combine(disabledDir, "A.addin");
        File.WriteAllText(path, TestManifestXml.ValidAddin("A"));

        var watcher = NewWatcher(userRoot, NewRoot());
        using var signal = new ManualResetEventSlim(false);
        watcher.Changed += (_, _) => signal.Set();
        watcher.Start();

        // IncludeSubdirectories должен покрывать disabled/ тем же наблюдателем на корень версий.
        File.WriteAllText(path, TestManifestXml.ValidAddin("A-edited"));

        Assert.True(signal.Wait(WaitTimeout, TestContext.Current.CancellationToken), "Changed не сработал после правки файла в disabled/.");
    }

    [Fact]
    public void Changed_FiresAfterChangeInMachineScope()
    {
        var machineRoot = NewRoot();
        var versionDir = VersionDir(machineRoot, "2025");
        Directory.CreateDirectory(versionDir);

        var watcher = NewWatcher(NewRoot(), machineRoot);
        using var signal = new ManualResetEventSlim(false);
        watcher.Changed += (_, _) => signal.Set();
        watcher.Start();

        File.WriteAllText(Path.Combine(versionDir, "M.addin"), TestManifestXml.ValidAddin("M"));

        Assert.True(signal.Wait(WaitTimeout, TestContext.Current.CancellationToken), "Changed не сработал для машинной области.");
    }

    [Fact]
    public async Task Changed_BurstOfRapidWrites_CoalescesIntoSingleEvent()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);
        var path = Path.Combine(versionDir, "Burst.addin");
        await File.WriteAllTextAsync(path, TestManifestXml.ValidAddin("Burst"), TestContext.Current.CancellationToken);

        var watcher = NewWatcher(userRoot, NewRoot());
        var count = 0;
        using var firstSignal = new ManualResetEventSlim(false);
        watcher.Changed += (_, _) =>
        {
            Interlocked.Increment(ref count);
            firstSignal.Set();
        };
        watcher.Start();

        for (var i = 0; i < 5; i++)
            await File.WriteAllTextAsync(path, TestManifestXml.ValidAddin($"Burst-{i}"), TestContext.Current.CancellationToken);

        Assert.True(firstSignal.Wait(WaitTimeout, TestContext.Current.CancellationToken), "Changed ни разу не сработал на серию записей.");

        // Дебаунс — 300мс; ждём с запасом, чтобы поймать возможное (ошибочное) повторное
        // срабатывание, которого быть не должно.
        await Task.Delay(TimeSpan.FromMilliseconds(800), TestContext.Current.CancellationToken);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Changed_DoesNotFireBeforeStart()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);

        var watcher = NewWatcher(userRoot, NewRoot());
        var fired = false;
        watcher.Changed += (_, _) => fired = true;

        // Намеренно не вызываем Start().
        await File.WriteAllTextAsync(Path.Combine(versionDir, "New.addin"), TestManifestXml.ValidAddin("New"), TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        Assert.False(fired, "Changed сработал до вызова Start().");
    }

    [Fact]
    public async Task Changed_DoesNotFireAfterDispose()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);

        var watcher = new FileSystemAddinChangeWatcher(NullLogger<FileSystemAddinChangeWatcher>.Instance, userRoot, NewRoot());
        var fired = false;
        watcher.Changed += (_, _) => fired = true;
        watcher.Start();
        watcher.Dispose();

        await File.WriteAllTextAsync(Path.Combine(versionDir, "AfterDispose.addin"), TestManifestXml.ValidAddin("AfterDispose"), TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        Assert.False(fired, "Changed сработал после Dispose().");
    }

    [Fact]
    public void Start_MissingBothFolders_DoesNotThrow()
    {
        var watcher = NewWatcher(NewRoot(), NewRoot());

        var ex = Record.Exception(watcher.Start);

        Assert.Null(ex);
    }

    /// <summary>
    /// Регрессия: если папки <c>Autodesk\Revit\Addins</c> нет на момент <see cref="FileSystemAddinChangeWatcher.Start"/>,
    /// слежение должно включиться позже, когда папка появится (раньше флаг "стартовали" выставлялся
    /// до попытки создать наблюдателя, и при отсутствующей папке слежение не включалось уже никогда).
    /// </summary>
    [Fact]
    public async Task Start_MissingAddinsRoot_StartsWatchingWhenRootAppears()
    {
        var userRoot = NewRoot();
        var watcher = new FileSystemAddinChangeWatcher(
            NullLogger<FileSystemAddinChangeWatcher>.Instance,
            userRoot,
            NewRoot(),
            retryInterval: TimeSpan.FromMilliseconds(50));
        _watchers.Add(watcher);
        using var signal = new ManualResetEventSlim(false);
        watcher.Changed += (_, _) => signal.Set();
        watcher.Start();

        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);

        // Момент, когда повторный таймер создаст наблюдателя, не синхронизирован с тестом, поэтому
        // пишем файл, пока Changed не сработает; каждая попытка ждёт дольше дебаунса (300 мс),
        // чтобы событие успело подняться, а не тонуло в бесконечных перезаписях.
        var cancellationToken = TestContext.Current.CancellationToken;
        var deadline = DateTime.UtcNow + WaitTimeout;
        var attempt = 0;
        while (!signal.IsSet && DateTime.UtcNow < deadline)
        {
            await File.WriteAllTextAsync(
                Path.Combine(versionDir, "Retried.addin"),
                TestManifestXml.ValidAddin($"Retried-{attempt++}"),
                cancellationToken);
            signal.Wait(TimeSpan.FromMilliseconds(500), cancellationToken);
        }

        Assert.True(signal.IsSet, "Changed не сработал после появления корня и записи .addin файла.");
    }

    /// <summary>
    /// Когда оба корня уже существуют, повторный таймер не должен взводиться: наблюдатель на корень
    /// ровно один (Information "слежение запущено" по разу на корень), дублей нет.
    /// </summary>
    [Fact]
    public async Task Start_BothRootsPresent_WatchesEachRootOnceWithoutDuplicates()
    {
        var userRoot = NewRoot();
        var machineRoot = NewRoot();
        Directory.CreateDirectory(VersionDir(userRoot, "2025"));
        Directory.CreateDirectory(VersionDir(machineRoot, "2025"));
        var logger = new RecordingLogger<FileSystemAddinChangeWatcher>();
        var watcher = new FileSystemAddinChangeWatcher(
            logger,
            userRoot,
            machineRoot,
            retryInterval: TimeSpan.FromMilliseconds(25));
        _watchers.Add(watcher);

        watcher.Start();

        // Даём повторному таймеру (если бы он ошибочно остался взведённым) шанс создать дубль.
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        Assert.Equal(2, logger.Entries.Count(entry =>
            entry.Level == LogLevel.Information
            && entry.Message.Contains("слежение запущено", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Часть плотного логирования: повторный Start() — не редкий пограничный случай, а обычное
    /// событие в реальном DI-графе (см. ListViewModel, вызывающий Start() один раз за
    /// конструктор, но при повторном создании ViewModel в тестах/хостинге второй Start() на тот
    /// же singleton-сервис вполне возможен) — должен быть виден на уровне Debug, а не проходить
    /// молча (см. FileSystemAddinChangeWatcher.Start).
    /// </summary>
    [Fact]
    public void Start_CalledTwice_SecondCallLogsDebug()
    {
        var logger = new RecordingLogger<FileSystemAddinChangeWatcher>();
        var watcher = new FileSystemAddinChangeWatcher(logger, NewRoot(), NewRoot());
        _watchers.Add(watcher);

        watcher.Start();
        watcher.Start();

        Assert.True(
            logger.HasEntry(LogLevel.Debug, "повторный вызов"),
            "Повторный Start() должен быть залогирован на уровне Debug.");
    }

    [Fact]
    public void Dispose_LogsInformationWithWatcherCount()
    {
        var userRoot = NewRoot();
        Directory.CreateDirectory(VersionDir(userRoot, "2025"));
        var logger = new RecordingLogger<FileSystemAddinChangeWatcher>();
        var watcher = new FileSystemAddinChangeWatcher(logger, userRoot, NewRoot());
        watcher.Start(); // одна папка существует -> один реальный FileSystemWatcher поднят

        watcher.Dispose();

        Assert.True(
            logger.HasEntry(LogLevel.Information, "останавливаем слежение"),
            "Dispose() должен залогировать остановку слежения на уровне Information.");
    }

    private FileSystemAddinChangeWatcher NewWatcher(string userRoot, string machineRoot)
    {
        var watcher = new FileSystemAddinChangeWatcher(NullLogger<FileSystemAddinChangeWatcher>.Instance, userRoot, machineRoot);
        _watchers.Add(watcher);
        return watcher;
    }

    private static string VersionDir(string baseDirectory, string version) =>
        Path.Combine(baseDirectory, "Autodesk", "Revit", "Addins", version);

    private string NewRoot() => _tempDirectories.Create();

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var watcher in _watchers)
            watcher.Dispose();

        _tempDirectories.Dispose();
    }
}
