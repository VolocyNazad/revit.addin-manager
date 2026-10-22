using System.IO;
using AddinManager.Core.Manifests;
using AddinManager.Core.Storage;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Баннер ошибки после пакетного удаления: <see cref="ListViewModel.Refresh"/> обнуляет
/// <c>ErrorMessage</c>, поэтому ошибка частичного удаления должна выставляться после него.
/// </summary>
public sealed class ListViewModelBulkDeleteTests
{
    [Fact]
    public void DeleteSelected_PartialFailure_KeepsFirstErrorVisible()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin"), NewFile("B.addin"));
        store.ThrowOnNextDelete = new IOException("Файл занят");
        var list = ListViewModelFixture.Create(store);
        foreach (var row in list.Files)
            row.IsSelected = true;

        list.DeleteSelected();

        Assert.Equal("Файл занят", list.ErrorMessage);
        Assert.Single(list.Files);
    }

    private static AddinFile NewFile(string fileName) =>
        new(fileName, AddinScope.User, "2025", Enabled: true,
            FullPath: $@"C:\fake\2025\{fileName}",
            VersionRootDirectory: @"C:\fake\2025",
            Manifest: new AddinManifest([], null, null, null, null, [], [], []));
}
