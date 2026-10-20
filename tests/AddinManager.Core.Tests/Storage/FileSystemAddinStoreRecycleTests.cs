using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using AddinManager.Core.Tests.TestDoubles;
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
        File.WriteAllText(path, ValidAddinXml("A"));
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
}
