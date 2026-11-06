using System.Windows;

namespace AddinManager.Launcher.Interop;

/// <summary>
/// Положение и размер максимизированного окна в координатах монитора: рабочая область (монитор
/// минус панель задач), а не весь монитор.
/// </summary>
/// <remarks>
/// Окно с <c>WindowStyle="None"</c> и <c>WindowChrome</c> разворачивается на весь монитор, поэтому
/// нижняя полоса окна уходила под панель задач. Windows ожидает в <c>WM_GETMINMAXINFO</c> смещение
/// рабочей области относительно начала монитора и её размер — здесь считается только эта
/// арифметика, вызов Win32 остаётся в окне.
/// </remarks>
internal static class MaximizedBounds
{
    /// <summary>Возвращает положение и размер максимизированного окна на одном мониторе.</summary>
    /// <param name="monitor">Прямоугольник монитора в координатах экрана.</param>
    /// <param name="workArea">Прямоугольник рабочей области того же монитора в координатах экрана.</param>
    /// <returns>Смещение рабочей области от начала монитора и её размер.</returns>
    internal static Int32Rect FromMonitor(Int32Rect monitor, Int32Rect workArea)
    {
        return new Int32Rect(
            Math.Abs(workArea.X - monitor.X),
            Math.Abs(workArea.Y - monitor.Y),
            workArea.Width,
            workArea.Height);
    }
}
