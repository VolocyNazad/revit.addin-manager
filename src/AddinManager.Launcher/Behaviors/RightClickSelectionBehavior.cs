using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AddinManager.Launcher.Behaviors;

/// <summary>
/// Выделение строки правой кнопкой мыши как в Проводнике: клик по невыделенной строке сбрасывает
/// прежнее выделение и выделяет только её, клик по уже выделенной строке мультивыбор не трогает.
/// Так контекстное меню строки всегда открывается для ожидаемого набора строк.
/// </summary>
public static class RightClickSelectionBehavior
{
    /// <summary>Включает поведение на списке.</summary>
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled",
        typeof(bool),
        typeof(RightClickSelectionBehavior),
        new PropertyMetadata(false, OnEnabledChanged));

    /// <summary>Читает признак включения поведения.</summary>
    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

    /// <summary>Задаёт признак включения поведения.</summary>
    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ListBox listBox)
            return;

        if ((bool)e.NewValue)
            listBox.PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;
        else
            listBox.PreviewMouseRightButtonDown -= OnPreviewMouseRightButtonDown;
    }

    private static void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox)
            return;

        if (ItemsControl.ContainerFromElement(listBox, e.OriginalSource as DependencyObject) is not ListBoxItem item)
            return;

        if (item.IsSelected)
            return;

        listBox.SelectedItems.Clear();
        item.IsSelected = true;
        item.Focus();
    }
}
