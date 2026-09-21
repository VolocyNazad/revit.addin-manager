using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace AddinManager.Launcher.Behaviors;

/// <summary>
/// Перетаскивание безрамочного окна за элемент: вешается на карточку диалога вместо
/// копипасты <c>OnDragMove</c> по code-behind каждого окна.
/// </summary>
public sealed class WindowDragBehavior : Behavior<FrameworkElement>
{
    /// <inheritdoc />
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.MouseLeftButtonDown += OnMouseLeftButtonDown;
    }

    /// <inheritdoc />
    protected override void OnDetaching()
    {
        AssociatedObject.MouseLeftButtonDown -= OnMouseLeftButtonDown;
        base.OnDetaching();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            Window.GetWindow(AssociatedObject)?.DragMove();
    }
}
