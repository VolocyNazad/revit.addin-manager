using System.Diagnostics;
using System.IO;
using AddinManager.Launcher.Abstractions.Services;

namespace AddinManager.Launcher.Services;

/// <summary>Показ через <c>explorer /select</c>: папка открывается, файл подсвечен.</summary>
public sealed class ExplorerFolderOpener : IFolderOpener
{
    /// <inheritdoc />
    public void Reveal(string fullPath) =>
        Process.Start(new ProcessStartInfo(ExplorerPath, $"/select,\"{fullPath}\"") { UseShellExecute = true });

    private static string ExplorerPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
}
