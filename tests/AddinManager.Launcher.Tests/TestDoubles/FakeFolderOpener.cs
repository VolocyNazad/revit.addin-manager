using AddinManager.Launcher.Abstractions.Services;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// Записывает показанные пути вместо запуска проводника: проверяется, что команда
/// передала именно путь выбранного файла.
/// </summary>
public sealed class FakeFolderOpener : IFolderOpener
{
    /// <summary>Пути, переданные в <see cref="Reveal"/>.</summary>
    public List<string> RevealedPaths { get; } = [];

    /// <inheritdoc />
    public void Reveal(string fullPath) => RevealedPaths.Add(fullPath);
}
