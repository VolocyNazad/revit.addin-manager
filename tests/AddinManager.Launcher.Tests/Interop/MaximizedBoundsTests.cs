using System.Windows;
using AddinManager.Launcher.Interop;

namespace AddinManager.Launcher.Tests.Interop;

/// <summary>
/// Максимизированное окно должно занимать рабочую область монитора, а не весь монитор: иначе
/// нижняя полоса окна уходит под панель задач (см. MaximizedBounds).
/// </summary>
public sealed class MaximizedBoundsTests
{
    [Fact]
    public void NoTaskbar_FillsTheMonitor()
    {
        var bounds = MaximizedBounds.FromMonitor(
            new Int32Rect(0, 0, 1920, 1080),
            new Int32Rect(0, 0, 1920, 1080));

        Assert.Equal(new Int32Rect(0, 0, 1920, 1080), bounds);
    }

    [Fact]
    public void TaskbarAtBottom_StopsAboveIt()
    {
        var bounds = MaximizedBounds.FromMonitor(
            new Int32Rect(0, 0, 1920, 1080),
            new Int32Rect(0, 0, 1920, 1040));

        Assert.Equal(new Int32Rect(0, 0, 1920, 1040), bounds);
    }

    [Fact]
    public void TaskbarAtTop_StartsBelowIt()
    {
        var bounds = MaximizedBounds.FromMonitor(
            new Int32Rect(0, 0, 1920, 1080),
            new Int32Rect(0, 40, 1920, 1040));

        Assert.Equal(new Int32Rect(0, 40, 1920, 1040), bounds);
    }

    [Fact]
    public void TaskbarAtLeft_StartsRightOfIt()
    {
        var bounds = MaximizedBounds.FromMonitor(
            new Int32Rect(0, 0, 1920, 1080),
            new Int32Rect(80, 0, 1840, 1080));

        Assert.Equal(new Int32Rect(80, 0, 1840, 1080), bounds);
    }

    [Fact]
    public void SecondaryMonitor_OffsetIsRelativeToThatMonitor()
    {
        var bounds = MaximizedBounds.FromMonitor(
            new Int32Rect(1920, 0, 2560, 1440),
            new Int32Rect(1920, 0, 2560, 1400));

        Assert.Equal(new Int32Rect(0, 0, 2560, 1400), bounds);
    }

    [Fact]
    public void MonitorLeftOfPrimary_OffsetIsRelativeToThatMonitor()
    {
        var bounds = MaximizedBounds.FromMonitor(
            new Int32Rect(-1920, 0, 1920, 1080),
            new Int32Rect(-1920, 0, 1920, 1040));

        Assert.Equal(new Int32Rect(0, 0, 1920, 1040), bounds);
    }
}
