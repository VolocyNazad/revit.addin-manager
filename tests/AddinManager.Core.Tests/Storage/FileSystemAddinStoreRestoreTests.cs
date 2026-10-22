using System.Text;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Guard;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using AddinManager.Core.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Core.Tests.Storage;

/// <summary>
/// Восстановление файла по сохранённым байтам (отмена удаления или замены цели): атомарная запись,
/// создание недостающих папок, отказ перезаписывать существующий файл и запрет при живом Revit.
/// </summary>
public sealed class FileSystemAddinStoreRestoreTests : IDisposable
{
    private readonly TempDirectorySet _tempDirectories = new();
    private readonly IAddinManifestParser _parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());

    [Fact]
    public void Restore_WritesBytesAndCreatesMissingFolders()
    {
        var userRoot = NewRoot();
        var path = Path.Combine(userRoot, "Autodesk", "Revit", "Addins", "2025", "Restored.addin");
        var content = Encoding.UTF8.GetBytes(TestManifestXml.ValidAddin("Restored"));
        var store = NewStore(userRoot);

        store.Restore(path, content);

        Assert.Equal(content, File.ReadAllBytes(path));
    }

    [Fact]
    public void Restore_ExistingFile_ThrowsAndKeepsOriginal()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);
        var path = Path.Combine(versionDir, "Existing.addin");
        var original = TestManifestXml.ValidAddin("Original");
        File.WriteAllText(path, original);
        var store = NewStore(userRoot);

        var exception = Assert.Throws<IOException>(() => store.Restore(path, [1, 2, 3]));

        Assert.Contains("Existing.addin", exception.Message);
        Assert.Equal(original, File.ReadAllText(path));
    }

    [Fact]
    public void Restore_RevitRunning_ThrowsAndWritesNothing()
    {
        var userRoot = NewRoot();
        var path = Path.Combine(userRoot, "Autodesk", "Revit", "Addins", "2025", "Restored.addin");
        using var guard = new PollingRevitProcessGuard(
            static () => new HashSet<string>(["2024"], StringComparer.Ordinal));
        guard.Start();
        var store = new FileSystemAddinStore(
            _parser,
            NullLogger<FileSystemAddinStore>.Instance,
            guard,
            TestLocalization.For<FileSystemAddinStore>(),
            userRoot,
            NewRoot());

        Assert.Throws<RevitRunningException>(() => store.Restore(path, [1, 2, 3]));
        Assert.False(File.Exists(path));
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)!));
    }

    [Fact]
    public void Restore_Succeeds_LogsInformationWithLength()
    {
        var userRoot = NewRoot();
        var path = Path.Combine(userRoot, "Autodesk", "Revit", "Addins", "2025", "Restored.addin");
        var content = Encoding.UTF8.GetBytes(TestManifestXml.ValidAddin("Restored"));
        var logger = new RecordingLogger<FileSystemAddinStore>();
        var store = NewStore(userRoot, logger);

        store.Restore(path, content);

        Assert.True(
            logger.HasEntry(LogLevel.Information, "восстановлено"),
            "Успешный Restore должен залогировать факт восстановления на уровне Information.");
        Assert.True(
            logger.HasEntry(LogLevel.Information, $"{content.Length} байт"),
            "Успешный Restore должен залогировать длину записанного содержимого.");
    }

    [Fact]
    public void Restore_TargetIsDirectory_LogsErrorWrapsExceptionAndCleansTempFile()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);
        var path = Path.Combine(versionDir, "Blocked.addin");
        Directory.CreateDirectory(path); // Цель — папка: File.Move в неё невозможен.
        var logger = new RecordingLogger<FileSystemAddinStore>();
        var store = NewStore(userRoot, logger);

        var exception = Assert.Throws<IOException>(() => store.Restore(path, [1, 2, 3]));

        Assert.IsAssignableFrom<IOException>(exception.InnerException);
        Assert.True(
            logger.HasEntry(LogLevel.Error, "не удалось восстановить"),
            "Сбой восстановления должен быть залогирован на уровне Error.");
        Assert.Empty(Directory.GetFiles(versionDir, "*.tmp", SearchOption.TopDirectoryOnly));
    }

    [Theory]
    [InlineData("en", "already exists and was not overwritten")]
    [InlineData("ru", "уже существует и не был перезаписан")]
    public void Restore_ExistingFile_MessageFollowsCulture(string culture, string expected)
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);
        var path = Path.Combine(versionDir, "Existing.addin");
        File.WriteAllText(path, TestManifestXml.ValidAddin("Original"));
        var store = NewStore(userRoot);

        TestCulture.RunIn(culture, () =>
        {
            var exception = Assert.Throws<IOException>(() => store.Restore(path, [1]));

            Assert.Contains(expected, exception.Message);
        });
    }

    [Theory]
    [InlineData("en", "File name cannot be empty")]
    [InlineData("ru", "Имя файла не может быть пустым")]
    public void CreateFile_EmptyName_MessageFollowsCulture(string culture, string expected)
    {
        var store = NewStore(NewRoot());

        TestCulture.RunIn(culture, () =>
        {
            var exception = Assert.Throws<ArgumentException>(
                () => store.CreateFile("   ", "2025", AddinScope.User, disabled: false));

            Assert.Contains(expected, exception.Message);
        });
    }

    private FileSystemAddinStore NewStore(string userRoot, ILogger<FileSystemAddinStore>? logger = null) =>
        new(
            _parser,
            logger ?? NullLogger<FileSystemAddinStore>.Instance,
            StoppedRevitProcessGuard.Instance,
            TestLocalization.For<FileSystemAddinStore>(),
            userRoot,
            NewRoot());

    private static string VersionDir(string baseDirectory, string version) =>
        Path.Combine(baseDirectory, "Autodesk", "Revit", "Addins", version);

    private string NewRoot() => _tempDirectories.Create();

    /// <inheritdoc />
    public void Dispose()
    {
        _tempDirectories.Dispose();
    }
}
