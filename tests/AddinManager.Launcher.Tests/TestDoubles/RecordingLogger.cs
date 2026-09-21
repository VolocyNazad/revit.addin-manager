using Microsoft.Extensions.Logging;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// Записывает каждый вызов <see cref="ILogger"/> вместо того, чтобы никуда не писать (в отличие
/// от <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger{T}"/>, который тесты берут
/// по умолчанию) — нужен там, где сам факт и уровень логирования конкретного события — часть
/// проверяемого поведения ("это обязано быть залогировано"), а не просто побочный эффект.
/// Тот же двойник, что и в <c>AddinManager.Core.Tests</c> (другая сборка, отсюда без <c>cref</c>
/// и без общего проекта — тестовые проекты друг на друга не ссылаются).
/// </summary>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<Entry> _entries = [];

    /// <summary>Все записи в порядке вызова, включая отфильтрованные по уровню (<see cref="IsEnabled"/> всегда true).</summary>
    public IReadOnlyList<Entry> Entries => _entries;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        _entries.Add(new Entry(logLevel, formatter(state, exception), exception));

    /// <summary>Есть ли запись данного уровня, чьё сообщение содержит подстроку (без учёта регистра).</summary>
    public bool HasEntry(LogLevel level, string containing) =>
        _entries.Any(e => e.Level == level && e.Message.Contains(containing, StringComparison.OrdinalIgnoreCase));

    /// <summary>Одна запись лога: уровень, готовое (отформатированное) сообщение, исключение (если было).</summary>
    public sealed record Entry(LogLevel Level, string Message, Exception? Exception);
}

/// <summary>Пустой scope для <see cref="RecordingLogger{T}.BeginScope{TState}"/>.</summary>
internal sealed class NullScope : IDisposable
{
    public static readonly NullScope Instance = new();

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
