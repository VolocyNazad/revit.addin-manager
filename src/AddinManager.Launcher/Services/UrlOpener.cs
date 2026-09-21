using System.Diagnostics;
using AddinManager.Launcher.Abstractions.Services;

namespace AddinManager.Launcher.Services;

/// <summary>Открывает ссылку через <see cref="Process.Start(ProcessStartInfo)"/> с оболочкой (браузер по умолчанию).</summary>
public sealed class UrlOpener : IUrlOpener
{
    /// <inheritdoc />
    public void Open(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
