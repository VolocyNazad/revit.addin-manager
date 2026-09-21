using AddinManager.Core.Guard;
using System.Diagnostics;

namespace AddinManager.Launcher
{
    /// <summary>
    /// Revit proccess help instruments
    /// </summary>
    internal static class RevitProccesesHelper
    {

        public static IReadOnlySet<string> DetectRunningRevitVersions()
        {
            var versions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var process in Process.GetProcessesByName("Revit"))
            {
                // Процесс увидели, а версию не прочитали (чужие права, уже завершился) — это всё
                // равно запущенный Revit, помечаем "?" вместо года, а не молчим.
                using (process)
                    versions.Add(RevitProcessYear(process));
            }

            return versions;
        }

        public static string RevitProcessYear(Process process)
        {
            try
            {
                return PollingRevitProcessGuard.TryFormatRevitYear(process.MainModule?.FileName) ?? "?";
            }
            catch (Exception)
            {
                return "?";
            }
        }
    }
}