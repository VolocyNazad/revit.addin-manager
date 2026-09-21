using System.Globalization;
using System.Windows.Data;

namespace AddinManager.Launcher.Converters;

/// <summary>
/// Возвращает 1.0, если <c>bool?</c> value соответствует состоянию, закодированному строкой
/// parameter (<c>"True"</c>/<c>"False"</c>/что угодно ещё — трактуется как "не задано",
/// <see langword="null"/>), иначе 0.45. Тот же паттерн подсветки активной чипсы через Opacity,
/// что и <see cref="EnumEqualsToOpacityConverter"/>, но для <c>bool?</c> — простое
/// <see cref="object.Equals(object, object)"/> там не различает "не задано" от несовпадения типов.
/// </summary>
public sealed class NullableBoolStateToOpacityConverter : IValueConverter
{
    private const double ActiveOpacity = 1.0;
    private const double InactiveOpacity = 0.45;

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var current = value as bool?;
        var matches = (parameter as string) switch
        {
            "True" => current == true,
            "False" => current == false,
            _ => current is null,
        };

        return matches ? ActiveOpacity : InactiveOpacity;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
