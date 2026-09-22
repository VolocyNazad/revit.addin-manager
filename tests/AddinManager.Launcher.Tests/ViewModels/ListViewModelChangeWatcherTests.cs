using System.IO;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Composition;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Проверяет вторую часть задачи ("если меняем что-то в файловой системе, окно тоже
/// обновляется"): <see cref="ListViewModel"/> должен запускать слежение
/// (<see cref="Core.Abstractions.Storage.IAddinChangeWatcher"/>) при создании и перечитывать диск (<see cref="ListViewModel.Refresh"/>)
/// на каждое его срабатывание — через <see cref="Launcher.Abstractions.Services.IUiDispatcher"/>, а не напрямую (событие
/// watcher'а может прийти не из потока UI, см. его контракт).
/// </summary>
public sealed class ListViewModelChangeWatcherTests
{
    private static readonly IAddinManifestParser Parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());

    [Fact]
    public void Constructor_StartsChangeWatcher()
    {
        var watcher = new FakeAddinChangeWatcher();

        _ = NewListViewModel(new FakeAddinStore(), watcher, out _);

        Assert.Equal(1, watcher.StartCallCount);
    }

    [Fact]
    public void ChangeWatcher_Changed_RefreshesThroughDispatcher()
    {
        var store = new FakeAddinStore();
        var watcher = new FakeAddinChangeWatcher();
        var list = NewListViewModel(store, watcher, out var dispatcher);
        var scansBeforeRaise = store.ScanCallCount;

        watcher.Raise();

        Assert.True(dispatcher.InvokeCount > 0, "Changed должен маршалиться через IUiDispatcher.Invoke.");
        Assert.True(store.ScanCallCount > scansBeforeRaise, "Changed должен приводить к повторному ScanVersion (т.е. Refresh()).");
        Assert.Equal(0, list.FileCount); // диск как был пуст, так и остался — Refresh честно это подтвердил
    }

    [Fact]
    public void ChangeWatcher_Changed_PicksUpFileThatAppearedOnDisk()
    {
        var store = new FakeAddinStore();
        var watcher = new FakeAddinChangeWatcher();
        var list = NewListViewModel(store, watcher, out _);

        Assert.Equal(0, list.FileCount);

        // Файл "появился на диске" между сканированиями — типичный случай внешнего изменения.
        store.SetFiles("2025", NewFile("New.addin", "2025"));
        watcher.Raise();

        Assert.Equal(1, list.FileCount);
        Assert.Equal("New.addin", list.SelectedFile?.FileName);
    }

    [Fact]
    public void ChangeWatcher_Changed_PicksUpFileThatDisappearedFromDisk()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("Gone.addin", "2025"));
        var watcher = new FakeAddinChangeWatcher();
        var list = NewListViewModel(store, watcher, out _);

        Assert.Equal(1, list.FileCount);

        // Файл удалили извне (в проводнике, другим процессом) между сканированиями.
        store.SetFiles("2025");
        watcher.Raise();

        Assert.Equal(0, list.FileCount);
        Assert.Null(list.SelectedFile);
    }

    /// <summary>
    /// Часть плотного логирования "сервисов обновления данных аддинов" — когда файл, выбранный
    /// до Refresh, не пережил пересканирование (удалён извне между сканированиями), это должно
    /// быть видно в логе на уровне Information, а не только по факту смены SelectedFile.
    /// </summary>
    [Fact]
    public void Refresh_PreviousSelectionGone_LogsInformation()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("Gone.addin", "2025"));
        var watcher = new FakeAddinChangeWatcher();
        var logger = new RecordingLogger<ListViewModel>();
        var list = new ListViewModel(
            store, watcher, new RecordingUiDispatcher(), logger,
            new FakeLocalizationService(), TestLocalization.For<ListViewModel>(),
            new AddinFileRowViewModelFactory(store, NullLoggerFactory.Instance, TestLocalization.For<AddinFileRowViewModel>()),
            new FakeToastService(),
            new FakeRevitProcessGuard(),
            new FakeDialogService(),
            new FakeFolderOpener());
        Assert.Equal("Gone.addin", list.SelectedFile?.FileName);

        store.SetFiles("2025");
        list.Refresh();

        Assert.True(
            logger.HasEntry(LogLevel.Information, "не пережил пересканирование"),
            "Потеря прежнего выбора при Refresh должна быть залогирована на уровне Information.");
    }

    [Fact]
    public void Refresh_FindsFilesForVersion_LogsDebugCount()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin", "2025"));
        var watcher = new FakeAddinChangeWatcher();
        var logger = new RecordingLogger<ListViewModel>();

        _ = new ListViewModel(
            store, watcher, new RecordingUiDispatcher(), logger,
            new FakeLocalizationService(), TestLocalization.For<ListViewModel>(),
            new AddinFileRowViewModelFactory(store, NullLoggerFactory.Instance, TestLocalization.For<AddinFileRowViewModel>()),
            new FakeToastService(),
            new FakeRevitProcessGuard(),
            new FakeDialogService(),
            new FakeFolderOpener());

        Assert.True(
            logger.HasEntry(LogLevel.Debug, "2025"),
            "Найденные для конкретной версии файлы должны попадать в Debug-лог с номером версии.");
    }

    /// <summary>
    /// Смена языка — тоже Refresh (пересоздаёт строки, те читают новые строки при построении),
    /// и тоже через <see cref="Launcher.Abstractions.Services.IUiDispatcher"/> — тот же путь,
    /// что у внешнего изменения диска выше.
    /// </summary>
    [Fact]
    public void Localization_LanguageChanged_RefreshesThroughDispatcher()
    {
        var store = new FakeAddinStore();
        var watcher = new FakeAddinChangeWatcher();
        var dispatcher = new RecordingUiDispatcher();
        var localization = new FakeLocalizationService();
        var list = NewListViewModel(store, watcher, dispatcher, localization, new FakeRevitProcessGuard(), new FakeDialogService());
        var scansBeforeRaise = store.ScanCallCount;

        localization.RaiseLanguageChanged();

        Assert.True(dispatcher.InvokeCount > 0, "Смена языка должна маршалиться через IUiDispatcher.Invoke.");
        Assert.True(store.ScanCallCount > scansBeforeRaise, "Смена языка должна приводить к повторному ScanVersion (т.е. Refresh()).");
        Assert.Equal(0, list.FileCount);
    }

    /// <summary>
    /// Подписи тулбара читаются из resx под текущую <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>,
    /// а не зашиты: культуру выставляет сам тест, чтобы не зависеть от языка ОС.
    /// </summary>
    [Fact]
    public void Labels_FollowCurrentUiCulture()
    {
        var list = NewListViewModel(new FakeAddinStore(), new FakeAddinChangeWatcher(), out _);

        TestCulture.RunIn("ru", () => Assert.Equal("Поиск по имени", list.SearchPlaceholder));
        TestCulture.RunIn("en", () => Assert.Equal("Search by name", list.SearchPlaceholder));
    }

    /// <summary>
    /// Строки списка обязан создавать контейнер через фабрику: Refresh сканирует диск, а сами
    /// строки собирает <see cref="Launcher.Abstractions.Composition.IAddinFileRowViewModelFactory"/>
    /// (прямого <c>new</c> в прод-коде нет — см. политику в docs/policies/development.md).
    /// </summary>
    [Fact]
    public void Refresh_CreatesRowsThroughFactory()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin", "2025"), NewFile("B.addin", "2025"));
        var factory = new FakeFileRowFactory(store);
        var list = new ListViewModel(
            store,
            new FakeAddinChangeWatcher(),
            new RecordingUiDispatcher(),
            NullLogger<ListViewModel>.Instance,
            new FakeLocalizationService(),
            TestLocalization.For<ListViewModel>(),
            factory,
            new FakeToastService(),
            new FakeRevitProcessGuard(),
            new FakeDialogService(),
            new FakeFolderOpener());

        Assert.Equal(2, factory.CreatedFiles.Count);
        Assert.Equal(2, list.Files.Count);
    }

    /// <summary>
    /// Ручное обновление по кнопке показывает тост-подтверждение на языке интерфейса, а тихий
    /// <see cref="ListViewModel.Refresh"/> (старт, вотчер, сохранения) — нет.
    /// </summary>
    [Fact]
    public void RefreshManualCommand_ShowsToastButSilentRefreshDoesNot()
    {
        var store = new FakeAddinStore();
        var toast = new FakeToastService();
        var list = new ListViewModel(
            store,
            new FakeAddinChangeWatcher(),
            new RecordingUiDispatcher(),
            NullLogger<ListViewModel>.Instance,
            new FakeLocalizationService(),
            TestLocalization.For<ListViewModel>(),
            new AddinFileRowViewModelFactory(store, NullLoggerFactory.Instance, TestLocalization.For<AddinFileRowViewModel>()),
            toast,
            new FakeRevitProcessGuard(),
            new FakeDialogService(),
            new FakeFolderOpener());
        Assert.Empty(toast.ShownMessages);

        TestCulture.RunIn("ru", () =>
        {
            list.RefreshManualCommand.Execute(null);

            Assert.Equal(["Список обновлён"], toast.ShownMessages);
        });

        list.Refresh();

        Assert.Single(toast.ShownMessages);
    }

    /// <summary>
    /// Пока жив Revit, тогглы строк гаснут: сторож разносит состояние по строкам, новые строки
    /// после Refresh подхватывают текущее.
    /// </summary>
    [Fact]
    public void GuardChanged_LocksRows()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("A.addin", "2025"));
        var guard = new FakeRevitProcessGuard();
        var list = new ListViewModel(
            store,
            new FakeAddinChangeWatcher(),
            new RecordingUiDispatcher(),
            NullLogger<ListViewModel>.Instance,
            new FakeLocalizationService(),
            TestLocalization.For<ListViewModel>(),
            new AddinFileRowViewModelFactory(store, NullLoggerFactory.Instance, TestLocalization.For<AddinFileRowViewModel>()),
            new FakeToastService(),
            guard,
            new FakeDialogService(),
            new FakeFolderOpener());
        var row = Assert.Single(list.Files);
        Assert.False(row.IsLocked);

        guard.RunningVersions = new HashSet<string>(["2025"], StringComparer.Ordinal);
        guard.RaiseChanged();

        Assert.True(row.IsLocked);

        guard.RunningVersions = new HashSet<string>(StringComparer.Ordinal);
        guard.RaiseChanged();

        Assert.False(row.IsLocked);
    }

    /// <summary>
    /// Крестик строки: подтверждение — файл исчезает из стора и списка, выбор переезжает
    /// (тут список пуст — в null); отказ — ничего не трогает, даже рескана нет.
    /// </summary>
    [Fact]
    public void RowDeleteCommand_Confirmed_DeletesAndRefreshes()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("Gone.addin", "2025"));
        var dialog = new FakeDialogService();
        var list = NewListViewModel(store, new FakeAddinChangeWatcher(), out _, dialog);
        var row = Assert.Single(list.Files);
        Assert.Equal(1, list.FileCount);
        var scansBeforeDelete = store.ScanCallCount;

        TestCulture.RunIn("ru", () =>
        {
            row.DeleteCommand.Execute(null);

            Assert.Equal(["Удалить 'Gone.addin' навсегда?"], dialog.ShownMessages);
        });

        Assert.Equal(0, list.FileCount);
        Assert.Null(list.SelectedFile);
        Assert.True(store.ScanCallCount > scansBeforeDelete, "После удаления список должен перечитаться.");
    }

    [Fact]
    public void RowDeleteCommand_Declined_DoesNothing()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("Stay.addin", "2025"));
        var dialog = new FakeDialogService { Result = false };
        var list = NewListViewModel(store, new FakeAddinChangeWatcher(), out _, dialog);
        var row = Assert.Single(list.Files);
        var scansBeforeDelete = store.ScanCallCount;

        row.DeleteCommand.Execute(null);

        Assert.Single(dialog.ShownMessages);
        Assert.Equal(1, list.FileCount);
        Assert.Equal("Stay.addin", list.SelectedFile?.FileName);
        Assert.Equal(scansBeforeDelete, store.ScanCallCount);
    }

    [Fact]
    public void RowDeleteCommand_StoreThrows_ShowsRowErrorAndKeepsSelection()
    {
        var store = new FakeAddinStore();
        store.SetFiles("2025", NewFile("Locked.addin", "2025"));
        var list = NewListViewModel(store, new FakeAddinChangeWatcher(), out _, new FakeDialogService());
        var row = Assert.Single(list.Files);
        store.ThrowOnNextDelete = new IOException("boom");

        row.DeleteCommand.Execute(null);

        Assert.Equal("boom", row.ErrorMessage);
        Assert.Equal("Locked.addin", list.SelectedFile?.FileName);
        Assert.Equal(1, list.FileCount);
    }

    private static ListViewModel NewListViewModel(
        FakeAddinStore store, FakeAddinChangeWatcher watcher, out RecordingUiDispatcher dispatcher)
    {
        dispatcher = new RecordingUiDispatcher();
        return NewListViewModel(store, watcher, dispatcher, new FakeLocalizationService(), new FakeRevitProcessGuard(), new FakeDialogService());
    }

    private static ListViewModel NewListViewModel(
        FakeAddinStore store, FakeAddinChangeWatcher watcher, out RecordingUiDispatcher dispatcher, FakeDialogService dialog)
    {
        dispatcher = new RecordingUiDispatcher();
        return NewListViewModel(store, watcher, dispatcher, new FakeLocalizationService(), new FakeRevitProcessGuard(), dialog);
    }

    private static ListViewModel NewListViewModel(
        FakeAddinStore store,
        FakeAddinChangeWatcher watcher,
        RecordingUiDispatcher dispatcher,
        FakeLocalizationService localization,
        FakeRevitProcessGuard guard,
        FakeDialogService dialog)
    {
        return new ListViewModel(
            store,
            watcher,
            dispatcher,
            NullLogger<ListViewModel>.Instance,
            localization,
            TestLocalization.For<ListViewModel>(),
            new AddinFileRowViewModelFactory(store, NullLoggerFactory.Instance, TestLocalization.For<AddinFileRowViewModel>()),
            new FakeToastService(),
            guard,
            dialog,
            new FakeFolderOpener());
    }

    private static AddinFile NewFile(string fileName, string version) =>
        new(fileName, AddinScope.User, version, Enabled: true,
            FullPath: $@"C:\fake\{version}\{fileName}",
            VersionRootDirectory: $@"C:\fake\{version}",
            Manifest: Parser.Parse(ValidAddinXml(fileName)));

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
