using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using AddinManager.Launcher.Abstractions;
using VolocyNazad.Wpf.DependencyProperties;

namespace AddinManager.Launcher.Behaviors;

/// <summary>
/// Обычный клик левой кнопкой по строке, без <c>Ctrl</c> и <c>Shift</c>, оставляет отмеченной
/// только эту строку, как в Проводнике Windows 11; клик по чекбоксу отметки не трогает.
/// </summary>
/// <remarks>
/// WPF снимает выбор только у тех строк, чьи контейнеры сейчас созданы. Если отмеченная строка
/// ушла из видимой области, её контейнер выгружен, и флаг в модели остаётся включённым — при
/// прокрутке назад строка снова выглядела бы отмеченной. Поэтому после клика флаги сверяются
/// с фактическим выбором списка: отметка остаётся только у выбранных строк.
/// </remarks>
[GenerateAttachedDependencyProperty<bool>(
    "Enabled",
    XmlDocumentation = "Включает поведение на списке.",
    GetterXmlDocumentation = "<summary>Читает признак включения поведения.</summary>",
    SetterXmlDocumentation = "<summary>Задаёт признак включения поведения.</summary>")]
public static partial class PlainClickSelectionBehavior
{
    private static readonly ConditionalWeakTable<ListBox, MousePress> Presses = new();

    private static readonly MouseButtonEventHandler OnMouseDownCompletedHandler = OnMouseDownCompleted;
    private static readonly MouseButtonEventHandler OnMouseUpCompletedHandler = OnMouseUpCompleted;

    private static void OnEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ListBox listBox)
            return;

        if ((bool)e.NewValue)
        {
            Presses.GetOrCreateValue(listBox);
            listBox.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            listBox.AddHandler(Mouse.MouseDownEvent, OnMouseDownCompletedHandler, handledEventsToo: true);
            listBox.AddHandler(Mouse.MouseUpEvent, OnMouseUpCompletedHandler, handledEventsToo: true);
        }
        else
        {
            listBox.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            listBox.RemoveHandler(Mouse.MouseDownEvent, OnMouseDownCompletedHandler);
            listBox.RemoveHandler(Mouse.MouseUpEvent, OnMouseUpCompletedHandler);
            Presses.Remove(listBox);
        }
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox)
            return;

        var source = e.OriginalSource as DependencyObject;
        var container = source is null ? null : ItemsControl.ContainerFromElement(listBox, source) as ListBoxItem;
        var press = Presses.GetOrCreateValue(listBox);
        press.IsPlainRowClick = ReplacesSelection(Keyboard.Modifiers, source, container);
    }

    /// <summary>
    /// Сверяет флаги строк с выбором списка: снимает отметку у строк, которые уже не выбраны.
    /// Новых отметок не добавляет — выбрать строку может только сам список.
    /// </summary>
    /// <param name="listBox">Список, в котором прошёл обычный клик.</param>
    internal static void Reconcile(ListBox listBox)
    {
        if (listBox.SelectedItems.Count == 0)
            return;

        var selected = new HashSet<object>(listBox.SelectedItems.Cast<object>());
        foreach (var item in listBox.Items)
        {
            if (item is IBulkSelectableRow row && row.IsSelected && !selected.Contains(item))
                row.IsSelected = false;
        }
    }

    /// <summary>Решает, должен ли клик оставить отмеченной только одну строку.</summary>
    /// <param name="modifiers">Клавиши-модификаторы, зажатые в момент нажатия.</param>
    /// <param name="source">Элемент под указателем.</param>
    /// <param name="container">Строка списка под указателем; <see langword="null"/>, если клик пришёлся мимо строк.</param>
    internal static bool ReplacesSelection(ModifierKeys modifiers, DependencyObject? source, DependencyObject? container)
    {
        if ((modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0)
            return false;

        if (container is null)
            return false;

        return !IsInteractiveContent(source, container);
    }

    /// <summary>
    /// Клик по чекбоксу, переключателю или полю переименования не должен менять выбор списка:
    /// WPF помечает такое нажатие обработанным, и поведение повторяет это правило.
    /// </summary>
    /// <param name="source">Элемент под указателем.</param>
    /// <param name="container">Строка списка, в которой ищутся интерактивные элементы.</param>
    internal static bool IsInteractiveContent(DependencyObject? source, DependencyObject container)
    {
        for (var current = source; current is not null && !ReferenceEquals(current, container); current = ParentOf(current))
        {
            if (current is ButtonBase or TextBoxBase)
                return true;
        }

        return false;
    }

    private static void OnMouseDownCompleted(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        if (sender is ListBox listBox && IsReplacingPress(listBox))
            Reconcile(listBox);
    }

    private static void OnMouseUpCompleted(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        if (sender is not ListBox listBox || !Presses.TryGetValue(listBox, out var press))
            return;

        // WPF схлопывает выбор по уже отмеченной строке только на отпускании кнопки,
        // поэтому вторую сверку делает тот же жест, что и нажатие.
        if (press.IsPlainRowClick)
            Reconcile(listBox);

        press.IsPlainRowClick = false;
    }

    private static bool IsReplacingPress(ListBox listBox) =>
        Presses.TryGetValue(listBox, out var press) && press.IsPlainRowClick;

    private static DependencyObject? ParentOf(DependencyObject child)
    {
        var visualParent = child is Visual ? VisualTreeHelper.GetParent(child) : null;
        return visualParent ?? LogicalTreeHelper.GetParent(child);
    }

    /// <summary>Нажатие, которое началось обычным кликом по строке, а не по чекбоксу.</summary>
    private sealed class MousePress
    {
        internal bool IsPlainRowClick { get; set; }
    }
}
