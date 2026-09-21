using System.Windows;
using Microsoft.Xaml.Behaviors;

namespace AddinManager.Launcher.Behaviors;

/// <summary>
/// Переносит результат модели в <see cref="Window.DialogResult"/>: напрямую забиндить
/// нельзя (обычное CLR-свойство), поэтому его выставляет поведение по своему dependency
/// property. Установка закрывает модальное окно — та же семантика, что у ручного переноса
/// из code-behind.
/// </summary>
public sealed class DialogResultBehavior : Behavior<Window>
{
    /// <summary>Результат из модели; установка закрывает окно.</summary>
    public bool? DialogResult
    {
        get => (bool?)GetValue(DialogResultProperty);
        set => SetValue(DialogResultProperty, value);
    }

    /// <summary>Dependency property для <see cref="DialogResult"/>.</summary>
    public static readonly DependencyProperty DialogResultProperty =
        DependencyProperty.Register(
            nameof(DialogResult),
            typeof(bool?),
            typeof(DialogResultBehavior),
            new PropertyMetadata(null, OnDialogResultChanged));

    private static void OnDialogResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DialogResultBehavior behavior
            && e.NewValue is bool result
            && behavior.AssociatedObject is not null)
            behavior.AssociatedObject.DialogResult = result;
    }
}
