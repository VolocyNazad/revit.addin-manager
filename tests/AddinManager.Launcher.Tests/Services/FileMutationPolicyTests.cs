using AddinManager.Core.Manifests;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Services;

namespace AddinManager.Launcher.Tests.Services;

/// <summary>
/// Машинная область (<c>%PROGRAMDATA%</c>) меняется только приложением с правами администратора:
/// без них файл доступен только для чтения, и операции блокируются заранее.
/// </summary>
public sealed class FileMutationPolicyTests
{
    [Fact]
    public void MachineScope_NotElevated_IsReadOnly()
    {
        var policy = new FileMutationPolicy(isElevated: false);

        Assert.True(policy.IsReadOnly(NewFile(AddinScope.Machine)));
    }

    [Fact]
    public void MachineScope_Elevated_IsWritable()
    {
        var policy = new FileMutationPolicy(isElevated: true);

        Assert.False(policy.IsReadOnly(NewFile(AddinScope.Machine)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UserScope_IsAlwaysWritable(bool isElevated)
    {
        var policy = new FileMutationPolicy(isElevated);

        Assert.False(policy.IsReadOnly(NewFile(AddinScope.User)));
    }

    private static AddinFile NewFile(AddinScope scope) =>
        new("Plugin.addin", scope, "2024", Enabled: true,
            FullPath: @"C:\fake\2024\Plugin.addin",
            VersionRootDirectory: @"C:\fake\2024",
            Manifest: new AddinManifest([], null, null, null, null, [], [], []));
}
