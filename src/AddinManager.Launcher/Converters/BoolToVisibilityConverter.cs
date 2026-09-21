using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AddinManager.Launcher.Converters;

/// <summary>
/// <see cref="Visibility.Visible"/> when the bound <see cref="bool"/> is <see langword="true"/>,
/// otherwise <see cref="Visibility.Collapsed"/>; pass <c>ConverterParameter="Invert"</c> to flip
/// both outcomes — the built-in <see cref="System.Windows.Controls.BooleanToVisibilityConverter"/>
/// has no such parameter, so it can't cover the "hide when supported" half of a pair like
/// <see cref="ViewModels.ManifestSettingsViewModel.IsSupported"/> without a second,
/// separately-computed property.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        var invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        return flag != invert ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
