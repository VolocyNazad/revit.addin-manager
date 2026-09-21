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

        return file with { Enabled = enabled };
    }

    /// <inheritdoc />
    public void Delete(AddinFile file)
    {
        DeleteCallCount++;

        if (ThrowOnNextDelete is { } exception)
        {
            ThrowOnNextDelete = null;
            throw exception;
        }

        foreach (var files in _byVersion.Values)
            files.RemoveAll(f => f.FileName == file.FileName && f.Scope == file.Scope && f.Version == file.Version);
    }

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
