using System.Windows;
using System.Windows.Controls;

namespace AddinManager.Launcher.Controls;

/// <summary>
/// Иконка из общего словаря <c>Resources/Icons.xaml</c>: вид задаётся <see cref="Kind"/>
/// (шаблон подтягивается по ключу <c>Icon.{Kind}</c>), цвет — через обычный
/// <c>Foreground</c> носителя. Неизвестному виду соответствует пустое место.
/// </summary>
public sealed class PackIcon : Control
{
    /// <summary>Какая иконка показана.</summary>
    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(
            nameof(Kind),
            typeof(PackIconKind),
            typeof(PackIcon),
            new PropertyMetadata(default(PackIconKind), OnKindChanged));

    /// <summary>Какая иконка показана.</summary>
    public PackIconKind Kind
    {
        get => (PackIconKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdateTemplate();
    }

    private static void OnKindChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is PackIcon icon)
            icon.UpdateTemplate();
    }

    private void UpdateTemplate()
    {
        if (GetTemplateChild("PART_Icon") is ContentControl host)
            host.Template = TryFindResource("Icon." + Kind) as ControlTemplate;
    }
}
