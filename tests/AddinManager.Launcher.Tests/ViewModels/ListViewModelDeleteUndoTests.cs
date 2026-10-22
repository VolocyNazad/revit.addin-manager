using System.IO;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Удаление строки и его отмена на настоящем диске: байты восстановленного файла пишутся через
/// <see cref="IAddinStore.Restore"/>, а не из view model. Тот же путь, что у перемещения с
/// заменой цели (<see cref="ListViewModelOverwriteUndoTests"/>).
/// </summary>
public sealed class ListViewModelDeleteUndoTests : IDisposable
{
    private readonly TempDirectorySet _tempDirectories = new();
    private readonly IAddinManifestParser _parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());

    [Fact]
    public void Delete_ThenUndo_RestoresFileContent()
    {
        var userRoot = _tempDirectories.Create();
        var versionDirectory = Path.Combine(userRoot, "Autodesk", "Revit", "Addins", "2025");
        Directory.CreateDirectory(versionDirectory);
        var path = Path.Combine(versionDirectory, "A.addin");
        var content = TestManifestXml.ValidAddin("A");
        File.WriteAllText(path, content);

        var guard = new FakeRevitProcessGuard();
        var store = new FileSystemAddinStore(
            _parser, NullLogger<FileSystemAddinStore>.Instance, guard,
            TestLocalization.For<FileSystemAddinStore>(), userRoot, _tempDirectories.Create());
        var list = ListViewModelFixture.Create(store, guard: guard);
        Assert.Single(list.Files);

        list.Files.Single().DeleteCommand.Execute(null);

        Assert.False(File.Exists(path));
        Assert.True(list.HasUndo);

        list.UndoLastActionCommand.Execute(null);

        Assert.True(File.Exists(path));
        Assert.Equal(content, File.ReadAllText(path));
    }

    /// <inheritdoc />
    public void Dispose() => _tempDirectories.Dispose();
}
