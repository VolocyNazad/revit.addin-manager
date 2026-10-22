using AddinManager.Core.Abstractions.Guard;
using AddinManager.Core.Abstractions.Manifests;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Abstractions.Composition;
using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.Composition;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// Собирает <see cref="EntriesViewModel"/> и <see cref="FormViewModel"/> с тем же фейковым графом,
/// что и остальные тесты: тест передаёт только то, что действительно проверяет, а изменение
/// конструктора чинится в одном месте.
/// </summary>
internal static class EditorZoneFixture
{
    internal static EntriesViewModel CreateEntries(
        IAddinManifestParser parser,
        IFileSelection fileSelection,
        IAddinFileCatalog fileCatalog,
        IAddinMarkupService? markupService = null,
        IDialogService? dialogService = null,
        IRevitProcessGuard? guard = null,
        IUiDispatcher? dispatcher = null,
        IAddinEntryRowViewModelFactory? rowFactory = null,
        IToastService? toastService = null,
        IClipboardService? clipboardService = null)
    {
        guard ??= new FakeRevitProcessGuard();
        return new EntriesViewModel(
            fileSelection,
            fileCatalog,
            new FakeLocalizationService(),
            TestLocalization.For<EntriesViewModel>(),
            rowFactory ?? new AddinEntryRowViewModelFactory(TestLocalization.For<AddinEntryRowViewModel>()),
            parser,
            markupService ?? new FileAddinMarkupService(
                parser, NullLogger<FileAddinMarkupService>.Instance, guard,
                TestLocalization.For<FileAddinMarkupService>()),
            dialogService ?? new FakeDialogService(),
            dispatcher ?? new RecordingUiDispatcher(),
            guard,
            clipboardService ?? new FakeClipboardService(),
            toastService ?? new FakeToastService());
    }

    internal static FormViewModel CreateForm(
        IEntrySelection entrySelection,
        IFileSelection fileSelection,
        IAddinFileCatalog fileCatalog,
        IAddinManifestParser parser,
        IManifestSchema schema,
        ILogger<FormViewModel>? logger = null,
        IAddinMarkupService? markupService = null,
        ISelectableOptionViewModelFactory? optionFactory = null,
        IUiDispatcher? dispatcher = null,
        IRevitProcessGuard? guard = null)
    {
        guard ??= new FakeRevitProcessGuard();
        return new FormViewModel(
            entrySelection,
            fileSelection,
            fileCatalog,
            parser,
            markupService ?? new FileAddinMarkupService(
                parser, NullLogger<FileAddinMarkupService>.Instance, guard,
                TestLocalization.For<FileAddinMarkupService>()),
            schema,
            logger ?? NullLogger<FormViewModel>.Instance,
            new FakeLocalizationService(),
            TestLocalization.For<FormViewModel>(),
            optionFactory ?? new SelectableOptionViewModelFactory(),
            dispatcher ?? new RecordingUiDispatcher(),
            guard);
    }
}
