using AddinManager.Core;
using AddinManager.Core.Abstractions.Installations;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// Установленные версии Revit для тестов: по умолчанию весь поддерживаемый каталог, чтобы тесты,
/// которым важна не версия, а другая ось, вели себя как раньше.
/// </summary>
public sealed class FakeInstalledRevitVersions(params string[] versions) : IInstalledRevitVersions
{
    /// <summary>Версии, которые вернёт проба; можно заменить, чтобы проверить пересборку чипсов.</summary>
    public IReadOnlyList<string> Versions { get; set; } =
        versions.Length > 0 ? versions : RevitVersionCatalog.SupportedVersions;

    /// <inheritdoc />
    public IReadOnlyList<string> GetVersions() => Versions;
}
