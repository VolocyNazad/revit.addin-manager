using System.IO;
using AddinManager.Core.Manifests;
using AddinManager.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Встроенное переименование строки списка: F2/меню включают поле с текущим именем, Enter
/// сохраняет через <see cref="AddinManager.Core.Abstractions.Storage.IAddinStore.Rename"/>,
/// Esc отменяет, ошибка сервиса показывается в строке и не меняет имя.
/// </summary>
public sealed class FileRowRenameTests
{
    [Fact]
    public void BeginRename_PutsCurrentNameIntoDraft()
    {
        var row = NewRow("A.addin");

        row.BeginRename();

        Assert.True(row.IsRenaming);
        Assert.Equal("A.addin", row.RenameText);
    }

    [Fact]
    public void CommitRename_RenamesFileAndRaisesRenamed()
    {
        var store = new FakeAddinStore();
        var row = NewRow("A.addin", store);
        var raised = 0;
        row.Renamed += (_, _) => raised++;
        row.BeginRename();
        row.RenameText = "Renamed";

        row.CommitRename();

        Assert.False(row.IsRenaming);
        Assert.Equal("Renamed.addin", row.FileName);
        Assert.Equal(1, raised);
        Assert.Equal(1, store.RenameCallCount);
        Assert.Null(row.ErrorMessage);
    }

    [Fact]
    public void CommitRename_StoreFailure_ShowsErrorAndKeepsOldName()
    {
        var store = new FakeAddinStore { ThrowOnNextRename = new IOException("Не удалось переименовать") };
        var row = NewRow("A.addin", store);
        row.BeginRename();
        row.RenameText = "Renamed";

        row.CommitRename();

        Assert.False(row.IsRenaming);
        Assert.Equal("A.addin", row.FileName);
        Assert.Equal("Не удалось переименовать", row.ErrorMessage);
    }

    [Fact]
    public void CancelRename_KeepsOldName()
    {
        var row = NewRow("A.addin");
        row.BeginRename();
        row.RenameText = "Renamed";

        row.CancelRename();

        Assert.False(row.IsRenaming);
        Assert.Null(row.RenameText);
        Assert.Equal("A.addin", row.FileName);
    }

    [Fact]
    public void BeginRename_WhileRevitIsRunning_DoesNothing()
    {
        var row = NewRow("A.addin");
        row.IsLocked = true;

        row.BeginRename();

        Assert.False(row.IsRenaming);
    }

    private static AddinFileRowViewModel NewRow(string fileName, FakeAddinStore? store = null)
    {
        store ??= new FakeAddinStore();
        var file = new AddinFile(
            fileName,
            AddinScope.User,
            "2025",
            Enabled: true,
            FullPath: @"C:\Addins\2025\" + fileName,
            VersionRootDirectory: @"C:\Addins\2025",
            Manifest: new AddinManifest([], null, null, null, null, [], [], []));
        return new AddinFileRowViewModel(
            store,
            file,
            NullLogger<AddinFileRowViewModel>.Instance,
            TestLocalization.For<AddinFileRowViewModel>());
    }
}
