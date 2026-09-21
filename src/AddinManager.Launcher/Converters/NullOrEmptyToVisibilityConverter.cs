using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AddinManager.Launcher.Converters;

/// <summary>
/// <see cref="Visibility.Collapsed"/> для <see langword="null"/> или пустой/пробельной строки,
/// иначе <see cref="Visibility.Visible"/>. Используется, чтобы скрывать необязательные подписи
/// (например, подзаголовок Vendor), когда исходных данных нет.
/// </summary>
public sealed class NullOrEmptyToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string text && !string.IsNullOrWhiteSpace(text) ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
