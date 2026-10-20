using AddinManager.Core.Abstractions.Guard;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Guard;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using AddinManager.Core.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Core.Tests.Storage;

/// <summary>
/// Переименование .addin на месте: файл остаётся в своей папке (корень версии или disabled/),
/// получает расширение .addin при необходимости, а пустое/недопустимое имя и занятое имя
/// отклоняются, не трогая исходный файл.
/// </summary>
public sealed class FileSystemAddinStoreRenameTests : IDisposable
{
    private readonly List<string> _tempRoots = [];
    private readonly IAddinManifestParser _parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());

    [Fact]
    public void Rename_DisabledFile_KeepsDisabledFolder_AndAppendsExtension()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        var disabledDir = Path.Combine(versionDir, "disabled");
        Directory.CreateDirectory(versionDir);
        Directory.CreateDirectory(disabledDir);
        File.WriteAllText(Path.Combine(disabledDir, "A.addin"), ValidAddinXml("A"));
        var store = NewStore(userRoot);
        var file = Assert.Single(store.ScanVersion("2025"));

        var renamed = store.Rename(file, "Renamed");

        Assert.Equal("Renamed.addin", renamed.FileName);
        Assert.False(renamed.Enabled);
        Assert.Equal(Path.Combine(disabledDir, "Renamed.addin"), renamed.FullPath);
        Assert.False(File.Exists(Path.Combine(disabledDir, "A.addin")));
        Assert.True(File.Exists(Path.Combine(disabledDir, "Renamed.addin")));
    }

    [Fact]
    public void Rename_ToExistingName_ThrowsAndKeepsOriginalFile()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "A.addin"), ValidAddinXml("A"));
        File.WriteAllText(Path.Combine(versionDir, "B.addin"), ValidAddinXml("B"));
        var store = NewStore(userRoot);
        var file = Assert.Single(store.ScanVersion("2025"), f => f.FileName == "A.addin");

        Assert.Throws<IOException>(() => store.Rename(file, "B.addin"));

        Assert.True(File.Exists(Path.Combine(versionDir, "A.addin")));
        Assert.True(File.Exists(Path.Combine(versionDir, "B.addin")));
    }

    [Fact]
    public void Rename_WhitespaceName_ThrowsAndKeepsOriginalFile()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "A.addin"), ValidAddinXml("A"));
        var store = NewStore(userRoot);
        var file = Assert.Single(store.ScanVersion("2025"));

        Assert.Throws<IOException>(() => store.Rename(file, "   "));

        Assert.True(File.Exists(Path.Combine(versionDir, "A.addin")));
    }

    [Fact]
    public void Rename_InvalidCharacters_ThrowsAndKeepsOriginalFile()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "A.addin"), ValidAddinXml("A"));
        var store = NewStore(userRoot);
        var file = Assert.Single(store.ScanVersion("2025"));

        Assert.Throws<IOException>(() => store.Rename(file, "bad/name.addin"));

        Assert.True(File.Exists(Path.Combine(versionDir, "A.addin")));
    }

    [Fact]
    public void Rename_RevitRunning_Throws()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "A.addin"), ValidAddinXml("A"));
        using var guard = new PollingRevitProcessGuard(static () => new HashSet<string>(["2024"], StringComparer.Ordinal));
        guard.Start();
        var store = NewStore(userRoot, guard);
        var file = Assert.Single(store.ScanVersion("2025"));

        Assert.Throws<RevitRunningException>(() => store.Rename(file, "Renamed"));
    }

    private FileSystemAddinStore NewStore(string userRoot, IRevitProcessGuard? guard = null) =>
        new(
            _parser,
            NullLogger<FileSystemAddinStore>.Instance,
            guard ?? StoppedRevitProcessGuard.Instance,
            TestLocalization.For<FileSystemAddinStore>(),
            userRoot,
            NewRoot());

    private static string VersionDir(string baseDirectory, string version) =>
        Path.Combine(baseDirectory, "Autodesk", "Revit", "Addins", version);

    private string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "AddinManagerTests-" + Guid.NewGuid());
        _tempRoots.Add(root);
        return root;
    }

    private static string ValidAddinXml(string name) => $"""
        <RevitAddIns>
          <AddIn Type="Application">
            <Name>{name}</Name>
            <Assembly>a.dll</Assembly>
            <AddInId>11111111-1111-1111-1111-111111111111</AddInId>
            <FullClassName>A.App</FullClassName>
          </AddIn>
        </RevitAddIns>
        """;

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var root in _tempRoots)
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // Временная папка теста — не критично, если не удалилась.
            }
            catch (UnauthorizedAccessException)
            {
                // Временная папка теста — не критично, если не удалилась.
            }
        }
    }
}
