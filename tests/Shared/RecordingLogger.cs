using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace AddinManager.Testing;

/// <summary>
/// Записывает каждый вызов <see cref="ILogger"/> вместо того, чтобы никуда не писать (в отличие
/// от <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger{T}"/>, который тесты берут
/// по умолчанию) — нужен там, где сам факт и уровень логирования конкретного события — часть
/// проверяемого поведения ("это обязано быть залогировано"), а не просто побочный эффект.
/// Хранит и шаблон, и структурированные поля, поэтому проверка не ломается от формулировки или
/// плюрализации сообщения. Общий хелпер тестовых проектов (linked source), а не копия в каждом.
/// </summary>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private static readonly IReadOnlyDictionary<string, object?> NoProperties =
        ReadOnlyDictionary<string, object?>.Empty;

    private readonly List<Entry> _entries = [];

    /// <summary>Все записи в порядке вызова, включая отфильтрованные по уровню (<see cref="IsEnabled"/> всегда true).</summary>
    public IReadOnlyList<Entry> Entries => _entries;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        var template = message;
        IReadOnlyDictionary<string, object?> properties = NoProperties;

        if (state is IReadOnlyList<KeyValuePair<string, object?>> values)
        {
            var structured = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (key, value) in values)
            {
                if (key == "{OriginalFormat}")
                    template = value as string ?? message;
                else
                    structured[key] = value;
            }

            properties = new ReadOnlyDictionary<string, object?>(structured);
        }

        _entries.Add(new Entry(logLevel, eventId, message, template, properties, exception));
    }

    /// <summary>Есть ли запись данного уровня, чьё отформатированное сообщение содержит подстроку (без учёта регистра).</summary>
    public bool HasEntry(LogLevel level, string containing) =>
        _entries.Any(e => e.Level == level && e.Message.Contains(containing, StringComparison.OrdinalIgnoreCase));

    /// <summary>Есть ли запись данного уровня, чей шаблон содержит подстроку (без учёта регистра).</summary>
    public bool HasTemplate(LogLevel level, string containing) =>
        _entries.Any(e => e.Level == level && e.Template.Contains(containing, StringComparison.OrdinalIgnoreCase));

    /// <summary>Есть ли запись данного уровня с указанным структурированным полем.</summary>
    public bool HasProperty(LogLevel level, string property, object? value) =>
        _entries.Any(e => e.Level == level
            && e.Properties.TryGetValue(property, out var actual)
            && Equals(actual, value));

    /// <summary>Одна запись лога: уровень, готовое сообщение, шаблон, структурированные поля и исключение.</summary>
    /// <param name="Level">Уровень.</param>
    /// <param name="EventId">Идентификатор события.</param>
    /// <param name="Message">Отформатированное сообщение.</param>
    /// <param name="Template">Исходный шаблон с плейсхолдерами.</param>
    /// <param name="Properties">Структурированные поля (кроме <c>{OriginalFormat}</c>).</param>
    /// <param name="Exception">Исключение, если было.</param>
    public sealed record Entry(
        LogLevel Level,
        EventId EventId,
        string Message,
        string Template,
        IReadOnlyDictionary<string, object?> Properties,
        Exception? Exception);
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
