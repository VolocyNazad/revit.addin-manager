using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AddinManager.Launcher.ViewModels;

namespace AddinManager.Launcher.Converters;

/// <summary>
/// Возвращает <see cref="Visibility.Visible"/>, если <see cref="EditorMode"/> в value входит
/// в набор режимов, перечисленных через запятую в parameter (например "Form,EntriesForm"),
/// иначе <see cref="Visibility.Collapsed"/>.
/// </summary>
public sealed class EditorModeToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not EditorMode mode || parameter is not string modes)
            return Visibility.Collapsed;

        foreach (var name in modes.Split(','))
        {
            if (Enum.TryParse<EditorMode>(name.Trim(), out var candidate) && candidate == mode)
                return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
