using Microsoft.Win32;

namespace AddinManager.Launcher
{
    /// <summary>
    /// Theme help instruments
    /// </summary>
    internal static class ThemeHelper
    {

        public static bool IsSystemDark() =>
            Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                1) is 0;
    }
}