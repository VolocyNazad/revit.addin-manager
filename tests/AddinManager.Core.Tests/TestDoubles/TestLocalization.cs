using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Core.Tests.TestDoubles;

/// <summary>
/// Настоящий <see cref="IStringLocalizer{T}"/> поверх реальных resx тестируемой сборки
/// (нейтральный — английский). Культуру выставляет сам тест (см. <see cref="TestCulture"/>),
/// локайзер читает её лениво при каждом обращении. Фабрике локайзеров нужен
/// <see cref="ILoggerFactory"/> — здесь заглушка, логи самих локайзеров тестам не нужны.
/// Дубликат одноимённого хелпера из <c>AddinManager.Launcher.Tests</c> — тестовые сборки друг
/// на друга не ссылаются.
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
