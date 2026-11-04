using System.Windows;
using Microsoft.Xaml.Behaviors;
using VolocyNazad.Wpf.DependencyProperties;

namespace AddinManager.Launcher.Behaviors;

/// <summary>
/// Переносит результат модели в <see cref="Window.DialogResult"/>: напрямую забиндить
/// нельзя (обычное CLR-свойство), поэтому его выставляет поведение по своему dependency
/// property. Установка закрывает модальное окно — та же семантика, что у ручного переноса
/// из code-behind.
/// </summary>
[GenerateDependencyProperty<bool?>(
    "DialogResult",
    XmlDocumentation = "Dependency property для <see cref=\"DialogResult\"/>.",
    PropertyXmlDocumentation = "Результат из модели; установка закрывает окно.")]
public sealed partial class DialogResultBehavior : Behavior<Window>
{
    private static void OnDialogResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DialogResultBehavior behavior
            && e.NewValue is bool result
            && behavior.AssociatedObject is not null)
            behavior.AssociatedObject.DialogResult = result;
    }
}
