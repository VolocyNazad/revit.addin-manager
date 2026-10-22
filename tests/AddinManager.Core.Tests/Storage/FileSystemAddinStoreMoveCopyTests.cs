using AddinManager.Core.Abstractions.Guard;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Guard;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using AddinManager.Core.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Core.Tests.Storage;

/// <summary>
/// Перемещение и копирование между версиями/scope: создание папок, сохранение состояния,
/// конфликт цели и возврат заменённого файла для undo.
/// </summary>
public sealed class FileSystemAddinStoreMoveCopyTests : IDisposable
{
    private readonly IAddinManifestParser _parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());
    private readonly string _userRoot = Path.Combine(Path.GetTempPath(), "AddinManagerTests-" + Guid.NewGuid());
    private readonly string _machineRoot = Path.Combine(Path.GetTempPath(), "AddinManagerTests-" + Guid.NewGuid());

    [Fact]
    public void Move_ToNewVersionAndScope_CreatesFoldersAndMovesFile()
    {
        var store = NewStore();
        var source = WriteFile(store, _userRoot, "2025", "A.addin", "A");
        var moved = store.Move(source, "2026", AddinScope.Machine, enabled: true, overwrite: false);

        Assert.Equal("2026", moved.File.Version);
        Assert.Equal(AddinScope.Machine, moved.File.Scope);
        Assert.True(moved.File.Enabled);
        Assert.False(File.Exists(source.FullPath));
        Assert.True(File.Exists(moved.File.FullPath));
        Assert.Contains("disabled", store.GetTargetPath("A.addin", "2025", AddinScope.User, enabled: false));
    }

    [Fact]
    public void Copy_LeavesSourceAndCopiesToTarget()
    {
        var store = NewStore();
        var source = WriteFile(store, _userRoot, "2025", "A.addin", "A");

        var copied = store.Copy(source, "2026", AddinScope.User, enabled: false, overwrite: false).File;

        Assert.True(File.Exists(source.FullPath));
        Assert.True(File.Exists(copied.FullPath));
        Assert.False(copied.Enabled);
        Assert.Contains("disabled", copied.FullPath);
    }

    [Fact]
    public void Move_Overwrite_ReturnsReplacedContent()
    {
        var store = NewStore();
        var source = WriteFile(store, _userRoot, "2025", "A.addin", "A");
        var targetPath = store.GetTargetPath("A.addin", "2026", AddinScope.User, enabled: true);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? string.Empty);
        File.WriteAllText(targetPath, TestManifestXml.ValidAddin("Old"));

        var result = store.Move(source, "2026", AddinScope.User, enabled: true, overwrite: true);

        Assert.Equal(targetPath, result.ReplacedPath);
        Assert.NotNull(result.ReplacedContent);
        Assert.Contains("Old", System.Text.Encoding.UTF8.GetString(result.ReplacedContent));
        Assert.False(File.Exists(source.FullPath));
    }

    [Fact]
    public void Move_ExistingTargetWithoutOverwrite_ThrowsAndKeepsSource()
    {
        var store = NewStore();
        var source = WriteFile(store, _userRoot, "2025", "A.addin", "A");
        var targetPath = store.GetTargetPath("A.addin", "2026", AddinScope.User, enabled: true);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? string.Empty);
        File.WriteAllText(targetPath, TestManifestXml.ValidAddin("Old"));

        Assert.Throws<IOException>(() => store.Move(source, "2026", AddinScope.User, enabled: true, overwrite: false));
        Assert.True(File.Exists(source.FullPath));
    }

    [Fact]
    public void Move_SameFolder_Throws()
    {
        var store = NewStore();
        var source = WriteFile(store, _userRoot, "2025", "A.addin", "A");

        Assert.Throws<IOException>(() => store.Move(source, "2025", AddinScope.User, enabled: true, overwrite: false));
        Assert.True(File.Exists(source.FullPath));
    }

    [Fact]
    public void Copy_Overwrite_ReturnsReplacedContentAndKeepsSource()
    {
        var store = NewStore();
        var source = WriteFile(store, _userRoot, "2025", "A.addin", "A");
        var targetPath = store.GetTargetPath("A.addin", "2026", AddinScope.User, enabled: true);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? string.Empty);
        File.WriteAllText(targetPath, TestManifestXml.ValidAddin("Old"));

        var result = store.Copy(source, "2026", AddinScope.User, enabled: true, overwrite: true);

        Assert.Equal(targetPath, result.ReplacedPath);
        Assert.NotNull(result.ReplacedContent);
        Assert.True(File.Exists(source.FullPath));
        Assert.True(File.Exists(result.File.FullPath));
    }

    [Fact]
    public void Copy_SameFolder_Throws()
    {
        var store = NewStore();
        var source = WriteFile(store, _userRoot, "2025", "A.addin", "A");

        Assert.Throws<IOException>(() => store.Copy(source, "2025", AddinScope.User, enabled: true, overwrite: false));
    }

    [Fact]
    public void Move_ToDisabledTarget_UsesDisabledFolder()
    {
        var store = NewStore();
        var source = WriteFile(store, _userRoot, "2025", "A.addin", "A");

        var result = store.Move(source, "2026", AddinScope.User, enabled: false, overwrite: false);

        Assert.False(result.File.Enabled);
        Assert.Contains("disabled", result.File.FullPath);
        Assert.True(File.Exists(result.File.FullPath));
    }

    [Fact]
    public void Move_RevitRunning_Throws()
    {
        using var guard = new PollingRevitProcessGuard(static () => new HashSet<string>(["2024"], StringComparer.Ordinal));
        guard.Start();
        var store = NewStore(guard);
        var source = WriteFile(store, _userRoot, "2025", "A.addin", "A");

        Assert.Throws<RevitRunningException>(() => store.Move(source, "2026", AddinScope.User, enabled: true, overwrite: false));
        Assert.True(File.Exists(source.FullPath));
    }

    /// <summary>
    /// Замена существующей цели без корзины удаляет её безвозвратно — этот fallback обязан быть
    /// залогирован на уровне Warning, а не пройти молча (development policy).
    /// </summary>
    [Fact]
    public void Move_OverwriteWithoutRecycler_LogsWarningForPermanentDelete()
    {
        var logger = new RecordingLogger<FileSystemAddinStore>();
        var store = NewStore(logger: logger);
        var source = WriteFile(store, _userRoot, "2025", "A.addin", "A");
        var targetPath = store.GetTargetPath("A.addin", "2026", AddinScope.User, enabled: true);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? string.Empty);
        File.WriteAllText(targetPath, TestManifestXml.ValidAddin("Old"));

        store.Move(source, "2026", AddinScope.User, enabled: true, overwrite: true);

        Assert.True(
            logger.HasEntry(LogLevel.Warning, "IFileRecycler не передан"),
            "Permanent-delete fallback при замене цели должен быть залогирован на уровне Warning.");
    }

    [Fact]
    public void TargetExists_ReflectsFileOnDisk()
    {
        var store = NewStore();
        WriteFile(store, _userRoot, "2025", "A.addin", "A");

        Assert.True(store.TargetExists("A.addin", "2025", AddinScope.User, enabled: true));
        Assert.False(store.TargetExists("A.addin", "2026", AddinScope.User, enabled: true));
    }

    private FileSystemAddinStore NewStore(
        IRevitProcessGuard? guard = null,
        ILogger<FileSystemAddinStore>? logger = null) =>
        new(
            _parser,
            logger ?? NullLogger<FileSystemAddinStore>.Instance,
            guard ?? StoppedRevitProcessGuard.Instance,
            TestLocalization.For<FileSystemAddinStore>(),
            _userRoot,
            _machineRoot);

    private static AddinFile WriteFile(FileSystemAddinStore store, string root, string version, string fileName, string name)
    {
        var versionDir = Path.Combine(root, "Autodesk", "Revit", "Addins", version);
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, fileName), TestManifestXml.ValidAddin(name));
        return Assert.Single(store.ScanVersion(version));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var root in new[] { _userRoot, _machineRoot })
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // Временная папка теста — не критично.
            }
        }
    }
}
