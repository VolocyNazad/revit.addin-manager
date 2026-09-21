using AddinManager.Core.Abstractions.Guard;
using AddinManager.Core.Guard;

namespace AddinManager.Core.Tests.TestDoubles;

/// <summary>
/// Никогда не запущенный сторож для тестов сервисов: опрос не стартует, множество версий
/// пусто. Общий, без состояния — потокобезопасен для параллельных тестов.
/// </summary>
public static class StoppedRevitProcessGuard
{
    /// <summary>Общий остановленный сторож.</summary>
    public static IRevitProcessGuard Instance { get; } =
        new PollingRevitProcessGuard(static () => new HashSet<string>(StringComparer.Ordinal));
}
