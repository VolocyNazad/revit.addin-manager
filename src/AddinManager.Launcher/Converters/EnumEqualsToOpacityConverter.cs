using System.Globalization;
using System.Windows.Data;

namespace AddinManager.Launcher.Converters;

/// <summary>
/// Возвращает 1.0, если value равно parameter (через <see cref="object.Equals(object, object)"/>),
/// иначе 0.45. Используется для подсветки активной кнопки-переключателя режима через Opacity,
/// без ручного перебора кнопок в code-behind.
/// </summary>
public sealed class EnumEqualsToOpacityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Equals(value, parameter) ? OpacityConversion.Active : OpacityConversion.Inactive;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
