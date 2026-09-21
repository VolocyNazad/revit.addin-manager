using AddinManager.Core.Guard;

namespace AddinManager.Core.Tests.Guard;

/// <summary>
/// Сторож опрашивает множество версий и поднимает событие только когда оно изменилось —
/// включая смену состава при всё ещё запущенном Revit. Интервал в тестах короткий,
/// ожидания — с запасом, тем же приёмом, что соседние watcher-тесты на реальном диске.
/// </summary>
public sealed class PollingRevitProcessGuardTests
{
    [Fact]
    public void Start_ChecksImmediately()
    {
        using var guard = new PollingRevitProcessGuard(static () => Versions("2024"), TimeSpan.FromHours(1));

        Assert.False(guard.IsRunning);
        guard.Start();

        Assert.True(guard.IsRunning);
        Assert.Equal(Versions("2024"), guard.RunningVersions);
    }

    [Fact]
    public void CheckNow_Transition_RaisesChangedOnce()
    {
        var versions = Versions();
        using var guard = new PollingRevitProcessGuard(() => versions, TimeSpan.FromHours(1));
        guard.Start();
        var raised = 0;
        guard.Changed += (_, _) => raised++;

        versions = Versions("2024");
        guard.CheckNow();

        Assert.True(guard.IsRunning);
        Assert.Equal(Versions("2024"), guard.RunningVersions);
        Assert.Equal(1, raised);

        guard.CheckNow();

        Assert.Equal(1, raised);
    }

    [Fact]
    public void CheckNow_VersionSwapWhileRunning_RaisesChanged()
    {
        var versions = Versions("2024");
        using var guard = new PollingRevitProcessGuard(() => versions, TimeSpan.FromHours(1));
        guard.Start();
        var raised = 0;
        guard.Changed += (_, _) => raised++;

        versions = Versions("2025");
        guard.CheckNow();

        Assert.True(guard.IsRunning);
        Assert.Equal(Versions("2025"), guard.RunningVersions);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Start_Twice_ChecksOnce()
    {
        var probes = 0;
        using var guard = new PollingRevitProcessGuard(
            () =>
            {
                probes++;
                return Versions();
            },
            TimeSpan.FromHours(1));

        guard.Start();
        guard.Start();

        Assert.Equal(1, probes);
    }

    [Fact]
    public void Timer_PollsAutomatically()
    {
        var versions = Versions();
        using var guard = new PollingRevitProcessGuard(() => versions, TimeSpan.FromMilliseconds(50));
        var signal = new ManualResetEventSlim();
        guard.Changed += (_, _) => signal.Set();
        guard.Start();

        versions = Versions("2024", "2025");

        Assert.True(signal.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken), "Опрос должен подхватить запуск Revit без ручного CheckNow.");
        Assert.Equal(Versions("2024", "2025"), guard.RunningVersions);
    }

    [Fact]
    public async Task Dispose_StopsPolling()
    {
        var versions = Versions();
        var guard = new PollingRevitProcessGuard(() => versions, TimeSpan.FromMilliseconds(50));
        var raised = 0;
        guard.Changed += (_, _) => raised++;
        guard.Start();
        guard.Dispose();

        versions = Versions("2024");
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);

        Assert.Equal(0, raised);
        Assert.False(guard.IsRunning);
    }

    [Fact]
    public void ProbeThrows_KeepsPreviousState()
    {
        var calls = 0;
        using var guard = new PollingRevitProcessGuard(
            () =>
            {
                calls++;
                return calls > 1 ? throw new InvalidOperationException("probe failed") : Versions("2024");
            },
            TimeSpan.FromHours(1));
        var raised = 0;
        guard.Changed += (_, _) => raised++;

        guard.Start();
        Assert.True(guard.IsRunning);
        Assert.Equal(1, raised);

        guard.CheckNow();

        Assert.True(guard.IsRunning);
        Assert.Equal(Versions("2024"), guard.RunningVersions);
        Assert.Equal(1, raised);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryFormatRevitYear_NoPath_ReturnsNull(string? path)
    {
        Assert.Null(PollingRevitProcessGuard.TryFormatRevitYear(path));
    }

    [Fact]
    public void TryFormatRevitYear_MissingOrEmptyFile_ReturnsNull()
    {
        Assert.Null(PollingRevitProcessGuard.TryFormatRevitYear(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nope.exe")));

        var empty = Path.GetTempFileName();
        try
        {
            Assert.Null(PollingRevitProcessGuard.TryFormatRevitYear(empty));
        }
        finally
        {
            File.Delete(empty);
        }
    }

    [Fact]
    public void TryFormatRevitYear_VersionedFile_Adds2000ToMajor()
    {
        // Собственная сборка без штамповки версий (см. Directory.Build.props): 1.0.0.0 → "2001".
        var ownAssembly = typeof(PollingRevitProcessGuard).Assembly.Location;

        Assert.Equal("2001", PollingRevitProcessGuard.TryFormatRevitYear(ownAssembly));
    }

    private static IReadOnlySet<string> Versions(params string[] years) =>
        new HashSet<string>(years, StringComparer.Ordinal);
}
