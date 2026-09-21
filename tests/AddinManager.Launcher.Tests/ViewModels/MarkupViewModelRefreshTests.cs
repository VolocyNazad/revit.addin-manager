using System.IO;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Composition;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Проверяет первую часть задачи ("где бы мы ни меняли данные, они должны быть актуальны и в
/// других окнах") для конкретной ранее найденной асимметрии: правка через форму уже дёргала
/// <see cref="ListViewModel.Refresh"/> после сохранения, а правка сырого XML в
/// <see cref="MarkupViewModel"/> — нет, так что список (и всё, что от него зависит — панель
/// записей, форма) продолжал показывать манифест, прочитанный до правки. Использует настоящие
/// <see cref="FileSystemAddinStore"/>/<see cref="FileAddinMarkupService"/> на временных папках
/// (тот же приём, что и в AddinManager.Core.Tests) — иначе тест не отличил бы "Save дернул
/// Refresh" от "оба смотрят в один и тот же объект в памяти".
/// </summary>
public sealed class MarkupViewModelRefreshTests : IDisposable
{
    private readonly List<string> _tempRoots = [];
    private readonly IAddinManifestParser _parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());

    [Fact]
    public void Save_RefreshesListViewModel_WithoutManualRefresh()
    {
        var userRoot = NewRoot();
        var versionDir = Path.Combine(userRoot, "Autodesk", "Revit", "Addins", "2025");
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "A.addin"), ValidAddinXml("Original"));

        var store = new FileSystemAddinStore(_parser, NullLogger<FileSystemAddinStore>.Instance, new FakeRevitProcessGuard(), TestLocalization.For<FileSystemAddinStore>(), userRoot, NewRoot());
        var list = new ListViewModel(
            store,
            new FakeAddinChangeWatcher(), // слежение за диском тут не тестируем — см. ListViewModelChangeWatcherTests
            new RecordingUiDispatcher(),
            NullLogger<ListViewModel>.Instance,
            new FakeLocalizationService(),
            TestLocalization.For<ListViewModel>(),
            new AddinFileRowViewModelFactory(store, NullLoggerFactory.Instance, TestLocalization.For<AddinFileRowViewModel>()),
            new FakeToastService(),
            new FakeRevitProcessGuard(),
            new FakeDialogService());

        // Список уже прочитал файл при старте — с именем записи "Original".
        Assert.Equal("Original", list.Files.Single().File.Manifest.Entries.Single().Name);

        var markupService = new FileAddinMarkupService(_parser, NullLogger<FileAddinMarkupService>.Instance, new FakeRevitProcessGuard(), TestLocalization.For<FileAddinMarkupService>());
        var markup = new MarkupViewModel(
            list, markupService, NullLogger<MarkupViewModel>.Instance,
            new FakeLocalizationService(), TestLocalization.For<MarkupViewModel>(),
            new RecordingUiDispatcher(),
            new FakeRevitProcessGuard(),
            NewEntries(list))
        {
            RawXml = ValidAddinXml("Edited")
        };
        Assert.True(markup.IsDirty);
        Assert.Null(markup.ErrorMessage);

        markup.SaveCommand.Execute(null);

        Assert.False(markup.IsDirty);
        // Без ручного list.Refresh() список уже должен видеть новое имя записи — это и есть
        // регрессия, которую чинит эта задача: MarkupViewModel.Save теперь сам дёргает Refresh.
        Assert.Equal("Edited", list.Files.Single().File.Manifest.Entries.Single().Name);
    }

    [Fact]
    public void Save_InvalidXml_DoesNotSaveAndDoesNotRefresh()
    {
        var userRoot = NewRoot();
        var versionDir = Path.Combine(userRoot, "Autodesk", "Revit", "Addins", "2025");
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "A.addin"), ValidAddinXml("Original"));

        var store = new FileSystemAddinStore(_parser, NullLogger<FileSystemAddinStore>.Instance, new FakeRevitProcessGuard(), TestLocalization.For<FileSystemAddinStore>(), userRoot, NewRoot());
        var list = new ListViewModel(
            store,
            new FakeAddinChangeWatcher(),
            new RecordingUiDispatcher(),
            NullLogger<ListViewModel>.Instance,
            new FakeLocalizationService(),
            TestLocalization.For<ListViewModel>(),
            new AddinFileRowViewModelFactory(store, NullLoggerFactory.Instance, TestLocalization.For<AddinFileRowViewModel>()),
            new FakeToastService(),
            new FakeRevitProcessGuard(),
            new FakeDialogService());

        var markupService = new FileAddinMarkupService(_parser, NullLogger<FileAddinMarkupService>.Instance, new FakeRevitProcessGuard(), TestLocalization.For<FileAddinMarkupService>());
        var markup = new MarkupViewModel(
            list, markupService, NullLogger<MarkupViewModel>.Instance,
            new FakeLocalizationService(), TestLocalization.For<MarkupViewModel>(),
            new RecordingUiDispatcher(),
            new FakeRevitProcessGuard(),
            NewEntries(list))
        {
            RawXml = "not xml {{{"
        };
        Assert.NotNull(markup.ErrorMessage);

        // CanSave() требует ErrorMessage == null — SaveCommand не должна быть исполнима.
        Assert.False(markup.SaveCommand.CanExecute(null));

        markup.SaveCommand.Execute(null); // на случай прямого вызова в обход CanExecute — не должно ничего менять.

        Assert.Equal("Original", list.Files.Single().File.Manifest.Entries.Single().Name);
    }

    /// <summary>
    /// Часть плотного логирования "сервисов обновления данных аддинов" — успешное сохранение
    /// через MarkupViewModel должно быть видно в логе на уровне Information, не только через
    /// косвенный эффект (список перечитался).
    /// </summary>
    [Fact]
    public void Save_Succeeds_LogsInformation()
    {
        var userRoot = NewRoot();
        var versionDir = Path.Combine(userRoot, "Autodesk", "Revit", "Addins", "2025");
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "A.addin"), ValidAddinXml("Original"));

        var store = new FileSystemAddinStore(_parser, NullLogger<FileSystemAddinStore>.Instance, new FakeRevitProcessGuard(), TestLocalization.For<FileSystemAddinStore>(), userRoot, NewRoot());
        var list = new ListViewModel(
            store,
            new FakeAddinChangeWatcher(),
            new RecordingUiDispatcher(),
            NullLogger<ListViewModel>.Instance,
            new FakeLocalizationService(),
            TestLocalization.For<ListViewModel>(),
            new AddinFileRowViewModelFactory(store, NullLoggerFactory.Instance, TestLocalization.For<AddinFileRowViewModel>()),
            new FakeToastService(),
            new FakeRevitProcessGuard(),
            new FakeDialogService());

        var markupService = new FileAddinMarkupService(_parser, NullLogger<FileAddinMarkupService>.Instance, new FakeRevitProcessGuard(), TestLocalization.For<FileAddinMarkupService>());
        var logger = new RecordingLogger<MarkupViewModel>();
        var markup = new MarkupViewModel(
            list, markupService, logger,
            new FakeLocalizationService(), TestLocalization.For<MarkupViewModel>(),
            new RecordingUiDispatcher(),
            new FakeRevitProcessGuard(),
            NewEntries(list))
        {
            RawXml = ValidAddinXml("Edited")
        };
        markup.SaveCommand.Execute(null);

        Assert.True(
            logger.HasEntry(LogLevel.Information, "сохранён"),
            "Успешное сохранение из MarkupViewModel должно быть залогировано на уровне Information.");
    }

    private EntriesViewModel NewEntries(ListViewModel list) => new(
        list,
        new FakeLocalizationService(),
        TestLocalization.For<EntriesViewModel>(),
        new AddinEntryRowViewModelFactory(TestLocalization.For<AddinEntryRowViewModel>()),
        _parser,
        new FileAddinMarkupService(
            _parser,
            NullLogger<FileAddinMarkupService>.Instance,
            new FakeRevitProcessGuard(),
            TestLocalization.For<FileAddinMarkupService>()),
        new FakeDialogService(),
        new RecordingUiDispatcher(),
        new FakeRevitProcessGuard());

    /// <summary>
    /// Выбор записи в списке подсвечивает её <c>&lt;AddIn&gt;</c>-блок в разметке: срез
    /// начинается открывающим тегом и содержит <c>AddInId</c> именно выбранной записи.
    /// </summary>
    [Fact]
    public void SelectingEntry_HighlightsItsBlockInRawXml()
    {
        var (_, entries, markup) = NewGraph(TwoEntryXml());

        entries.SelectedEntry = entries.Entries[1];

        var span = Assert.NotNull(markup.SelectedEntrySpan);
        var block = markup.RawXml!.Substring(span.Start, span.Length);
        Assert.StartsWith("<AddIn", block, StringComparison.Ordinal);
        Assert.Contains("22222222-2222-2222-2222-222222222222", block, StringComparison.Ordinal);
        Assert.DoesNotContain("11111111-1111-1111-1111-111111111111", block, StringComparison.Ordinal);
    }

    /// <summary>
    /// Правка текста срез не трогает: иначе каждое нажатие клавиши заново выделяло бы блок
    /// и уводило каретку в его начало. Свежий срез посчитается при следующей смене выбора.
    /// </summary>
    [Fact]
    public void EditedXml_DoesNotMoveSpan()
    {
        var (_, entries, markup) = NewGraph(TwoEntryXml());
        entries.SelectedEntry = entries.Entries[1];
        var before = Assert.NotNull(markup.SelectedEntrySpan);

        markup.RawXml = ValidAddinXml("Only");

        Assert.Equal(before, markup.SelectedEntrySpan);
    }

    private (ListViewModel List, EntriesViewModel Entries, MarkupViewModel Markup) NewGraph(string xml)
    {
        var userRoot = NewRoot();
        var versionDir = Path.Combine(userRoot, "Autodesk", "Revit", "Addins", "2025");
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "A.addin"), xml);

        var store = new FileSystemAddinStore(_parser, NullLogger<FileSystemAddinStore>.Instance, new FakeRevitProcessGuard(), TestLocalization.For<FileSystemAddinStore>(), userRoot, NewRoot());
        var list = new ListViewModel(
            store,
            new FakeAddinChangeWatcher(),
            new RecordingUiDispatcher(),
            NullLogger<ListViewModel>.Instance,
            new FakeLocalizationService(),
            TestLocalization.For<ListViewModel>(),
            new AddinFileRowViewModelFactory(store, NullLoggerFactory.Instance, TestLocalization.For<AddinFileRowViewModel>()),
            new FakeToastService(),
            new FakeRevitProcessGuard(),
            new FakeDialogService());
        var entries = NewEntries(list);
        var markup = new MarkupViewModel(
            list,
            new FileAddinMarkupService(_parser, NullLogger<FileAddinMarkupService>.Instance, new FakeRevitProcessGuard(), TestLocalization.For<FileAddinMarkupService>()),
            NullLogger<MarkupViewModel>.Instance,
            new FakeLocalizationService(),
            TestLocalization.For<MarkupViewModel>(),
            new RecordingUiDispatcher(),
            new FakeRevitProcessGuard(),
            entries);

        return (list, entries, markup);
    }

    private static string TwoEntryXml() => """
        <RevitAddIns>
          <AddIn Type="Application">
            <Name>First</Name>
            <Assembly>a.dll</Assembly>
            <AddInId>11111111-1111-1111-1111-111111111111</AddInId>
            <FullClassName>A.First</FullClassName>
          </AddIn>
          <AddIn Type="Command">
            <Text>Second</Text>
            <Assembly>b.dll</Assembly>
            <AddInId>22222222-2222-2222-2222-222222222222</AddInId>
            <FullClassName>A.Second</FullClassName>
          </AddIn>
        </RevitAddIns>
        """;

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

    private string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "AddinManagerTests-" + Guid.NewGuid());
        _tempRoots.Add(root);
        return root;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var root in _tempRoots)
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // Временная папка теста — не критично, если не удалилась.
            }
            catch (UnauthorizedAccessException)
            {
                // Временная папка теста — не критично, если не удалилась.
            }
        }
    }
}
