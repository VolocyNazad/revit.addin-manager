using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// Настоящий <see cref="IStringLocalizer{T}"/> поверх реальных resx тестируемой сборки
/// (нейтральный — английский): тесты ViewModel'ей проверяют поведение со строками, а не
/// подменяют каждый ключ фейком. Культуру выставляет сам тест (см. <see cref="TestCulture"/>),
/// локайзер читает её лениво при каждом обращении. Фабрике локайзеров нужен
/// <see cref="ILoggerFactory"/> — здесь заглушка, логи самих локайзеров тестам не нужны.
/// </summary>
public static class TestLocalization
{
    private static readonly ServiceProvider Provider = new ServiceCollection()
        .AddLocalization(options => options.ResourcesPath = string.Empty)
        .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
        .BuildServiceProvider();

    /// <summary>Локайзер для типа из тестируемой сборки.</summary>
    public static IStringLocalizer<T> For<T>() => Provider.GetRequiredService<IStringLocalizer<T>>();
}
