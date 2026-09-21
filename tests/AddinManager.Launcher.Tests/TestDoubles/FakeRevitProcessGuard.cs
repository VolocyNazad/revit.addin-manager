using AddinManager.Core.Abstractions.Guard;
using AddinManager.Core.Guard;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// В памяти, без процессов: <see cref="IRevitProcessGuard"/> для тестов — интересует только
/// подписка на смену множества версий (<see cref="RaiseChanged"/>), а не опрос (тот уже покрыт
/// <c>AddinManager.Core.Tests</c>, другая сборка).
/// </summary>
public sealed class FakeRevitProcessGuard : IRevitProcessGuard
{
    /// <summary>Версии, которые увидит следующий <see cref="RaiseChanged"/>.</summary>
    public IReadOnlySet<string> RunningVersions { get; set; } =
        new HashSet<string>(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool IsRunning => RunningVersions.Count != 0;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Сколько раз вызвали <see cref="Start"/>.</summary>
    public int StartCallCount { get; private set; }

    /// <summary>Сколько раз вызвали <see cref="CheckNow"/>.</summary>
    public int CheckNowCallCount { get; private set; }

    /// <inheritdoc />
    public void Start() => StartCallCount++;

    /// <inheritdoc />
    public void CheckNow() => CheckNowCallCount++;

    /// <summary>Вручную firing <see cref="Changed"/> — имитирует запуск/закрытие Revit.</summary>
    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
