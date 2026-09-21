using System.IO;
using AddinManager.Core.Abstractions.Manifests;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Manifests;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Composition;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Часть плотного логирования и покрытия тестами "сервисов обновления данных аддинов": до этой
/// задачи <see cref="FormViewModel"/> не имел вообще никакого тестового покрытия. Использует
/// настоящие <see cref="FileSystemAddinStore"/>/<see cref="FileAddinMarkupService"/>/
/// <see cref="RevitManifestSchema"/> на временных папках — тот же приём, что и
/// <see cref="MarkupViewModelRefreshTests"/>, поскольку подмена всей цепочки List → Entries →
/// Form фейками потребовала бы воспроизвести ту же логику, что уже покрыта отдельно.
/// </summary>
public sealed class FormViewModelTests : IDisposable
{
    private readonly List<string> _tempRoots = [];
    private readonly IAddinManifestParser _parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());
    private readonly IManifestSchema _schema = new RevitManifestSchema();

    [Fact]
    public void Save_ValidChanges_LogsInformationAndRefreshesList()
    {
        var (list, entries) = NewListAndEntries("2025", "Original");
        var logger = new RecordingLogger<FormViewModel>();
        var form = new FormViewModel(
            entries, list, _parser, NewMarkupService(), _schema, logger,
            new FakeLocalizationService(), TestLocalization.For<FormViewModel>(),
            new SelectableOptionViewModelFactory(),
            new RecordingUiDispatcher(),
            new FakeRevitProcessGuard())
        {
            Name = "Renamed"
        };
        Assert.True(form.IsDirty);
        Assert.True(form.SaveCommand.CanExecute(null));

        form.SaveCommand.Execute(null);

        Assert.True(
            logger.HasEntry(LogLevel.Information, "сохранена"),
            "Успешное сохранение формы должно быть залогировано на уровне Information.");
        Assert.Equal("Renamed", list.Files.Single().File.Manifest.Entries.Single().Name);
    }

    [Fact]
    public void Save_InvalidAddInId_LogsWarningAndDoesNotSave()
    {
        var (list, entries) = NewListAndEntries("2025", "Original");
        var logger = new RecordingLogger<FormViewModel>();
        var form = new FormViewModel(
            entries, list, _parser, NewMarkupService(), _schema, logger,
            new FakeLocalizationService(), TestLocalization.For<FormViewModel>(),
            new SelectableOptionViewModelFactory(),
            new RecordingUiDispatcher(),
            new FakeRevitProcessGuard())
        {
            AddInIdText = "not-a-guid"
        };

        Assert.False(form.SaveCommand.CanExecute(null)); // инлайн-валидация: невалидный AddInId блокирует Save заранее
        form.SaveCommand.Execute(null);

        Assert.True(
            logger.HasEntry(LogLevel.Warning, "некорректный AddInId"),
            "Отклонённый некорректный AddInId должен быть залогирован на уровне Warning.");
        Assert.Equal("Original", list.Files.Single().File.Manifest.Entries.Single().Name);
    }

    [Fact]
    public void Save_MissingAssemblyPath_LogsWarningAndDoesNotSave()
    {
        var (list, entries) = NewListAndEntries("2025", "Original");
        var logger = new RecordingLogger<FormViewModel>();
        var form = new FormViewModel(
            entries, list, _parser, NewMarkupService(), _schema, logger,
            new FakeLocalizationService(), TestLocalization.For<FormViewModel>(),
            new SelectableOptionViewModelFactory(),
            new RecordingUiDispatcher(),
            new FakeRevitProcessGuard())
        {
            AssemblyPath = "   "
        };

        Assert.False(form.SaveCommand.CanExecute(null)); // инлайн-валидация: пустой Assembly блокирует Save заранее
        form.SaveCommand.Execute(null);

        Assert.True(
            logger.HasEntry(LogLevel.Warning, "assembly"),
            "Отсутствующий Assembly должен быть залогирован на уровне Warning.");
        Assert.Equal("Original", list.Files.Single().File.Manifest.Entries.Single().Name);
    }

    [Fact]
    public void FieldErrors_EmptyRequiredFields_ShownInlineAndBlockSave()
    {
        var (list, entries) = NewListAndEntries("2025", "Original");
        var form = NewForm(entries, list);

        Assert.Null(form.AssemblyPathError);
        Assert.Null(form.FullClassNameError);

        form.AssemblyPath = "   ";
        form.FullClassName = string.Empty;

        TestCulture.RunIn("en", () =>
        {
            Assert.Equal("Assembly is required.", form.AssemblyPathError);
            Assert.Equal("FullClassName is required.", form.FullClassNameError);
        });
        Assert.False(form.SaveCommand.CanExecute(null));

        form.AssemblyPath = "a.dll";
        form.FullClassName = "A.App";

        Assert.Null(form.AssemblyPathError);
        Assert.Null(form.FullClassNameError);
        Assert.True(form.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void FieldErrors_BadAddInId_ShownInlineAndBlockSaveUntilRegenerated()
    {
        var (list, entries) = NewListAndEntries("2025", "Original");
        var form = NewForm(entries, list);

        Assert.Null(form.AddInIdError);

        form.AddInIdText = "zzz";

        TestCulture.RunIn("en", () =>
            Assert.Equal("Invalid AddInId: 'zzz'.", form.AddInIdError));
        Assert.False(form.SaveCommand.CanExecute(null));

        form.GenerateAddInIdCommand.Execute(null);

        Assert.Null(form.AddInIdError);
        Assert.True(form.SaveCommand.CanExecute(null));
    }

    private FormViewModel NewForm(
        EntriesViewModel entries, ListViewModel list, ILogger<FormViewModel>? logger = null) =>
        new(entries, list, _parser, NewMarkupService(), _schema,
            logger ?? NullLogger<FormViewModel>.Instance,
            new FakeLocalizationService(), TestLocalization.For<FormViewModel>(),
            new SelectableOptionViewModelFactory(),
            new RecordingUiDispatcher(),
            new FakeRevitProcessGuard());

    /// <summary>
    /// Пункты мульти-наборов обязан создавать контейнер через фабрику: форму собирает
    /// <see cref="Launcher.Abstractions.Composition.ISelectableOptionViewModelFactory"/>
    /// (прямого <c>new</c> в прод-коде нет). Command-запись — единственная со значениями
    /// VisibilityMode/Discipline, на Application проверять нечего.
    /// </summary>
    [Fact]
    public void LoadFrom_CreatesOptionsThroughFactory()
    {
        var userRoot = NewRoot();
        var versionDir = Path.Combine(userRoot, "Autodesk", "Revit", "Addins", "2025");
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "A.addin"), CommandAddinXml());

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
        var entries = new EntriesViewModel(
            list,
            new FakeLocalizationService(),
            TestLocalization.For<EntriesViewModel>(),
            new AddinEntryRowViewModelFactory(TestLocalization.For<AddinEntryRowViewModel>()),
            _parser,
            NewMarkupService(),
            new FakeDialogService(),
            new RecordingUiDispatcher(),
            new FakeRevitProcessGuard());
        var factory = new FakeOptionFactory();

        var form = new FormViewModel(
            entries, list, _parser, NewMarkupService(), _schema,
            NullLogger<FormViewModel>.Instance,
            new FakeLocalizationService(), TestLocalization.For<FormViewModel>(),
            factory,
            new RecordingUiDispatcher(),
            new FakeRevitProcessGuard());

        var expected = _schema.VisibilityModeValues.Concat(_schema.DisciplineValues).ToList();
        Assert.Equal(expected, factory.CreatedValues);
        Assert.Equal(expected.Count, form.VisibilityModeOptions.Count + form.DisciplineOptions.Count);
    }

    private FileAddinMarkupService NewMarkupService() =>
        new(_parser, NullLogger<FileAddinMarkupService>.Instance, new FakeRevitProcessGuard(), TestLocalization.For<FileAddinMarkupService>());

    /// <summary>
    /// Пока жив Revit, форма заблокирована: поля только читаются, Save/Discard/Generate
    /// недоступны (CanExecute), а снятие блокировки всё возвращает.
    /// </summary>
    [Fact]
    public void RevitRunning_LocksEditingAndSave()
    {
        var guard = new FakeRevitProcessGuard();
        var dispatcher = new RecordingUiDispatcher();
        var (list, entries) = NewListAndEntries("2025", "Original");
        var form = new FormViewModel(
            entries, list, _parser, NewMarkupService(), _schema,
            NullLogger<FormViewModel>.Instance,
            new FakeLocalizationService(), TestLocalization.For<FormViewModel>(),
            new SelectableOptionViewModelFactory(),
            dispatcher,
            guard)
        {
            Name = "Renamed"
        };
        Assert.False(form.IsLocked);
        Assert.True(form.SaveCommand.CanExecute(null));

        guard.RunningVersions = new HashSet<string>(["2025"], StringComparer.Ordinal);
        guard.RaiseChanged();

        Assert.True(form.IsLocked);
        Assert.False(form.SaveCommand.CanExecute(null));
        Assert.False(form.DiscardCommand.CanExecute(null));
        Assert.False(form.GenerateAddInIdCommand.CanExecute(null));
        Assert.All(
            form.VisibilityModeOptions.Concat(form.DisciplineOptions),
            option => Assert.True(option.IsLocked));

        guard.RunningVersions = new HashSet<string>(StringComparer.Ordinal);
        guard.RaiseChanged();

        Assert.False(form.IsLocked);
        Assert.True(form.SaveCommand.CanExecute(null));
    }

    private (ListViewModel List, EntriesViewModel Entries) NewListAndEntries(string version, string entryName)
    {
        var userRoot = NewRoot();
        var versionDir = Path.Combine(userRoot, "Autodesk", "Revit", "Addins", version);
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "A.addin"), ValidAddinXml(entryName));

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
        var entries = new EntriesViewModel(
            list,
            new FakeLocalizationService(),
            TestLocalization.For<EntriesViewModel>(),
            new AddinEntryRowViewModelFactory(TestLocalization.For<AddinEntryRowViewModel>()),
            _parser,
            NewMarkupService(),
            new FakeDialogService(),
            new RecordingUiDispatcher(),
            new FakeRevitProcessGuard());

        return (list, entries);
    }

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

    private static string CommandAddinXml() => """
        <RevitAddIns>
          <AddIn Type="Command">
            <Text>Open log</Text>
            <Assembly>a.dll</Assembly>
            <AddInId>33333333-3333-3333-3333-333333333333</AddInId>
            <FullClassName>A.Cmd</FullClassName>
            <VisibilityMode>AlwaysVisible</VisibilityMode>
            <Discipline>Architecture</Discipline>
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
