using AddinManager.Core.Manifests;
using AddinManager.Core.Storage;

namespace AddinManager.Core.Tests.Storage;

/// <summary>
/// Идентичность файла — ключ стабильности строк между пересканированиями: имя, область и версия,
/// без пути и состояния на диске.
/// </summary>
public sealed class AddinFileIdentityTests
{
    [Fact]
    public void From_File_CopiesNameScopeVersion()
    {
        AddinFile file = NewFile("A.addin", AddinScope.Machine, "2026");

        AddinFileIdentity identity = AddinFileIdentity.From(file);

        Assert.Equal("A.addin", identity.FileName);
        Assert.Equal(AddinScope.Machine, identity.Scope);
        Assert.Equal("2026", identity.Version);
    }

    [Fact]
    public void FileIdentity_DoesNotDependOnPathStateOrManifest()
    {
        AddinFile enabled = NewFile("A.addin", AddinScope.User, "2025");
        AddinFile disabled = enabled with
        {
            Enabled = false,
            FullPath = @"C:\elsewhere\disabled\A.addin",
            Manifest = new AddinManifest([], null, null, null, null, [], [], []),
        };

        Assert.Equal(enabled.Identity, disabled.Identity);
    }

    [Theory]
    [InlineData("B.addin", "2025", AddinScope.User)]
    [InlineData("A.addin", "2026", AddinScope.User)]
    [InlineData("A.addin", "2025", AddinScope.Machine)]
    public void FileIdentity_DifferentNameVersionOrScope_IsNotEqual(string fileName, string version, AddinScope scope)
    {
        AddinFile file = NewFile("A.addin", AddinScope.User, "2025");

        Assert.NotEqual(file.Identity, new AddinFileIdentity(fileName, scope, version));
    }

    [Fact]
    public void From_NullFile_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => AddinFileIdentity.From(null!));
    }

    private static AddinFile NewFile(string fileName, AddinScope scope, string version) =>
        new(fileName, scope, version, Enabled: true,
            FullPath: $@"C:\fake\{version}\{fileName}",
            VersionRootDirectory: $@"C:\fake\{version}",
            Manifest: new AddinManifest([], null, null, null, null, [], [], []));
}
