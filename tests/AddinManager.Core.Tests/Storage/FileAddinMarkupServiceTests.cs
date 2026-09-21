using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Guard;
using AddinManager.Core.Manifests;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using AddinManager.Core.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Core.Tests.Storage;

public sealed class FileAddinMarkupServiceTests : IDisposable
{
    private readonly List<string> _tempRoots = [];
    private readonly IAddinManifestParser _parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());
    private readonly FileAddinMarkupService _sut;

    public FileAddinMarkupServiceTests() =>
        _sut = new FileAddinMarkupService(
            _parser,
            NullLogger<FileAddinMarkupService>.Instance,
            StoppedRevitProcessGuard.Instance,
            TestLocalization.For<FileAddinMarkupService>());

    private FileAddinMarkupService NewService(ILogger<FileAddinMarkupService> logger) =>
        new(_parser, logger, StoppedRevitProcessGuard.Instance, TestLocalization.For<FileAddinMarkupService>());

    private const string ValidXml = """
        <RevitAddIns>
          <AddIn Type="Application">
            <Name>A</Name>
            <Assembly>a.dll</Assembly>
            <AddInId>11111111-1111-1111-1111-111111111111</AddInId>
            <FullClassName>A.App</FullClassName>
          </AddIn>
        </RevitAddIns>
        """;

    [Fact]
    public void ReadRaw_ReturnsFileContentAsIs()
    {
        var path = NewFile(ValidXml);

        Assert.Equal(ValidXml, _sut.ReadRaw(FileAt(path)));
    }

    [Fact]
    public void Validate_ValidXml_ReturnsNull()
    {
        Assert.Null(_sut.Validate(ValidXml));
    }

    [Fact]
    public void Validate_BrokenXml_ReturnsMessage()
    {
        var error = _sut.Validate("not xml {{{");

        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_DuplicateAddInId_ReturnsMessage()
    {
        const string xml = """
            <RevitAddIns>
              <AddIn Type="Application">
                <Assembly>a.dll</Assembly>
                <AddInId>22222222-2222-2222-2222-222222222222</AddInId>
                <FullClassName>A.App</FullClassName>
              </AddIn>
              <AddIn Type="Command">
                <Text>Dup</Text>
                <Assembly>b.dll</Assembly>
                <AddInId>22222222-2222-2222-2222-222222222222</AddInId>
                <FullClassName>B.Cmd</FullClassName>
              </AddIn>
            </RevitAddIns>
            """;

        Assert.NotNull(_sut.Validate(xml));
    }

    [Fact]
    public void Save_ValidXml_WritesFileAndBackup()
    {
        var path = NewFile(ValidXml);
        const string updated = """
            <RevitAddIns>
              <AddIn Type="Application">
                <Name>A2</Name>
                <Assembly>a.dll</Assembly>
                <AddInId>11111111-1111-1111-1111-111111111111</AddInId>
                <FullClassName>A.App</FullClassName>
              </AddIn>
            </RevitAddIns>
            """;

        _sut.Save(FileAt(path), updated);

        Assert.Equal(updated, File.ReadAllText(path));
        Assert.Equal(ValidXml, File.ReadAllText(path + ".bak"));
    }

    [Fact]
    public void Save_NoExistingFile_WritesWithoutBackup()
    {
        var root = NewRoot();
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "New.addin");

        _sut.Save(FileAt(path), ValidXml);

        Assert.Equal(ValidXml, File.ReadAllText(path));
        Assert.False(File.Exists(path + ".bak"));
    }

    [Fact]
    public void Save_InvalidXml_ThrowsAndLeavesFileUntouched()
    {
        var path = NewFile(ValidXml);

        Assert.Throws<AddinManifestFormatException>(() => _sut.Save(FileAt(path), "not xml {{{"));
        Assert.Equal(ValidXml, File.ReadAllText(path));
        Assert.False(File.Exists(path + ".bak"));
        Assert.False(File.Exists(path + ".tmp"));
    }

    /// <summary>
    /// Пока жив Revit, запись запрещена (план, раздел 5 — "No .addin moves while live"): бросает
    /// <see cref="RevitRunningException"/> до валидации и записи.
    /// </summary>
    [Fact]
    public void Save_RevitRunning_ThrowsAndLeavesFileUntouched()
    {
        var path = NewFile(ValidXml);
        using var guard = new PollingRevitProcessGuard(
            static () => new HashSet<string>(["2024"], StringComparer.Ordinal));
        guard.Start();
        var service = new FileAddinMarkupService(
            _parser,
            NullLogger<FileAddinMarkupService>.Instance,
            guard,
            TestLocalization.For<FileAddinMarkupService>());

        var exception = Assert.Throws<RevitRunningException>(() => service.Save(FileAt(path), ValidXml));

        Assert.NotEmpty(exception.Message);
        Assert.Equal(ValidXml, File.ReadAllText(path));
        Assert.False(File.Exists(path + ".bak"));
    }

    /// <summary>
    /// Часть плотного логирования "сервисов обновления данных аддинов" — три исхода Save
    /// (успех/отклонено валидацией/сбой записи) должны быть видны в логе на разных уровнях,
    /// а не только через возвращаемое значение/исключение.
    /// </summary>
    [Fact]
    public void Save_ValidXml_LogsInformationWithLength()
    {
        var path = NewFile(ValidXml);
        var logger = new RecordingLogger<FileAddinMarkupService>();
        var service = NewService(logger);

        service.Save(FileAt(path), ValidXml);

        Assert.True(
            logger.HasEntry(LogLevel.Information, "сохранено"),
            "Успешный Save должен залогировать факт сохранения на уровне Information.");
    }

    [Fact]
    public void Save_InvalidXml_LogsWarningWithReason()
    {
        var path = NewFile(ValidXml);
        var logger = new RecordingLogger<FileAddinMarkupService>();
        var service = NewService(logger);

        Assert.Throws<AddinManifestFormatException>(() => service.Save(FileAt(path), "not xml {{{"));

        Assert.True(
            logger.HasEntry(LogLevel.Warning, "отклонено валидацией"),
            "Отклонённая валидацией запись должна быть залогирована на уровне Warning.");
    }

    [Fact]
    public void Save_TargetDirectoryMissing_LogsErrorAndWrapsException()
    {
        // Директория не создана — File.WriteAllText во временный файл бросит
        // DirectoryNotFoundException (наследник IOException), не трогая диск с обходом ОС-прав.
        var root = Path.Combine(Path.GetTempPath(), "AddinManagerTests-" + Guid.NewGuid());
        var path = Path.Combine(root, "Missing.addin");
        var logger = new RecordingLogger<FileAddinMarkupService>();
        var service = NewService(logger);

        var ex = Assert.Throws<IOException>(() => service.Save(FileAt(path), ValidXml));

        Assert.IsAssignableFrom<IOException>(ex.InnerException);
        Assert.True(
            logger.HasEntry(LogLevel.Error, "не удалось записать"),
            "Сбой записи должен быть залогирован на уровне Error с деталями пути.");
    }

    private static AddinFile FileAt(string path) => new(
        Path.GetFileName(path),
        AddinScope.User,
        "2025",
        Enabled: true,
        FullPath: path,
        VersionRootDirectory: Path.GetDirectoryName(path)!,
        Manifest: new AddinManifest([], null, null, null, null, [], [], []));

    private string NewFile(string content)
    {
        var root = NewRoot();
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "A.addin");
        File.WriteAllText(path, content);
        return path;
    }

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
