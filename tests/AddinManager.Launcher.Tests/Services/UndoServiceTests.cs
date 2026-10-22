using System.IO;
using AddinManager.Core.Manifests;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Services;
using Microsoft.Extensions.Logging;

namespace AddinManager.Launcher.Tests.Services;

/// <summary>
/// Одноуровневый стек отмены: хранит последнее действие, выполняет его через store и возвращает
/// идентичность файла для восстановления выбора. Перечитывание списка и тосты — не его дело.
/// </summary>
public sealed class UndoServiceTests
{
    [Fact]
    public void Record_SetsHasUndoAndRaisesChanged()
    {
        var store = new FakeAddinStore();
        var service = NewService(store);
        var changed = 0;
        service.Changed += (_, _) => changed++;

        service.Record(new UndoToggle(NewFile("A.addin", "2025", AddinScope.User), PreviousEnabled: true));

        Assert.True(service.HasUndo);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Record_ReplacesPreviousAction()
    {
        var store = new FakeAddinStore();
        var service = NewService(store);
        var first = NewFile("First.addin", "2025", AddinScope.User);
        var second = NewFile("Second.addin", "2026", AddinScope.User);

        service.Record(new UndoToggle(first, PreviousEnabled: true));
        service.Record(new UndoToggle(second, PreviousEnabled: false));
        AddinFileIdentity identity = service.Undo(_ => first);

        Assert.Equal(second.Identity, identity);
        Assert.False(service.HasUndo);
    }

    [Fact]
    public void Undo_WithoutRecord_Throws()
    {
        var service = NewService(new FakeAddinStore());

        Assert.Throws<InvalidOperationException>(() => service.Undo(_ => null));
    }

    [Fact]
    public void Undo_Toggle_RestoresPreviousStateAndReturnsIdentity()
    {
        var store = new FakeAddinStore();
        AddinFile file = NewFile("A.addin", "2025", AddinScope.User);
        store.SetFiles("2025", file with { Enabled = false });
        var service = NewService(store);
        service.Record(new UndoToggle(file with { Enabled = false }, PreviousEnabled: true));

        AddinFileIdentity identity = service.Undo(_ => file with { Enabled = false });

        Assert.Equal(1, store.SetEnabledCallCount);
        Assert.Equal(file.Identity, identity);
        Assert.False(service.HasUndo);
    }

    [Fact]
    public void Undo_ToggleGroup_RestoresEveryFile()
    {
        var store = new FakeAddinStore();
        AddinFile first = NewFile("A.addin", "2025", AddinScope.User);
        AddinFile second = NewFile("B.addin", "2025", AddinScope.User);
        var service = NewService(store);
        service.Record(new UndoToggleGroup(
        [
            new UndoToggle(first, PreviousEnabled: true),
            new UndoToggle(second, PreviousEnabled: false),
        ]));

        service.Undo(identity => identity.FileName == "A.addin" ? first : second);

        Assert.Equal(2, store.SetEnabledCallCount);
    }

    [Fact]
    public void Undo_Delete_RestoresSavedBytes()
    {
        var store = new FakeAddinStore();
        AddinFile file = NewFile("A.addin", "2025", AddinScope.User);
        byte[] content = [1, 2, 3];
        var service = NewService(store);
        service.Record(new UndoDelete([new DeletedFile(file, content)]));

        AddinFileIdentity identity = service.Undo(_ => null);

        Assert.Equal(1, store.RestoreCallCount);
        var restored = Assert.Single(store.RestoredFiles);
        Assert.Equal(file.FullPath, restored.FullPath);
        Assert.Equal(content, restored.Content);
        Assert.Equal(file.Identity, identity);
    }

    [Fact]
    public void Undo_Move_MovesBackAndRestoresReplacedTarget()
    {
        var store = new FakeAddinStore();
        AddinFile original = NewFile("A.addin", "2025", AddinScope.User);
        AddinFile moved = original with { Version = "2026", FullPath = @"C:\fake\2026\A.addin" };
        store.SetFiles("2026", moved);
        byte[] replaced = [9, 9];
        var service = NewService(store);
        service.Record(new UndoMove(moved, original, ReplacedPath: @"C:\fake\2026\A.addin", ReplacedContent: replaced));

        AddinFileIdentity identity = service.Undo(_ => moved);

        Assert.Equal(1, store.MoveCallCount);
        Assert.Single(store.RestoredFiles);
        Assert.Equal(original.Identity, identity);
    }

    [Fact]
    public void Undo_Copy_DeletesCopyAndRestoresReplacedTarget()
    {
        var store = new FakeAddinStore();
        AddinFile source = NewFile("A.addin", "2025", AddinScope.User);
        AddinFile copy = source with { Version = "2026", FullPath = @"C:\fake\2026\A.addin" };
        store.SetFiles("2026", copy);
        byte[] replaced = [7];
        var service = NewService(store);
        service.Record(new UndoCopy(
            source, copy, ReplacedPath: @"C:\fake\2026\Other.addin", ReplacedContent: replaced));

        AddinFileIdentity identity = service.Undo(_ => copy);

        Assert.Equal(1, store.DeleteCallCount);
        Assert.Single(store.RestoredFiles);
        Assert.Equal(source.Identity, identity);
    }

    [Fact]
    public void Undo_Rename_RenamesBackAndReturnsPreviousIdentity()
    {
        var store = new FakeAddinStore();
        AddinFile before = NewFile("Old.addin", "2025", AddinScope.User);
        AddinFile after = before with { FileName = "New.addin", FullPath = @"C:\fake\2025\New.addin" };
        store.SetFiles("2025", after);
        var service = NewService(store);
        service.Record(new UndoRename(before, after));

        AddinFileIdentity identity = service.Undo(_ => after);

        Assert.Equal(1, store.RenameCallCount);
        Assert.Equal(before.Identity, identity);
    }

    [Fact]
    public void Undo_FileMissing_ThrowsLocalizedErrorClearsStackAndLogsWarning()
    {
        var store = new FakeAddinStore();
        var logger = new RecordingLogger<UndoService>();
        var service = new UndoService(store, logger, TestLocalization.For<UndoService>());
        service.Record(new UndoToggle(NewFile("Gone.addin", "2025", AddinScope.User), PreviousEnabled: true));

        Assert.Throws<IOException>(() => service.Undo(_ => null));

        Assert.False(service.HasUndo);
        Assert.True(logger.HasEntry(LogLevel.Warning, "Gone.addin"));
    }

    private static UndoService NewService(FakeAddinStore store) =>
        new(store, new RecordingLogger<UndoService>(), TestLocalization.For<UndoService>());

    private static AddinFile NewFile(string fileName, string version, AddinScope scope) =>
        new(fileName, scope, version, Enabled: true,
            FullPath: $@"C:\fake\{version}\{fileName}",
            VersionRootDirectory: $@"C:\fake\{version}",
            Manifest: new AddinManifest([], null, null, null, null, [], [], []));
}
