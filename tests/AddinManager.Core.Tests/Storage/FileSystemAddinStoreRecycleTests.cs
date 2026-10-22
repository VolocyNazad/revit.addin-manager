using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using AddinManager.Core.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Core.Tests.Storage;

/// <summary>
/// Удаление через <see cref="IFileRecycler"/>: store передаёт путь в Корзину и не трогает файл сам.
/// </summary>
public sealed class FileSystemAddinStoreRecycleTests : IDisposable
{
    private readonly IAddinManifestParser _parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AddinManagerTests-" + Guid.NewGuid());

    [Fact]
    public void Delete_WithRecycler_SendsPathToRecyclerAndKeepsFile()
    {
        var versionDir = Path.Combine(_root, "Autodesk", "Revit", "Addins", "2025");
        Directory.CreateDirectory(versionDir);
        var path = Path.Combine(versionDir, "A.addin");
        File.WriteAllText(path, TestManifestXml.ValidAddin("A"));
        var recycler = new FakeRecycler();
        var store = new FileSystemAddinStore(
            _parser,
            NullLogger<FileSystemAddinStore>.Instance,
            StoppedRevitProcessGuard.Instance,
            TestLocalization.For<FileSystemAddinStore>(),
            _root,
            Path.Combine(_root, "machine"),
            recycler);
        var file = Assert.Single(store.ScanVersion("2025"));

        store.Delete(file);

        Assert.Equal([path], recycler.Paths);
        Assert.True(File.Exists(path));
    }

    /// <summary>
    /// Fallback без <see cref="IFileRecycler"/> (поведение Core-тестов) удаляет файл безвозвратно,
    /// поэтому обязан быть виден в логе на уровне Warning — иначе необратимое удаление пройдёт молча
    /// (см. development policy: fallback must be explicit, documented and logged).
    /// </summary>
    [Fact]
    public void Delete_WithoutRecycler_LogsWarningAndDeletesPermanently()
    {
        var versionDir = Path.Combine(_root, "Autodesk", "Revit", "Addins", "2025");
        Directory.CreateDirectory(versionDir);
        var path = Path.Combine(versionDir, "A.addin");
        File.WriteAllText(path, TestManifestXml.ValidAddin("A"));
        var logger = new RecordingLogger<FileSystemAddinStore>();
        var store = new FileSystemAddinStore(
            _parser,
            logger,
            StoppedRevitProcessGuard.Instance,
            TestLocalization.For<FileSystemAddinStore>(),
            _root,
            Path.Combine(_root, "machine"));
        var file = Assert.Single(store.ScanVersion("2025"));

        var content = store.Delete(file);

        Assert.NotEmpty(content);
        Assert.False(File.Exists(path));
        Assert.True(
            logger.HasEntry(LogLevel.Warning, "IFileRecycler не передан"),
            "Permanent-delete fallback без корзины должен быть залогирован на уровне Warning.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Временная папка теста — не критично, если не удалилась.
        }
    }

    private sealed class FakeRecycler : IFileRecycler
    {
        public List<string> Paths { get; } = [];

        public void Recycle(string path) => Paths.Add(path);
    }

}
