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
    private readonly IAddinManifestParser _parser;
    private readonly ILogger<FileSystemAddinStore> _logger;
    private readonly IRevitProcessGuard _guard;
    private readonly IStringLocalizer<FileSystemAddinStore> _localizer;
    private readonly IFileRecycler? _recycler;
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
    /// <param name="recycler">Корзина для удаления; без неё файл удаляется безвозвратно (тесты ядра).</param>
    public FileSystemAddinStore(
        IAddinManifestParser parser,
        ILogger<FileSystemAddinStore> logger,
        IRevitProcessGuard guard,
        IStringLocalizer<FileSystemAddinStore> localizer,
        string? userBaseDirectory = null,
        string? machineBaseDirectory = null,
        IFileRecycler? recycler = null)
    {
        _parser = parser;
        _logger = logger;
        _guard = guard;
        _localizer = localizer;
        _userBaseDirectory = userBaseDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _machineBaseDirectory = machineBaseDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        _recycler = recycler;
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
        ThrowIfRevitRunning("SetEnabled", file.FileName, file.Scope, file.Version);

        if (file.Enabled == enabled)
        {
            _logger.LogDebug(
                "SetEnabled({FileName}, {Scope}, {Version}): уже {Enabled}, ничего не делаем",
                file.FileName, file.Scope, file.Version, enabled);
            return file;
        }

        var targetDirectory = AddinFolderLayout.GetFileFolder(file.VersionRootDirectory, enabled);
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
            throw new IOException(_localizer["SetEnabledFailed", file.FullPath, targetPath], ex);
        }

        _logger.LogInformation(
            "SetEnabled({FileName}, {Scope}, {Version}): перенос выполнен, теперь {Enabled}",
            file.FileName, file.Scope, file.Version, enabled);

        return file with { Enabled = enabled, FullPath = targetPath };
    }

    /// <inheritdoc />
    public AddinFile Rename(AddinFile file, string newFileName)
    {
        ThrowIfRevitRunning("Rename", file.FileName, file.Scope, file.Version);

        var normalizedName = AddinFileNames.Normalize(newFileName);
        if (normalizedName.Length == 0)
            throw new IOException(_localizer["EmptyFileName"]);

        if (normalizedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new IOException(_localizer["InvalidFileName"]);

        var targetDirectory = AddinFolderLayout.GetFileFolder(file.VersionRootDirectory, file.Enabled);
        var targetPath = Path.Combine(targetDirectory, normalizedName);

        if (string.Equals(targetPath, file.FullPath, StringComparison.Ordinal))
            return file;

        if (!string.Equals(targetPath, file.FullPath, StringComparison.OrdinalIgnoreCase)
            && File.Exists(targetPath))
        {
            throw new IOException(_localizer["FileAlreadyExists", normalizedName]);
        }

        _logger.LogInformation(
            "Rename({FileName}, {Scope}, {Version}): {From} -> {To}",
            file.FileName, file.Scope, file.Version, file.FullPath, targetPath);

        try
        {
            if (string.Equals(targetPath, file.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                // Переименование только регистра: Windows не отличит старый путь от нового,
                // поэтому идём через промежуточное имя в той же папке.
                var temporaryPath = Path.Combine(targetDirectory, $"{Guid.NewGuid():N}.tmp");
                File.Move(file.FullPath, temporaryPath);
                try
                {
                    File.Move(temporaryPath, targetPath);
                }
                catch (Exception)
                {
                    File.Move(temporaryPath, file.FullPath);
                    throw;
                }
            }
            else
            {
                File.Move(file.FullPath, targetPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Rename({FileName}, {Scope}, {Version}): не удалось переименовать {From} -> {To}",
                file.FileName, file.Scope, file.Version, file.FullPath, targetPath);

            // Не throw; — тот же экземпляр не должен уйти наверх залогированным дважды (S2139).
            throw new IOException(_localizer["RenameFailed", file.FullPath], ex);
        }

        return file with { FileName = normalizedName, FullPath = targetPath };
    }

    /// <inheritdoc />
    public byte[] Delete(AddinFile file)
    {
        ThrowIfRevitRunning("Delete", file.FileName, file.Scope, file.Version);

        _logger.LogInformation(
            "Delete({FileName}, {Scope}, {Version}): удаление {Path}",
            file.FileName, file.Scope, file.Version, file.FullPath);

        byte[] content;
        try
        {
            content = File.ReadAllBytes(file.FullPath);
            if (_recycler is not null)
            {
                _recycler.Recycle(file.FullPath);
            }
            else
            {
                // Без корзины удаление необратимо — это осознанное поведение ядра для тестов,
                // но оно обязано быть видно в логе (development policy: fallback must be logged).
                _logger.LogWarning(
                    "Delete({FileName}, {Scope}, {Version}): файл {Path} удаляется безвозвратно — IFileRecycler не передан",
                    file.FileName, file.Scope, file.Version, file.FullPath);
                File.Delete(file.FullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Delete({FileName}, {Scope}, {Version}): не удалось удалить {Path}",
                file.FileName, file.Scope, file.Version, file.FullPath);

            // Не throw; — тот же экземпляр не должен уйти наверх залогированным дважды (S2139).
            throw new IOException(_localizer["DeleteFailed", file.FullPath], ex);
        }

        _logger.LogInformation(
            "Delete({FileName}, {Scope}, {Version}): удалён",
            file.FileName, file.Scope, file.Version);

        return content;
    }

    /// <inheritdoc />
    public void Restore(string fullPath, byte[] content)
    {
        ThrowIfRevitRunning("Restore", Path.GetFileName(fullPath));

        if (File.Exists(fullPath))
        {
            _logger.LogWarning(
                "Restore({Path}): файл уже есть, перезапись запрещена",
                fullPath);
            throw new IOException(_localizer["RestoreTargetExists", Path.GetFileName(fullPath)]);
        }

        var targetDirectory = Path.GetDirectoryName(fullPath);

        // Уникальный временный файл в той же папке: File.Move в пределах одного тома атомарен,
        // а уникальный суффикс не даёт двум параллельным восстановлениям затереть temp друг друга.
        var temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            if (!string.IsNullOrEmpty(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            File.WriteAllBytes(temporaryPath, content);
            File.Move(temporaryPath, fullPath);
        }
        catch (Exception ex)
        {
            TryDeleteTemporaryFile(temporaryPath);

            _logger.LogError(
                ex,
                "Restore({Path}): не удалось восстановить файл",
                fullPath);

            // Не throw; — тот же экземпляр не должен уйти наверх залогированным дважды (S2139).
            throw new IOException(_localizer["RestoreFailed", Path.GetFileName(fullPath)], ex);
        }

        _logger.LogInformation(
            "Restore({Path}): восстановлено, {Length} байт",
            fullPath, content.Length);
    }

    /// <summary>
    /// Best-effort cleanup of the temporary file left by a failed <see cref="Restore"/>: the
    /// original failure is what the caller must see, so a cleanup failure stays silent.
    /// </summary>
    private static void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch (IOException)
        {
            // Уборка временного файла — best effort: исходная ошибка важнее.
        }
        catch (UnauthorizedAccessException)
        {
            // Уборка временного файла — best effort: исходная ошибка важнее.
        }
    }

    /// <inheritdoc />
    public string GetTargetPath(string fileName, string version, AddinScope scope, bool enabled)
    {
        var versionRoot = GetVersionRoot(scope, version);
        var directory = AddinFolderLayout.GetFileFolder(versionRoot, enabled);
        return Path.Combine(directory, fileName);
    }

    /// <inheritdoc />
    public bool TargetExists(string fileName, string version, AddinScope scope, bool enabled) =>
        File.Exists(GetTargetPath(fileName, version, scope, enabled));

    /// <inheritdoc />
    public FileMoveResult Move(AddinFile file, string targetVersion, AddinScope targetScope, bool enabled, bool overwrite) =>
        Transfer(file, targetVersion, targetScope, enabled, overwrite, copy: false);

    /// <inheritdoc />
    public FileMoveResult Copy(AddinFile file, string targetVersion, AddinScope targetScope, bool enabled, bool overwrite) =>
        Transfer(file, targetVersion, targetScope, enabled, overwrite, copy: true);

    private FileMoveResult Transfer(
        AddinFile file,
        string targetVersion,
        AddinScope targetScope,
        bool enabled,
        bool overwrite,
        bool copy)
    {
        ThrowIfRevitRunning(copy ? "Copy" : "Move", file.FileName, file.Scope, file.Version);

        var targetPath = GetTargetPath(file.FileName, targetVersion, targetScope, enabled);
        if (string.Equals(targetPath, file.FullPath, StringComparison.OrdinalIgnoreCase))
            throw new IOException(_localizer["SameFolder"]);

        string? replacedPath = null;
        byte[]? replacedContent = null;
        try
        {
            if (File.Exists(targetPath))
            {
                if (!overwrite)
                    throw new IOException(_localizer["FileAlreadyExists", file.FileName]);

                replacedPath = targetPath;
                replacedContent = File.ReadAllBytes(targetPath);
                if (_recycler is not null)
                {
                    _recycler.Recycle(targetPath);
                }
                else
                {
                    // Без корзины замена необратима — осознанный fallback ядра, но он обязан быть в логе.
                    _logger.LogWarning(
                        "{Operation}({FileName}, {Scope}, {Version}): заменяемый файл {Path} удаляется безвозвратно — IFileRecycler не передан",
                        copy ? "Copy" : "Move", file.FileName, file.Scope, file.Version, targetPath);
                    File.Delete(targetPath);
                }
            }

            var targetRoot = GetVersionRoot(targetScope, targetVersion);
            var targetDirectory = AddinFolderLayout.GetFileFolder(targetRoot, enabled);
            Directory.CreateDirectory(targetDirectory);

            _logger.LogInformation(
                "{Operation}({FileName}, {Scope}, {Version}): {From} -> {To}",
                copy ? "Copy" : "Move", file.FileName, file.Scope, file.Version, file.FullPath, targetPath);

            if (copy)
                File.Copy(file.FullPath, targetPath);
            else
                File.Move(file.FullPath, targetPath);

            var moved = file with
            {
                Version = targetVersion,
                Scope = targetScope,
                Enabled = enabled,
                FullPath = targetPath,
                VersionRootDirectory = targetRoot,
            };
            return new FileMoveResult(moved, replacedPath, replacedContent);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(
                ex,
                "{Operation}({FileName}, {Scope}, {Version}): не удалось {From} -> {To}",
                copy ? "Copy" : "Move", file.FileName, file.Scope, file.Version, file.FullPath, targetPath);
            throw new IOException(
                _localizer[copy ? "CopyFailed" : "MoveFailed", file.FullPath],
                ex);
        }
    }

    private string GetVersionRoot(AddinScope scope, string version)
    {
        var baseDirectory = scope == AddinScope.User ? _userBaseDirectory : _machineBaseDirectory;
        return AddinFolderLayout.GetVersionRoot(baseDirectory, version);
    }

    /// <summary>
    /// Rejects a disk mutation while Revit runs: the log line and the exception carry the same
    /// operation context as the former per-method copies.
    /// </summary>
    private void ThrowIfRevitRunning(string operation, string fileName, AddinScope scope, string version)
    {
        if (!_guard.IsRunning)
            return;

        _logger.LogWarning(
            "{Operation}({FileName}, {Scope}, {Version}): отклонено — запущен Revit",
            operation, fileName, scope, version);
        throw new RevitRunningException(_localizer["BlockedByRunningRevit"]);
    }

    /// <summary>
    /// Same refusal for a mutation that addresses a bare path and therefore has no scope/version
    /// context (see <see cref="Restore"/>).
    /// </summary>
    private void ThrowIfRevitRunning(string operation, string fileName)
    {
        if (!_guard.IsRunning)
            return;

        _logger.LogWarning(
            "{Operation}({FileName}): отклонено — запущен Revit",
            operation, fileName);
        throw new RevitRunningException(_localizer["BlockedByRunningRevit"]);
    }

    /// <inheritdoc />
    public AddinFile CreateFile(string fileName, string version, AddinScope scope, bool disabled)
    {
        ThrowIfRevitRunning("CreateFile", fileName, scope, version);

        var normalizedName = AddinFileNames.Normalize(fileName);
        if (normalizedName.Length == 0)
            throw new ArgumentException(_localizer["EmptyFileName"], nameof(fileName));

        var versionRoot = GetVersionRoot(scope, version);
        var targetDirectory = AddinFolderLayout.GetFileFolder(versionRoot, enabled: !disabled);
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
            throw new IOException(_localizer["CreateFileFailed", targetPath], ex);
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
        var versionRoot = AddinFolderLayout.GetVersionRoot(baseDirectory, version);
        if (!Directory.Exists(versionRoot))
        {
            _logger.LogDebug("ScanScope({Scope}, {Version}): {Root} не найден, пропускаем", scope, version, versionRoot);
            return;
        }

        var countBefore = files.Count;
        ScanFolder(versionRoot, versionRoot, scope, version, enabled: true, files);

        var disabledFolder = AddinFolderLayout.GetFileFolder(versionRoot, enabled: false);
        if (Directory.Exists(disabledFolder))
            ScanFolder(disabledFolder, versionRoot, scope, version, enabled: false, files);

        _logger.LogDebug(
            "ScanScope({Scope}, {Version}): найдено {Count} файлов в {Root}",
            scope, version, files.Count - countBefore, versionRoot);
    }

    private void ScanFolder(string folder, string versionRoot, AddinScope scope, string version, bool enabled, List<AddinFile> files)
    {
        foreach (var path in Directory.EnumerateFiles(folder, AddinFolderLayout.AddinFilePattern))
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
