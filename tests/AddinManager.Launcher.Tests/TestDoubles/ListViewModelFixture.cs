using AddinManager.Core.Abstractions.Guard;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Launcher.Abstractions.Composition;
using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.Composition;
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
        IEditorDirtyState? dirtyState = null) =>
        new(
            store,
            changeWatcher ?? new FakeAddinChangeWatcher(),
            dispatcher ?? new RecordingUiDispatcher(),
            logger ?? NullLogger<ListViewModel>.Instance,
            localizationService ?? new FakeLocalizationService(),
            TestLocalization.For<ListViewModel>(),
            rowFactory ?? new AddinFileRowViewModelFactory(
                store, NullLoggerFactory.Instance, TestLocalization.For<AddinFileRowViewModel>()),
            toastService ?? new FakeToastService(),
            guard ?? new FakeRevitProcessGuard(),
            dialogService ?? new FakeDialogService(),
            new FakeFolderOpener(),
            new FakeClipboardService(),
            dirtyState);
}
