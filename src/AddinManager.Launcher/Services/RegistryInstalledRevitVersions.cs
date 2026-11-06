using System.IO;
using System.Security;
using AddinManager.Core;
using AddinManager.Core.Abstractions.Installations;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace AddinManager.Launcher.Services;

/// <summary>
/// Reads the installed Revit versions from <c>HKLM\SOFTWARE\Autodesk\Revit</c>: Autodesk creates one
/// subkey per installed version, and the key is independent of the install location, so a custom
/// Program Files path does not break detection.
/// </summary>
public sealed class RegistryInstalledRevitVersions : IInstalledRevitVersions
{
    private const string RevitRegistryKeyPath = @"SOFTWARE\Autodesk\Revit";

    private readonly ILogger<RegistryInstalledRevitVersions> _logger;
    private readonly Func<IReadOnlyCollection<string>?> _readVersionKeys;

    /// <summary>Creates the probe over the real machine registry.</summary>
    /// <param name="logger">Describes the detected versions and the fallback reason.</param>
    public RegistryInstalledRevitVersions(ILogger<RegistryInstalledRevitVersions> logger)
        : this(logger, ReadRegistryVersionKeys)
    {
    }

    /// <summary>
    /// Creates the probe over a supplied set of version keys instead of the registry — the test seam
    /// for the filtering and fallback rules (<see langword="null"/> stands for a missing registry key).
    /// </summary>
    /// <param name="logger">Describes the detected versions and the fallback reason.</param>
    /// <param name="readVersionKeys">Reads the version subkey names; the composition root supplies the registry reader.</param>
    public RegistryInstalledRevitVersions(
        ILogger<RegistryInstalledRevitVersions> logger,
        Func<IReadOnlyCollection<string>?> readVersionKeys)
    {
        _logger = logger;
        _readVersionKeys = readVersionKeys;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetVersions()
    {
        try
        {
            if (_readVersionKeys() is { } keys)
            {
                var installedKeys = new HashSet<string>(keys, StringComparer.Ordinal);
                var installed = RevitVersionCatalog.SupportedVersions
                    .Where(installedKeys.Contains)
                    .ToList();
                if (installed.Count > 0)
                {
                    _logger.LogDebug(
                        "Установленные версии Revit: {Versions}",
                        string.Join(", ", installed));
                    return installed;
                }

                _logger.LogWarning(
                    "В реестре нет ни одной поддерживаемой версии Revit — показываю все поддерживаемые версии");
            }
            else
            {
                _logger.LogWarning(
                    "Ключ реестра {RegistryPath} не найден — показываю все поддерживаемые версии Revit",
                    RevitRegistryKeyPath);
            }
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            _logger.LogWarning(
                ex,
                "Не удалось прочитать установленные версии Revit из реестра — показываю все поддерживаемые версии");
        }

        return RevitVersionCatalog.SupportedVersions;
    }

    /// <summary>Reads the Revit version subkey names from the machine registry; a missing key yields <see langword="null"/>.</summary>
    private static IReadOnlyCollection<string>? ReadRegistryVersionKeys()
    {
        using RegistryKey? root = Registry.LocalMachine.OpenSubKey(RevitRegistryKeyPath);
        return root?.GetSubKeyNames();
    }
}
