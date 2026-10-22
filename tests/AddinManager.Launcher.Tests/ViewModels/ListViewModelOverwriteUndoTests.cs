using System.IO;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Перемещение с заменой цели и отмена через Ctrl+Z на настоящем диске: отмена возвращает и
/// перемещённый файл, и заменённый целевой — байты берутся из сохранённого результата
/// <see cref="IAddinStore.Move"/> и пишутся обратно через <see cref="IAddinStore.Restore"/>,
/// а не напрямую из ViewModel.
/// </summary>
public sealed class ListViewModelOverwriteUndoTests : IDisposable
{
    private readonly TempDirectorySet _tempDirectories = new();
    private readonly IAddinManifestParser _parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());

    [Fact]
    public void Move_WithOverwrite_ThenUndo_RestoresReplacedTarget()
    {
        var userRoot = _tempDirectories.Create();
        var machineRoot = _tempDirectories.Create();
        var sourcePath = WriteManifest(userRoot, "2025", SourceXml());
        var targetPath = WriteManifest(userRoot, "2026", TargetXml());

        var guard = new FakeRevitProcessGuard();
        var store = new FileSystemAddinStore(
            _parser, NullLogger<FileSystemAddinStore>.Instance, guard,
            TestLocalization.For<FileSystemAddinStore>(), userRoot, machineRoot);
        var dialog = new FakeDialogService
        {
            MoveCopyResult = new MoveCopyOptions("2026", AddinScope.User, EnableAfter: true, Overwrite: true),
        };
        var list = ListViewModelFixture.Create(store, guard: guard, dialogService: dialog);
        Assert.Equal(2, list.Files.Count);

        list.MoveToCommand.Execute(list.Files.Single(row => row.Version == "2025"));

        Assert.False(File.Exists(sourcePath));
        Assert.Equal(SourceXml(), File.ReadAllText(targetPath));
        Assert.True(list.HasUndo);

        list.UndoLastActionCommand.Execute(null);

        Assert.True(File.Exists(sourcePath));
        Assert.Equal(SourceXml(), File.ReadAllText(sourcePath));
        Assert.Equal(TargetXml(), File.ReadAllText(targetPath));
        Assert.Equal(2, list.Files.Count);
    }

    private static string WriteManifest(string userRoot, string version, string xml)
    {
        var versionDirectory = Path.Combine(userRoot, "Autodesk", "Revit", "Addins", version);
        Directory.CreateDirectory(versionDirectory);
        var path = Path.Combine(versionDirectory, "A.addin");
        File.WriteAllText(path, xml);
        return path;
    }

    private static string SourceXml() => """
        <RevitAddIns>
          <AddIn Type="Application">
            <Name>Source</Name>
            <Assembly>source.dll</Assembly>
            <AddInId>11111111-1111-1111-1111-111111111111</AddInId>
            <FullClassName>Source.App</FullClassName>
          </AddIn>
        </RevitAddIns>
        """;

    private static string TargetXml() => """
        <RevitAddIns>
          <AddIn Type="Application">
            <Name>Target</Name>
            <Assembly>target.dll</Assembly>
            <AddInId>22222222-2222-2222-2222-222222222222</AddInId>
            <FullClassName>Target.App</FullClassName>
          </AddIn>
        </RevitAddIns>
        """;

    /// <inheritdoc />
    public void Dispose() => _tempDirectories.Dispose();
}
