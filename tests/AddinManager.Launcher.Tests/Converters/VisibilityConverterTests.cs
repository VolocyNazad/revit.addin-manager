using System.Globalization;
using System.Windows;
using AddinManager.Launcher.Converters;

namespace AddinManager.Launcher.Tests.Converters;

public sealed class VisibilityConverterTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(true, null, Visibility.Visible)]
    [InlineData(false, null, Visibility.Collapsed)]
    [InlineData(true, "Invert", Visibility.Collapsed)]
    [InlineData(false, "Invert", Visibility.Visible)]
    [InlineData(true, "invert", Visibility.Collapsed)]
    public void BoolToVisibility_ConvertsFlag(bool value, string? parameter, Visibility expected)
    {
        object? actual = new BoolToVisibilityConverter().Convert(value, typeof(Visibility), parameter, Culture);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("value", null, Visibility.Visible)]
    [InlineData(null, null, Visibility.Collapsed)]
    [InlineData("value", "Invert", Visibility.Collapsed)]
    [InlineData(null, "Invert", Visibility.Visible)]
    public void NullToVisibility_ConvertsPresence(object? value, string? parameter, Visibility expected)
    {
        object? actual = new NullToVisibilityConverter().Convert(value, typeof(Visibility), parameter, Culture);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ConvertBack_IsNotSupported()
    {
        Assert.Throws<NotSupportedException>(
            () => new BoolToVisibilityConverter().ConvertBack(Visibility.Visible, typeof(bool), null, Culture));
        Assert.Throws<NotSupportedException>(
            () => new NullToVisibilityConverter().ConvertBack(Visibility.Visible, typeof(object), null, Culture));
    }

    [Theory]
    [InlineData("A", "A", 1.0)]
    [InlineData("A", "B", 0.45)]
    public void EnumEqualsToOpacity_ConvertsMatch(string value, string parameter, double expected)
    {
        object? actual = new EnumEqualsToOpacityConverter().Convert(value, typeof(double), parameter, Culture);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null, null, 1.0)]
    [InlineData(true, "True", 1.0)]
    [InlineData(true, "False", 0.45)]
    [InlineData(null, "True", 0.45)]
    public void NullableBoolStateToOpacity_ConvertsState(object? value, string? parameter, double expected)
    {
        object? actual = new NullableBoolStateToOpacityConverter().Convert(value, typeof(double), parameter, Culture);

        Assert.Equal(expected, actual);
    }
}
