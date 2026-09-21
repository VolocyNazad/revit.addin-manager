using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AddinManager.Launcher.Converters;

/// <summary>
/// <see cref="Visibility.Visible"/> для <see langword="null"/> или пустой/пробельной строки,
/// иначе <see cref="Visibility.Collapsed"/> — обратная логика к <see cref="NullOrEmptyToVisibilityConverter"/>.
/// Используется для placeholder-подсказки поверх пустого поля поиска.
/// </summary>
public sealed class EmptyToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not string text || string.IsNullOrWhiteSpace(text) ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
