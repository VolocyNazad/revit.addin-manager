using AddinManager.Core.Abstractions.Guard;
using AddinManager.Core.Abstractions.Installations;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Launcher.Abstractions.Composition;
using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.Composition;
using AddinManager.Launcher.Services;
using AddinManager.Localization.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// Builds <see cref="ListViewModel"/> with the fake graph the tests share, so a test passes only the
/// dependencies it actually asserts on. Defaults mirror the wiring the tests used to repeat inline.
/// </summary>
internal static class ListViewModelFixture
{
    internal static ListViewModel Create(
        IAddinStore store,
        IRevitProcessGuard? guard = null,
        IAddinChangeWatcher? changeWatcher = null,
        ILogger<ListViewModel>? logger = null,
        IAddinFileRowViewModelFactory? rowFactory = null,
        IToastService? toastService = null,
        IDialogService? dialogService = null,
        IUiDispatcher? dispatcher = null,
        ILocalizationService? localizationService = null,
        IUndoService? undoService = null,
        ILogger<FileCatalog>? catalogLogger = null,
        IEditorDirtyState? dirtyState = null,
        IInstalledRevitVersions? installedVersions = null)
    {
        dialogService ??= new FakeDialogService();
        toastService ??= new FakeToastService();
        undoService ??= CreateUndoService(store);
        FileCatalog catalog = CreateCatalog(store, rowFactory, catalogLogger);
        IFileOperations operations = CreateOperations(store, dialogService, undoService, dirtyState);
        return new(
            operations,
            changeWatcher ?? new FakeAddinChangeWatcher(),
            dispatcher ?? new RecordingUiDispatcher(),
            logger ?? NullLogger<ListViewModel>.Instance,
            localizationService ?? new FakeLocalizationService(),
            TestLocalization.For<ListViewModel>(),
            catalog,
            installedVersions ?? new FakeInstalledRevitVersions(),
            toastService,
            guard ?? new FakeRevitProcessGuard(),
            dialogService,
            new FakeFolderOpener(),
            new FakeClipboardService(),
            undoService,
            dirtyState);
    }

    /// <summary>Стек отмены поверх фейкового store — тем же составом, что собирает корень композиции.</summary>
    internal static IUndoService CreateUndoService(IAddinStore store) =>
        new UndoService(store, NullLogger<UndoService>.Instance, TestLocalization.For<UndoService>());

    /// <summary>Каталог строк поверх фейкового store; фабрику строк и логгер можно подменить тестом.</summary>
    internal static FileCatalog CreateCatalog(
        IAddinStore store,
        IAddinFileRowViewModelFactory? rowFactory = null,
        ILogger<FileCatalog>? logger = null) =>
        new(
            store,
            rowFactory ?? new AddinFileRowViewModelFactory(
                store, NullLoggerFactory.Instance, TestLocalization.For<AddinFileRowViewModel>()),
            logger ?? NullLogger<FileCatalog>.Instance);

    /// <summary>Файловые операции поверх тех же фейков, что получает модель.</summary>
    internal static IFileOperations CreateOperations(
        IAddinStore store,
        IDialogService? dialogService = null,
        IUndoService? undoService = null,
        IEditorDirtyState? dirtyState = null) =>
        new FileOperations(
            store,
            dialogService ?? new FakeDialogService(),
            undoService ?? CreateUndoService(store),
            TestLocalization.For<FileOperations>(),
            NullLogger<FileOperations>.Instance,
            dirtyState);
}
