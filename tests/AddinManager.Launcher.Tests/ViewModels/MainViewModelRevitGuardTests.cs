using System.IO;
using AddinManager.Theming;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Висящее предупреждение о запущенном Revit (<see cref="MainViewModel.ShowRevitWarning"/>):
/// сторож запускается с моделью, смена состояния приходит через диспетчер, крестик скрывает
/// до закрытия Revit, а остановка сторожа сбрасывает скрытие для следующего запуска.
/// </summary>
public sealed class MainViewModelRevitGuardTests
{
    [Fact]
    public void Constructor_StartsGuardAndReadsInitialState()
    {
        var guard = new FakeRevitProcessGuard
        {
            RunningVersions = new HashSet<string>(["2024"], StringComparer.Ordinal),
        };

        var model = NewModel(guard);

        Assert.Equal(1, guard.StartCallCount);
        Assert.True(model.IsRevitRunning);
        Assert.True(model.ShowRevitWarning);
    }

    [Fact]
    public void GuardChanged_UpdatesStateThroughDispatcher()
    {
        var dispatcher = new RecordingUiDispatcher();
        var guard = new FakeRevitProcessGuard();
        var model = NewModel(guard, dispatcher);
        Assert.False(model.ShowRevitWarning);

        guard.RunningVersions = new HashSet<string>(["2024"], StringComparer.Ordinal);
        guard.RaiseChanged();

        Assert.True(dispatcher.InvokeCount > 0, "Смена состояния должна маршалиться через IUiDispatcher.Invoke.");
        Assert.True(model.IsRevitRunning);
        Assert.True(model.ShowRevitWarning);
    }

    [Fact]
    public void WarningText_ListsRunningVersions()
    {
        TestCulture.RunIn("ru", () =>
        {
            var guard = new FakeRevitProcessGuard
            {
                RunningVersions = new HashSet<string>(["2024", "2025"], StringComparer.Ordinal),
            };
            var model = NewModel(guard);

            Assert.Equal("Revit запущен (2024, 2025) — изменения .addin применятся после его перезапуска.", model.RevitWarningText);
        });
        TestCulture.RunIn("en", () =>
        {
            var guard = new FakeRevitProcessGuard
            {
                RunningVersions = new HashSet<string>(["2024"], StringComparer.Ordinal),
            };
            var model = NewModel(guard);

            Assert.Equal("Revit is running (2024) — .addin changes will apply after you restart it.", model.RevitWarningText);
        });
    }

    [Fact]
    public void Dismiss_HidesWarningUntilRevitStops()
    {
        var guard = new FakeRevitProcessGuard();
        var model = NewModel(guard);
        guard.RunningVersions = new HashSet<string>(["2024"], StringComparer.Ordinal);
        guard.RaiseChanged();
        Assert.True(model.ShowRevitWarning);

        model.DismissRevitWarningCommand.Execute(null);

        Assert.True(model.IsRevitRunning);
        Assert.True(model.IsRevitWarningDismissed);
        Assert.False(model.ShowRevitWarning);
    }

    [Fact]
    public void RevitStopped_ResetsDismissSoNextRunShowsAgain()
    {
        var guard = new FakeRevitProcessGuard();
        var model = NewModel(guard);
        guard.RunningVersions = new HashSet<string>(["2024"], StringComparer.Ordinal);
        guard.RaiseChanged();
        model.DismissRevitWarningCommand.Execute(null);
        Assert.False(model.ShowRevitWarning);

        guard.RunningVersions = new HashSet<string>(StringComparer.Ordinal);
        guard.RaiseChanged();

        Assert.False(model.IsRevitWarningDismissed);

        guard.RunningVersions = new HashSet<string>(["2025"], StringComparer.Ordinal);
        guard.RaiseChanged();

        Assert.True(model.ShowRevitWarning);
    }

    [Fact]
    public void RefreshRevitGuard_ChecksImmediately()
    {
        var guard = new FakeRevitProcessGuard();
        var model = NewModel(guard);

        model.RefreshRevitGuard();

        Assert.Equal(1, guard.CheckNowCallCount);
    }

    private static MainViewModel NewModel(FakeRevitProcessGuard guard) =>
        NewModel(guard, new RecordingUiDispatcher());

    private static MainViewModel NewModel(FakeRevitProcessGuard guard, RecordingUiDispatcher dispatcher) => new(
        new ThemeService(static () => false, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "theme.txt")),
        new FakeLocalizationService(),
        TestLocalization.For<MainViewModel>(),
        dispatcher,
        guard,
        new FakeToastService(),
        new FakeDialogService(),
        new FakeUpdateChecker(),
        new FakeUrlOpener());
}
