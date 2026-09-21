using AddinManager.Core.Abstractions.Guard;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Guard;
using AddinManager.Core.Manifests;
using AddinManager.Core.Parsing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace AddinManager.Core.Storage;

/// <summary>Файловая реализация <see cref="IAddinStore"/>: настоящие папки Revit под %APPDATA%/%PROGRAMDATA%.</summary>
public sealed class FileSystemAddinStore : IAddinStore
{
    private const string DisabledFolderName = "disabled";

    private readonly IAddinManifestParser _parser;
    private readonly ILogger<FileSystemAddinStore> _logger;
    private readonly IRevitProcessGuard _guard;
    private readonly IStringLocalizer<FileSystemAddinStore> _localizer;
    private readonly string _userBaseDirectory;
    private readonly string _machineBaseDirectory;

    /// <summary>Создает сервис.</summary>
    /// <param name="parser">Разбор .addin манифестов.</param>
    /// <param name="logger">Логгер сканирования и переключения.</param>
    /// <param name="guard">Сторож запущенного Revit — перенос при живом Revit запрещён.</param>
    /// <param name="localizer">Текст отказа при живом Revit.</param>
    /// <param name="userBaseDirectory">
    /// Корень пользовательской области (обычно %APPDATA%); переопределяется в тестах,
    /// чтобы не трогать реальные папки Revit — тот же приём, что и в <c>AddinManager.Theming.ThemeService</c>
    /// (другая сборка, отсюда без <c>cref</c>).
    /// </param>
    /// <param name="machineBaseDirectory">Корень машинной области (обычно %PROGRAMDATA%); переопределяется в тестах.</param>
    public FileSystemAddinStore(
        IAddinManifestParser parser,
        ILogger<FileSystemAddinStore> logger,
        IRevitProcessGuard guard,
        IStringLocalizer<FileSystemAddinStore> localizer,
        string? userBaseDirectory = null,
        string? machineBaseDirectory = null)
    {
        _parser = parser;
        _logger = logger;
        _guard = guard;
        _localizer = localizer;
        _userBaseDirectory = userBaseDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _machineBaseDirectory = machineBaseDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    }

    /// <inheritdoc />
    public IReadOnlyList<AddinFile> ScanVersion(string version)
    {
        _logger.LogDebug("ScanVersion({Version}): начало", version);

        var files = new List<AddinFile>();

        ScanScope(_userBaseDirectory, AddinScope.User, version, files);
        ScanScope(_machineBaseDirectory, AddinScope.Machine, version, files);

        _logger.LogDebug("ScanVersion({Version}): найдено {Count} файлов", version, files.Count);

        return files;
    }

    /// <inheritdoc />
    public AddinFile SetEnabled(AddinFile file, bool enabled)
    {
        if (_guard.IsRunning)
        {
            _logger.LogWarning(
                "SetEnabled({FileName}, {Scope}, {Version}): отклонено — запущен Revit",
                file.FileName, file.Scope, file.Version);
            throw new RevitRunningException(_localizer["BlockedByRunningRevit"]);
        }

        if (file.Enabled == enabled)
        {
            _logger.LogDebug(
                "SetEnabled({FileName}, {Scope}, {Version}): уже {Enabled}, ничего не делаем",
                file.FileName, file.Scope, file.Version, enabled);
            return file;
        }

        var targetDirectory = enabled
            ? file.VersionRootDirectory
            : Path.Combine(file.VersionRootDirectory, DisabledFolderName);
        var targetPath = Path.Combine(targetDirectory, file.FileName);

        _logger.LogInformation(
            "SetEnabled({FileName}, {Scope}, {Version}): {Enabled} — перенос {From} -> {To}",
            file.FileName, file.Scope, file.Version, enabled, file.FullPath, targetPath);

        try
        {
            Directory.CreateDirectory(targetDirectory);
            File.Move(file.FullPath, targetPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "SetEnabled({FileName}, {Scope}, {Version}): не удалось перенести {From} -> {To}",
                file.FileName, file.Scope, file.Version, file.FullPath, targetPath);

            // Не throw; — тот же экземпляр не должен уйти наверх залогированным дважды (S2139):
            // оборачиваем в новое исключение с тем же контекстом, оригинал остаётся в InnerException.
            throw new IOException(
                $"Не удалось перенести '{file.FullPath}' в '{targetPath}' (SetEnabled {file.FileName}, {file.Scope}, {file.Version}).",
                ex);
        }

        _logger.LogInformation(
            "SetEnabled({FileName}, {Scope}, {Version}): перенос выполнен, теперь {Enabled}",
            file.FileName, file.Scope, file.Version, enabled);

        return file with { Enabled = enabled, FullPath = targetPath };
    }

    /// <inheritdoc />
    public void Delete(AddinFile file)
    {
        if (_guard.IsRunning)
        {
            _logger.LogWarning(
                "Delete({FileName}, {Scope}, {Version}): отклонено — запущен Revit",
                file.FileName, file.Scope, file.Version);
            throw new RevitRunningException(_localizer["BlockedByRunningRevit"]);
        }

        _logger.LogInformation(
            "Delete({FileName}, {Scope}, {Version}): удаление {Path}",
            file.FileName, file.Scope, file.Version, file.FullPath);

        try
        {
            File.Delete(file.FullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Delete({FileName}, {Scope}, {Version}): не удалось удалить {Path}",
                file.FileName, file.Scope, file.Version, file.FullPath);

            // Не throw; — тот же экземпляр не должен уйти наверх залогированным дважды (S2139).
            throw new IOException($"Не удалось удалить '{file.FullPath}'.", ex);
        }

        _logger.LogInformation(
            "Delete({FileName}, {Scope}, {Version}): удалён",
            file.FileName, file.Scope, file.Version);
    }

    /// <inheritdoc />
    public AddinFile CreateFile(string fileName, string version, AddinScope scope, bool disabled)
    {
        if (_guard.IsRunning)
        {
            _logger.LogWarning(
                "CreateFile({FileName}, {Scope}, {Version}): отклонено — запущен Revit",
                fileName, scope, version);
            throw new RevitRunningException(_localizer["BlockedByRunningRevit"]);
        }

        var normalizedName = fileName?.Trim() ?? string.Empty;
        if (normalizedName.Length == 0)
            throw new ArgumentException("File name is empty.", nameof(fileName));
        if (!normalizedName.EndsWith(".addin", StringComparison.OrdinalIgnoreCase))
            normalizedName += ".addin";

        var baseDirectory = scope == AddinScope.User ? _userBaseDirectory : _machineBaseDirectory;
        var versionRoot = Path.Combine(baseDirectory, "Autodesk", "Revit", "Addins", version);
        var targetDirectory = disabled ? Path.Combine(versionRoot, DisabledFolderName) : versionRoot;
        var targetPath = Path.Combine(targetDirectory, normalizedName);

        if (File.Exists(targetPath))
        {
            _logger.LogWarning(
                "CreateFile({FileName}, {Scope}, {Version}): уже есть {Path}",
                normalizedName, scope, version, targetPath);
            throw new IOException(_localizer["FileAlreadyExists", normalizedName]);
        }

        var xml = _parser.ToXml(new AddinManifest([], null, null, null, null, [], [], []));

        try
        {
            Directory.CreateDirectory(targetDirectory);
            File.WriteAllText(targetPath, xml);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "CreateFile({FileName}, {Scope}, {Version}): не удалось записать {Path}",
                normalizedName, scope, version, targetPath);

            // Не throw; — тот же экземпляр не должен уйти наверх залогированным дважды (S2139).
            throw new IOException($"Не удалось создать '{targetPath}'.", ex);
        }

        _logger.LogInformation(
            "CreateFile({FileName}, {Scope}, {Version}): создан {Path}",
            normalizedName, scope, version, targetPath);

        // Парсим написанное нами же — заодно проверяем, что создали валидное.
        var manifest = _parser.Parse(xml);
        return new AddinFile(normalizedName, scope, version, !disabled, targetPath, versionRoot, manifest);
    }

    private void ScanScope(string baseDirectory, AddinScope scope, string version, List<AddinFile> files)
    {
        var versionRoot = Path.Combine(baseDirectory, "Autodesk", "Revit", "Addins", version);
        if (!Directory.Exists(versionRoot))
        {
            _logger.LogDebug("ScanScope({Scope}, {Version}): {Root} не найден, пропускаем", scope, version, versionRoot);
            return;
        }

        var countBefore = files.Count;
        ScanFolder(versionRoot, versionRoot, scope, version, enabled: true, files);

        var disabledFolder = Path.Combine(versionRoot, DisabledFolderName);
        if (Directory.Exists(disabledFolder))
            ScanFolder(disabledFolder, versionRoot, scope, version, enabled: false, files);

        _logger.LogDebug(
            "ScanScope({Scope}, {Version}): найдено {Count} файлов в {Root}",
            scope, version, files.Count - countBefore, versionRoot);
    }

    private void ScanFolder(string folder, string versionRoot, AddinScope scope, string version, bool enabled, List<AddinFile> files)
    {
        foreach (var path in Directory.EnumerateFiles(folder, "*.addin"))
        {
            AddinFile? file = null;
            try
            {
                var manifest = _parser.Parse(File.ReadAllText(path));
                file = new AddinFile(Path.GetFileName(path), scope, version, enabled, path, versionRoot, manifest);
            }
            catch (AddinManifestFormatException ex)
            {
                // Битый файл: не роняем сканирование, просто не показываем его (см. IAddinStore.ScanVersion).
                _logger.LogWarning(ex, "Не удалось разобрать манифест {Path}, файл пропущен", path);
            }
            catch (IOException ex)
            {
                // Файл занят/недоступен на момент сканирования — пропускаем, следующий Refresh подхватит его снова.
                _logger.LogWarning(ex, "Файл {Path} недоступен на момент сканирования, пропущен", path);
            }

            if (file is not null)
                files.Add(file);
        }
    }
}
