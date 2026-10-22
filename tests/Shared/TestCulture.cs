using System.Globalization;

namespace AddinManager.Testing;

/// <summary>
/// Явно выставляет культуру UI на время теста и возвращает обе — текущего потока и
/// <see cref="CultureInfo.DefaultThreadCurrentUICulture"/> — обратно: утверждаемый
/// локализованный текст не должен зависеть от языка ОС, а фоновые/async пути (watcher,
/// отложенные действия) видят именно культуру по умолчанию. Общий хелпер тестовых проектов
/// (linked source), а не копия в каждом.
/// </summary>
public static class TestCulture
{
    /// <summary>Выполняет <paramref name="action"/> под культурой <paramref name="name"/>.</summary>
    public static void RunIn(string name, Action action)
    {
        var culture = new CultureInfo(name);
        var previousUiCulture = CultureInfo.CurrentUICulture;
        var previousDefaultCulture = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
            CultureInfo.DefaultThreadCurrentUICulture = previousDefaultCulture;
        }
    }
}
