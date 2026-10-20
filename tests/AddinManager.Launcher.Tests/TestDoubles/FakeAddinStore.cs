using System.IO;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Manifests;
using AddinManager.Core.Storage;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// В памяти, без диска: тесты ViewModel'ей не должны зависеть от реального
/// <see cref="System.IO.FileSystemWatcher"/>/файловой системы, чтобы проверить именно логику
/// ViewModel'я (кто кого дергает), а не поведение диска — то уже покрыто
/// <c>AddinManager.Core.Tests</c> (другая сборка, отсюда без <c>cref</c>).
/// </summary>
public sealed class FakeAddinStore : IAddinStore
{
    private readonly Dictionary<string, List<AddinFile>> _byVersion = [];

    /// <summary>Сколько раз вызвали <see cref="ScanVersion"/> — суммарно по всем версиям.</summary>
    public int ScanCallCount { get; private set; }

    /// <summary>Подменяет то, что вернёт <see cref="ScanVersion"/> для конкретной версии.</summary>
    public void SetFiles(string version, params AddinFile[] files) => _byVersion[version] = [.. files];

    /// <summary>
    /// Когда задано, следующий вызов <see cref="SetEnabled"/> бросает это исключение вместо
    /// обычного успешного переноса — имитирует, например, <see cref="UnauthorizedAccessException"/>
    /// из <see cref="FileSystemAddinStore"/> (другая сборка, отсюда без <c>cref</c>), когда
    /// область Machine недоступна для записи. Одноразовое: после броска сбрасывается в <c>null</c>,
    /// так что откат (следующий вызов с восстановленным значением) снова проходит успешно —
    /// точно как в реальном <see cref="FileSystemAddinStore"/>, где повторный SetEnabled с уже
    /// совпадающим состоянием ничего не переносит и не бросает.
    /// </summary>
    public Exception? ThrowOnNextSetEnabled { get; set; }

    /// <summary>Сколько раз реально вызвали <see cref="SetEnabled"/> (успешно или с броском).</summary>
    public int SetEnabledCallCount { get; private set; }

    /// <summary>
    /// Когда задано, следующий вызов <see cref="Delete"/> бросает это исключение вместо
    /// удаления — одноразовое, как <see cref="ThrowOnNextSetEnabled"/>.
    /// </summary>
    public Exception? ThrowOnNextDelete { get; set; }

    /// <summary>Сколько раз реально вызвали <see cref="Delete"/> (успешно или с броском).</summary>
    public int DeleteCallCount { get; private set; }

    /// <summary>
    /// Когда задано, следующий вызов <see cref="CreateFile"/> бросает это исключение вместо
    /// создания — одноразовое, как <see cref="ThrowOnNextSetEnabled"/>.
    /// </summary>
    public Exception? ThrowOnNextCreateFile { get; set; }

    /// <summary>Когда задано, следующий вызов <see cref="Move"/> бросает это исключение.</summary>
    public Exception? ThrowOnNextMove { get; set; }

    /// <summary>Когда задано, следующий вызов <see cref="Copy"/> бросает это исключение.</summary>
    public Exception? ThrowOnNextCopy { get; set; }

    /// <summary>Сколько раз вызывали <see cref="Move"/>.</summary>
    public int MoveCallCount { get; private set; }

    /// <summary>Сколько раз вызывали <see cref="Copy"/>.</summary>
    public int CopyCallCount { get; private set; }

    /// <inheritdoc />
    public string GetTargetPath(string fileName, string version, AddinScope scope, bool enabled) =>
        enabled
            ? $@"C:\fake\{version}\{fileName}"
            : $@"C:\fake\{version}\disabled\{fileName}";

    /// <inheritdoc />
    public bool TargetExists(string fileName, string version, AddinScope scope, bool enabled)
    {
        var path = GetTargetPath(fileName, version, scope, enabled);
        return _byVersion.Values.SelectMany(files => files)
            .Any(file => string.Equals(file.FullPath, path, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public FileMoveResult Move(AddinFile file, string targetVersion, AddinScope targetScope, bool enabled, bool overwrite)
    {
        MoveCallCount++;
        if (ThrowOnNextMove is { } exception)
        {
            ThrowOnNextMove = null;
            throw exception;
        }

        RemoveFromVersion(file);
        var moved = Place(file, targetVersion, targetScope, enabled, overwrite);
        return new FileMoveResult(moved, null, null);
    }

    /// <inheritdoc />
    public FileMoveResult Copy(AddinFile file, string targetVersion, AddinScope targetScope, bool enabled, bool overwrite)
    {
        CopyCallCount++;
        if (ThrowOnNextCopy is { } exception)
        {
            ThrowOnNextCopy = null;
            throw exception;
        }

        return new FileMoveResult(Place(file, targetVersion, targetScope, enabled, overwrite), null, null);
    }

    private AddinFile Place(AddinFile file, string targetVersion, AddinScope targetScope, bool enabled, bool overwrite)
    {
        var targetPath = GetTargetPath(file.FileName, targetVersion, targetScope, enabled);
        var existing = _byVersion.Values.SelectMany(files => files)
            .FirstOrDefault(item => string.Equals(item.FullPath, targetPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            if (!overwrite)
                throw new IOException($"File '{file.FileName}' already exists.");
            RemoveFromVersion(existing);
        }

        var placed = file with
        {
            Version = targetVersion,
            Scope = targetScope,
            Enabled = enabled,
            FullPath = targetPath,
            VersionRootDirectory = $@"C:\fake\{targetVersion}",
        };
        if (!_byVersion.TryGetValue(targetVersion, out var files))
            _byVersion[targetVersion] = files = [];
        files.Add(placed);
        return placed;
    }

    private void RemoveFromVersion(AddinFile file)
    {
        foreach (var files in _byVersion.Values)
            files.RemoveAll(item => item.FileName == file.FileName && item.Scope == file.Scope && item.Version == file.Version);
    }

    /// <inheritdoc />
    public IReadOnlyList<AddinFile> ScanVersion(string version)
    {
        ScanCallCount++;
        return _byVersion.TryGetValue(version, out var files) ? files : [];
    }

    /// <inheritdoc />
    public AddinFile SetEnabled(AddinFile file, bool enabled)
    {
        SetEnabledCallCount++;

        if (ThrowOnNextSetEnabled is { } exception)
        {
            ThrowOnNextSetEnabled = null;
            throw exception;
        }

        var updated = file with { Enabled = enabled };
        foreach (var files in _byVersion.Values)
        {
            var index = files.FindIndex(f => f.FileName == file.FileName && f.Scope == file.Scope && f.Version == file.Version);
            if (index >= 0)
                files[index] = updated;
        }

        return updated;
    }

    /// <inheritdoc />
    public byte[] Delete(AddinFile file)
    {
        DeleteCallCount++;

        if (ThrowOnNextDelete is { } exception)
        {
            ThrowOnNextDelete = null;
            throw exception;
        }

        foreach (var files in _byVersion.Values)
            files.RemoveAll(f => f.FileName == file.FileName && f.Scope == file.Scope && f.Version == file.Version);

        return [];
    }

    /// <inheritdoc />
    public AddinFile Rename(AddinFile file, string newFileName)
    {
        RenameCallCount++;

        if (ThrowOnNextRename is { } exception)
        {
            ThrowOnNextRename = null;
            throw exception;
        }

        var name = newFileName.Trim();
        if (!name.EndsWith(".addin", StringComparison.OrdinalIgnoreCase))
            name += ".addin";

        var targetPath = Path.Combine(Path.GetDirectoryName(file.FullPath) ?? string.Empty, name);
        var renamed = file with { FileName = name, FullPath = targetPath };

        foreach (var files in _byVersion.Values)
        {
            var index = files.FindIndex(f => f.FileName == file.FileName && f.Scope == file.Scope && f.Version == file.Version);
            if (index >= 0)
                files[index] = renamed;
        }

        return renamed;
    }

    /// <summary>Когда задано, следующий вызов <see cref="Rename"/> бросает это исключение.</summary>
    public Exception? ThrowOnNextRename { get; set; }

    /// <summary>Сколько раз вызывали <see cref="Rename"/>.</summary>
    public int RenameCallCount { get; private set; }

    /// <inheritdoc />
    public AddinFile CreateFile(string fileName, string version, AddinScope scope, bool disabled)
    {
        if (ThrowOnNextCreateFile is { } exception)
        {
            ThrowOnNextCreateFile = null;
            throw exception;
        }

        var name = fileName.Trim();
        if (!name.EndsWith(".addin", StringComparison.OrdinalIgnoreCase))
            name += ".addin";

        var root = $@"C:\fake\{version}";
        var file = new AddinFile(
            name, scope, version, !disabled,
            FullPath: disabled ? $@"{root}\disabled\{name}" : $@"{root}\{name}",
            VersionRootDirectory: root,
            Manifest: new AddinManifest([], null, null, null, null, [], [], []));
        SetFiles(version, [.. ScanVersion(version), file]);
        return file;
    }
}
