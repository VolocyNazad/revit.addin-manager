using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VolocyNazad.Wpf.DependencyProperties;

namespace AddinManager.Launcher.Behaviors;

/// <summary>
/// Выделение строки правой кнопкой мыши как в Проводнике: клик по невыделенной строке сбрасывает
/// прежнее выделение и выделяет только её, клик по уже выделенной строке мультивыбор не трогает.
/// Так контекстное меню строки всегда открывается для ожидаемого набора строк.
/// </summary>
[GenerateAttachedDependencyProperty<bool>(
    "Enabled",
    XmlDocumentation = "Включает поведение на списке.",
    GetterXmlDocumentation = "<summary>Читает признак включения поведения.</summary>",
    SetterXmlDocumentation = "<summary>Задаёт признак включения поведения.</summary>")]
public static partial class RightClickSelectionBehavior
{
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
