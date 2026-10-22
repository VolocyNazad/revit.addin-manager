using System.Windows;

namespace AddinManager.Launcher.Converters;

/// <summary>Shared visibility rule: <c>ConverterParameter="Invert"</c> flips both outcomes.</summary>
internal static class VisibilityConversion
{
    internal static Visibility FromFlag(bool flag, object? parameter) =>
        flag != IsInverted(parameter) ? Visibility.Visible : Visibility.Collapsed;

    private static bool IsInverted(object? parameter) =>
        string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Shared opacity values for the active/inactive toggle look.</summary>
internal static class OpacityConversion
{
    internal const double Active = 1.0;
    internal const double Inactive = 0.45;
}
