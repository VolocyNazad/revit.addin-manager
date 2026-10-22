using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// Собирает <see cref="MarkupSearchViewModel"/> для тестов разметки: настоящие resx-строки и
/// пустой логгер — проверяется поведение поиска, а не его локализация в каждом тесте.
/// </summary>
public static class TestMarkupSearch
{
    /// <summary>Новое состояние поиска по разметке.</summary>
    public static MarkupSearchViewModel Create() =>
        new(TestLocalization.For<MarkupSearchViewModel>(), NullLogger<MarkupSearchViewModel>.Instance);
}
