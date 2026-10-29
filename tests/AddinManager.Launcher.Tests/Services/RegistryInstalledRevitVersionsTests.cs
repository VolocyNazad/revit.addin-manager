using AddinManager.Core;
using AddinManager.Launcher.Services;
using Microsoft.Extensions.Logging;

namespace AddinManager.Launcher.Tests.Services;

/// <summary>
/// Проба установленных версий Revit: фильтрация до поддерживаемого каталога, порядок каталога и
/// явный fallback на весь каталог, когда реестр пуст или недоступен.
/// </summary>
public sealed class RegistryInstalledRevitVersionsTests
{
    [Fact]
    public void GetVersions_KeepsSupportedVersionsInCatalogOrder()
    {
        var logger = new RecordingLogger<RegistryInstalledRevitVersions>();
        var probe = new RegistryInstalledRevitVersions(logger, () => ["2026", "2020", "2021", "2025"]);

        Assert.Equal(["2021", "2025", "2026"], probe.GetVersions());
        Assert.True(logger.HasEntry(LogLevel.Debug, "2021, 2025, 2026"));
    }

    [Fact]
    public void GetVersions_MissingRegistryKey_FallsBackToAllSupported()
    {
        var logger = new RecordingLogger<RegistryInstalledRevitVersions>();
        var probe = new RegistryInstalledRevitVersions(logger, () => null);

        Assert.Equal(RevitVersionCatalog.SupportedVersions, probe.GetVersions());
        Assert.True(logger.HasEntry(LogLevel.Warning, "показываю все поддерживаемые версии"));
    }

    [Fact]
    public void GetVersions_NoSupportedKeys_FallsBackToAllSupported()
    {
        var logger = new RecordingLogger<RegistryInstalledRevitVersions>();
        var probe = new RegistryInstalledRevitVersions(logger, () => ["2020", "2019"]);

        Assert.Equal(RevitVersionCatalog.SupportedVersions, probe.GetVersions());
        Assert.True(logger.HasEntry(LogLevel.Warning, "показываю все поддерживаемые версии"));
    }
}
