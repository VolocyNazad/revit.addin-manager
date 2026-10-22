using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AddinManager.Launcher.Converters;

/// <summary>
/// <see cref="Visibility.Collapsed"/> when the bound value is <see langword="null"/>, otherwise
/// <see cref="Visibility.Visible"/>; pass <c>ConverterParameter="Invert"</c> to flip both outcomes.
/// For non-string values — see <see cref="NullOrEmptyToVisibilityConverter"/> for strings.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        VisibilityConversion.FromFlag(value is not null, parameter);

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
