using System.Globalization;

namespace AddinManager.Testing;

/// <summary>
/// Явно выставляет <see cref="CultureInfo.CurrentUICulture"/> на время теста и возвращает обратно:
/// утверждаемый локализованный текст не должен зависеть от языка ОС машины, где гоняют тесты.
/// Общий хелпер тестовых проектов (linked source), а не копия в каждом.
/// </summary>
public static class TestCulture
{
    /// <summary>Выполняет <paramref name="action"/> под культурой <paramref name="name"/>.</summary>
    public static void RunIn(string name, Action action)
    {
        var previousUiCulture = CultureInfo.CurrentUICulture;
        var previousDefaultCulture = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(name);
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
            CultureInfo.DefaultThreadCurrentUICulture = previousDefaultCulture;
        }
    }
}
