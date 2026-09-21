using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Guard;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using AddinManager.Core.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Core.Tests.Storage;

public sealed class FileSystemAddinStoreTests : IDisposable
{
    private readonly List<string> _tempRoots = [];
    private readonly IAddinManifestParser _parser =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());

    [Fact]
    public void ScanVersion_MissingVersionFolder_ReturnsEmpty()
    {
        var store = NewStore(NewRoot(), NewRoot());

        Assert.Empty(store.ScanVersion("2025"));
    }

    [Fact]
    public void ScanVersion_FindsEnabledAndDisabledFiles_WithCorrectFields()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        var disabledDir = Path.Combine(versionDir, "disabled");
        Directory.CreateDirectory(versionDir);
        Directory.CreateDirectory(disabledDir);
        File.WriteAllText(Path.Combine(versionDir, "Enabled.addin"), ValidAddinXml("Enabled"));
        File.WriteAllText(Path.Combine(disabledDir, "Disabled.addin"), ValidAddinXml("Disabled"));

        var files = NewStore(userRoot, NewRoot()).ScanVersion("2025");

        Assert.Equal(2, files.Count);

        var enabled = Assert.Single(files, f => f.FileName == "Enabled.addin");
        Assert.True(enabled.Enabled);
        Assert.Equal(AddinScope.User, enabled.Scope);
        Assert.Equal("2025", enabled.Version);
        Assert.Equal(versionDir, enabled.VersionRootDirectory);
        Assert.Equal(Path.Combine(versionDir, "Enabled.addin"), enabled.FullPath);

        var disabled = Assert.Single(files, f => f.FileName == "Disabled.addin");
        Assert.False(disabled.Enabled);
        Assert.Equal(Path.Combine(disabledDir, "Disabled.addin"), disabled.FullPath);
    }

    [Fact]
    public void ScanVersion_ReadsBothScopes()
    {
        var userRoot = NewRoot();
        var machineRoot = NewRoot();
        Directory.CreateDirectory(VersionDir(userRoot, "2025"));
        Directory.CreateDirectory(VersionDir(machineRoot, "2025"));
        File.WriteAllText(Path.Combine(VersionDir(userRoot, "2025"), "U.addin"), ValidAddinXml("U"));
        File.WriteAllText(Path.Combine(VersionDir(machineRoot, "2025"), "M.addin"), ValidAddinXml("M"));

        var files = NewStore(userRoot, machineRoot).ScanVersion("2025");

        Assert.Equal(AddinScope.User, Assert.Single(files, f => f.FileName == "U.addin").Scope);
        Assert.Equal(AddinScope.Machine, Assert.Single(files, f => f.FileName == "M.addin").Scope);
    }

    [Fact]
    public void ScanVersion_SkipsUnparsableManifest_KeepsGoodOnes()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "Broken.addin"), "not xml {{{");
        File.WriteAllText(Path.Combine(versionDir, "Good.addin"), ValidAddinXml("Good"));

        var files = NewStore(userRoot, NewRoot()).ScanVersion("2025");

        var file = Assert.Single(files);
        Assert.Equal("Good.addin", file.FileName);
    }

    [Fact]
    public void SetEnabled_Disables_MovesToDisabledFolder_AndReturnsUpdatedRecord()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);
        var path = Path.Combine(versionDir, "A.addin");
        File.WriteAllText(path, ValidAddinXml("A"));

        var store = NewStore(userRoot, NewRoot());
        var file = Assert.Single(store.ScanVersion("2025"));

        var updated = store.SetEnabled(file, false);

        var expectedPath = Path.Combine(versionDir, "disabled", "A.addin");
        Assert.False(updated.Enabled);
        Assert.Equal(expectedPath, updated.FullPath);
        Assert.True(File.Exists(expectedPath));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void SetEnabled_Enables_MovesBackToRoot_AndReturnsUpdatedRecord()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        var disabledDir = Path.Combine(versionDir, "disabled");
        Directory.CreateDirectory(disabledDir);
        var path = Path.Combine(disabledDir, "A.addin");
        File.WriteAllText(path, ValidAddinXml("A"));

        var store = NewStore(userRoot, NewRoot());
        var file = Assert.Single(store.ScanVersion("2025"));

        var updated = store.SetEnabled(file, true);

        var expectedPath = Path.Combine(versionDir, "A.addin");
        Assert.True(updated.Enabled);
        Assert.Equal(expectedPath, updated.FullPath);
        Assert.True(File.Exists(expectedPath));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void SetEnabled_AlreadyInTargetState_NoOpKeepsFileInPlace()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);
        var path = Path.Combine(versionDir, "A.addin");
        File.WriteAllText(path, ValidAddinXml("A"));

        var store = NewStore(userRoot, NewRoot());
        var file = Assert.Single(store.ScanVersion("2025"));

        var result = store.SetEnabled(file, true);

        Assert.Equal(file, result);
        Assert.True(File.Exists(path));
    }

    /// <summary>
    /// Создание файла: пишет минимальный валидный манифест (читается обратно без записей),
    /// суффикс <c>.addin</c> дописывается, <c>disabled</c> кладётся в подпапку.
    /// </summary>
    [Fact]
    public void CreateFile_WritesMinimalManifest_WithSuffixAndPlacement()
    {
        var userRoot = NewRoot();
        var store = NewStore(userRoot, NewRoot());

        var enabled = store.CreateFile("Fresh", "2025", AddinScope.User, disabled: false);
        var disabled = store.CreateFile("Old.addin", "2025", AddinScope.User, disabled: true);

        Assert.Equal("Fresh.addin", enabled.FileName);
        Assert.True(enabled.Enabled);
        Assert.Empty(enabled.Manifest.Entries);
        Assert.Equal(Path.Combine(userRoot, "Autodesk", "Revit", "Addins", "2025", "Fresh.addin"), enabled.FullPath);
        Assert.False(disabled.Enabled);
        Assert.Equal(Path.Combine(userRoot, "Autodesk", "Revit", "Addins", "2025", "disabled", "Old.addin"), disabled.FullPath);
        Assert.Equal(2, store.ScanVersion("2025").Count);
    }

    [Fact]
    public void CreateFile_ExistingFile_ThrowsAndKeepsOriginal()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);
        var path = Path.Combine(versionDir, "A.addin");
        File.WriteAllText(path, ValidAddinXml("A"));

        var store = NewStore(userRoot, NewRoot());

        Assert.Throws<IOException>(() => store.CreateFile("A.addin", "2025", AddinScope.User, disabled: false));
        Assert.Equal(ValidAddinXml("A"), File.ReadAllText(path));
    }

    [Fact]
    public void CreateFile_RevitRunning_ThrowsAndWritesNothing()
    {
        var userRoot = NewRoot();
        using var guard = new PollingRevitProcessGuard(
            static () => new HashSet<string>(["2024"], StringComparer.Ordinal));
        guard.Start();
        var store = new FileSystemAddinStore(
            _parser,
            NullLogger<FileSystemAddinStore>.Instance,
            guard,
            TestLocalization.For<FileSystemAddinStore>(),
            userRoot,
            NewRoot());

        Assert.Throws<RevitRunningException>(() => store.CreateFile("A", "2025", AddinScope.User, disabled: false));
        Assert.False(Directory.Exists(VersionDir(userRoot, "2025")));
    }
    /// <summary>
    /// Пока жив Revit, перенос запрещён (план, раздел 5 — "No .addin moves while live"): бросает
    /// <see cref="RevitRunningException"/> до любой работы с диском,
    /// файл остаётся на месте.
    /// </summary>
    [Fact]
    public void SetEnabled_RevitRunning_ThrowsAndKeepsFileInPlace()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);
        var path = Path.Combine(versionDir, "A.addin");
        File.WriteAllText(path, ValidAddinXml("A"));

        using var guard = new PollingRevitProcessGuard(
            static () => new HashSet<string>(["2024"], StringComparer.Ordinal));
        guard.Start();
        var store = new FileSystemAddinStore(
            _parser,
            NullLogger<FileSystemAddinStore>.Instance,
            guard,
            TestLocalization.For<FileSystemAddinStore>(),
            userRoot,
            NewRoot());
        var file = Assert.Single(store.ScanVersion("2025"));

        var exception = Assert.Throws<RevitRunningException>(() => store.SetEnabled(file, false));

        Assert.NotEmpty(exception.Message);
        Assert.True(File.Exists(path));
    }

    /// <summary>
    /// Регрессия на баг, найденный по логам: тумблер true → false → true бросал
    /// FileNotFoundException. Правильный вызывающий код (как теперь и <c>AddinManager.Launcher</c>,
    /// другая сборка, отсюда без <c>cref</c>) каждый раз подставляет в следующий вызов запись,
    /// возвращённую предыдущим.
    /// </summary>
    [Fact]
    public void SetEnabled_SequentialTogglesUsingReturnedFile_NeverThrows()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2021");
        var disabledDir = Path.Combine(versionDir, "disabled");
        Directory.CreateDirectory(disabledDir);
        var path = Path.Combine(disabledDir, "AccentViewManager.addin");
        File.WriteAllText(path, ValidAddinXml("AccentViewManager"));

        var store = NewStore(userRoot, NewRoot());
        var file = Assert.Single(store.ScanVersion("2021"));

        // Точная последовательность из отчёта об ошибке: true, false, true.
        file = store.SetEnabled(file, true);
        file = store.SetEnabled(file, false);
        file = store.SetEnabled(file, true);

        Assert.True(file.Enabled);
        Assert.True(File.Exists(Path.Combine(versionDir, "AccentViewManager.addin")));
    }

    /// <summary>
    /// Документирует сам баг: если вызывающий код игнорирует возвращённую запись и продолжает
    /// дергать <see cref="AddinManager.Core.Abstractions.Storage.IAddinStore.SetEnabled"/> со старой (см. контракт в IAddinStore),
    /// второй вызов молча ничего не делает (state кажется уже нужным), а третий падает —
    /// реальный файл к этому моменту лежит не там, где думает вызывающий код.
    /// </summary>
    [Fact]
    public void SetEnabled_CallerIgnoresReturnedFile_ThrowsOnThirdToggle()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2021");
        var disabledDir = Path.Combine(versionDir, "disabled");
        Directory.CreateDirectory(disabledDir);
        var path = Path.Combine(disabledDir, "AccentViewManager.addin");
        File.WriteAllText(path, ValidAddinXml("AccentViewManager"));

        var store = NewStore(userRoot, NewRoot());
        var staleFile = Assert.Single(store.ScanVersion("2021"));

        store.SetEnabled(staleFile, true); // Реальный перенос disabled/ -> корень; результат отброшен намеренно.
        store.SetEnabled(staleFile, false); // staleFile.Enabled уже false -> no-op, хотя на диске файл включён.

        // File.Move бросает FileNotFoundException, SetEnabled оборачивает её в IOException (см. SetEnabled).
        var ex = Assert.Throws<IOException>(() => store.SetEnabled(staleFile, true));
        Assert.IsType<FileNotFoundException>(ex.InnerException);
    }

    [Fact]
    public void SetEnabled_FileGoneFromDisk_LogsAndWrapsException()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);
        var path = Path.Combine(versionDir, "A.addin");
        File.WriteAllText(path, ValidAddinXml("A"));

        var store = NewStore(userRoot, NewRoot());
        var file = Assert.Single(store.ScanVersion("2025"));

        File.Delete(path); // Файл исчез с диска между сканированием и переключением.

        var ex = Assert.Throws<IOException>(() => store.SetEnabled(file, false));
        Assert.IsType<FileNotFoundException>(ex.InnerException);
    }

    /// <summary>
    /// Часть плотного логирования "сервисов обновления данных аддинов" — успешный перенос должен
    /// быть виден в логе на уровне Information (см. FileSystemAddinStore.SetEnabled), не только
    /// намерение (лог до File.Move) и не только ошибка (лог в catch).
    /// </summary>
    [Fact]
    public void SetEnabled_Succeeds_LogsInformation()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "A.addin"), ValidAddinXml("A"));

        var logger = new RecordingLogger<FileSystemAddinStore>();
        var store = NewStore(userRoot, NewRoot(), logger);
        var file = Assert.Single(store.ScanVersion("2025"));

        store.SetEnabled(file, false);

        Assert.True(
            logger.HasEntry(LogLevel.Information, "перенос выполнен"),
            "Успешный SetEnabled должен залогировать факт переноса на уровне Information.");
    }

    [Fact]
    public void SetEnabled_AlreadyInTargetState_DoesNotLogSuccess()
    {
        var userRoot = NewRoot();
        var versionDir = VersionDir(userRoot, "2025");
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "A.addin"), ValidAddinXml("A"));

        var logger = new RecordingLogger<FileSystemAddinStore>();
        var store = NewStore(userRoot, NewRoot(), logger);
        var file = Assert.Single(store.ScanVersion("2025"));

        store.SetEnabled(file, true); // уже enabled=true — no-op ветка, реального переноса нет

        Assert.False(
            logger.HasEntry(LogLevel.Information, "перенос выполнен"),
            "No-op ветка SetEnabled не должна логировать успешный перенос — переноса не было.");
    }

    /// <summary>
    /// Часть плотного логирования: сканирование отсутствующей папки версии — нередкий, ожидаемый
    /// случай (не у всех Revit-версий есть addin'ы в обоих scope), но он должен быть виден в
    /// логе на уровне Debug (см. FileSystemAddinStore.ScanScope), а не проходить бесследно.
    /// </summary>
    [Fact]
    public void ScanVersion_MissingVersionFolder_LogsDebug()
    {
        var logger = new RecordingLogger<FileSystemAddinStore>();
        var store = NewStore(NewRoot(), NewRoot(), logger);

        store.ScanVersion("2025");

        Assert.True(
            logger.HasEntry(LogLevel.Debug, "не найден"),
            "Отсутствующая папка версии должна быть залогирована на уровне Debug.");
    }

    [Fact]
    public void ScanVersion_FindsFiles_LogsCountPerScope()
    {
        var userRoot = NewRoot();
        Directory.CreateDirectory(VersionDir(userRoot, "2025"));
        File.WriteAllText(Path.Combine(VersionDir(userRoot, "2025"), "A.addin"), ValidAddinXml("A"));

        var logger = new RecordingLogger<FileSystemAddinStore>();
        var store = NewStore(userRoot, NewRoot(), logger);

        store.ScanVersion("2025");

        Assert.True(
            logger.HasEntry(LogLevel.Debug, "найдено 1 файлов"),
            "Найденные в scope файлы должны быть залогированы с их количеством.");
    }

    private FileSystemAddinStore NewStore(string userRoot, string machineRoot, ILogger<FileSystemAddinStore>? logger = null) =>
        new(_parser, logger ?? NullLogger<FileSystemAddinStore>.Instance, StoppedRevitProcessGuard.Instance, TestLocalization.For<FileSystemAddinStore>(), userRoot, machineRoot);

    private static string VersionDir(string baseDirectory, string version) =>
        Path.Combine(baseDirectory, "Autodesk", "Revit", "Addins", version);

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
