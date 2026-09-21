using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Storage;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// Слежение не запускается по-настоящему — <see cref="Raise"/> позволяет тесту сымитировать
/// "диск изменился" синхронно, без реального <see cref="System.IO.FileSystemWatcher"/> и без
/// ожидания дебаунса.
/// </summary>
public sealed class FakeAddinChangeWatcher : IAddinChangeWatcher
{
    /// <summary>Сколько раз вызвали <see cref="Start"/>.</summary>
    public int StartCallCount { get; private set; }

    /// <summary>Был ли вызван <see cref="Dispose"/>.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public void Start() => StartCallCount++;

    /// <summary>Синхронно поднимает <see cref="Changed"/>, как будто диск изменился извне.</summary>
    public void Raise() => Changed?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    public void Dispose() => IsDisposed = true;
}
