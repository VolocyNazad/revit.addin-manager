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
/// задачи <see cref="ManifestSettingsViewModel"/> не имел вообще никакого тестового покрытия.
/// Использует версию "2026" — единственную границу, где <see cref="RevitManifestSchema"/>
/// поддерживает <see cref="ManifestSettings"/> (см. её комментарий), иначе <c>IsSupported</c>
/// был бы false и <c>Save</c> — недоступен.
/// </summary>
public sealed class ManifestSettingsViewModelTests : IDisposable
{
    private readonly List<string> _tempRoots = [];
    private readonly IAddinManifestParser _parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());
    private readonly IManifestSchema _schema = new RevitManifestSchema();

    [Fact]
    public void Save_ValidChange_LogsInformationAndRefreshesList()
    {
        var (list, _) = NewListWithFile("2026");
        var logger = new RecordingLogger<ManifestSettingsViewModel>();
        var settings = new ManifestSettingsViewModel(
            list, _parser, NewMarkupService(), _schema, logger,
            new FakeLocalizationService(), TestLocalization.For<ManifestSettingsViewModel>(),
            new RecordingUiDispatcher(),
            new FakeRevitProcessGuard());

        Assert.True(settings.IsSupported);
        settings.SetUseRevitContextCommand.Execute("True");
        Assert.True(settings.IsDirty);
        Assert.True(settings.SaveCommand.CanExecute(null));

        settings.SaveCommand.Execute(null);

        Assert.True(
            logger.HasEntry(LogLevel.Information, "сохранены"),
            "Успешное сохранение ManifestSettings должно быть залогировано на уровне Information.");
        Assert.True(list.Files.Single().File.Manifest.Settings?.UseRevitContext);
    }

    [Fact]
    public void UnsupportedVersion_CannotSave()
    {
        var (list, _) = NewListWithFile("2025");
        var logger = new RecordingLogger<ManifestSettingsViewModel>();
        var settings = new ManifestSettingsViewModel(
            list, _parser, NewMarkupService(), _schema, logger,
            new FakeLocalizationService(), TestLocalization.For<ManifestSettingsViewModel>(),
            new RecordingUiDispatcher(),
            new FakeRevitProcessGuard());

        Assert.False(settings.IsSupported);
        Assert.False(settings.SaveCommand.CanExecute(null));
    }

    private FileAddinMarkupService NewMarkupService() =>
        new(_parser, NullLogger<FileAddinMarkupService>.Instance, new FakeRevitProcessGuard(), TestLocalization.For<FileAddinMarkupService>());

    private (ListViewModel List, string Path) NewListWithFile(string version)
    {
        var userRoot = NewRoot();
        var versionDir = Path.Combine(userRoot, "Autodesk", "Revit", "Addins", version);
        Directory.CreateDirectory(versionDir);
        var path = Path.Combine(versionDir, "A.addin");
        File.WriteAllText(path, ValidAddinXml("Original"));

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
            new FakeDialogService(),
            new FakeFolderOpener());

        return (list, path);
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
