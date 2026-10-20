using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AddinManager.Launcher.ViewModels;

namespace AddinManager.Launcher.Views;

/// <summary>
/// Вид зоны списка: поиск, кнопка обновления, фильтр по scope/версии, сортировка и группировка в тулбаре
/// (см. ListViewModel), ниже — строки .addin файлов: toggle, имя файла, строка метаданных и Vendor.
/// В code-behind остаётся только механика встроенного переименования: фокус в поле, Enter/Esc и
/// сохранение при потере фокуса (см. architecture.md, MVVM).
/// </summary>
public partial class ListView
{
    private readonly DispatcherPriority _renameFocusPriority = DispatcherPriority.Input;

    /// <summary>Создает вид.</summary>
    public ListView() => InitializeComponent();

    private void OnRenameBoxIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not TextBox box || !box.IsVisible)
            return;

        // Поле только что появилось вместо TextBlock; фокус ставим после раскладки.
        box.Dispatcher.BeginInvoke(_renameFocusPriority, new Action(() =>
        {
            if (!box.IsVisible)
                return;
            box.Focus();
            box.SelectAll();
        }));
    }

    private void OnRenameBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: AddinFileRowViewModel row })
            return;

        if (e.Key == Key.Enter)
        {
            row.CommitRename();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            row.CancelRename();
            e.Handled = true;
        }
    }

    private void OnRenameBoxLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: AddinFileRowViewModel row } && row.IsRenaming)
            row.CommitRename();
    }
}
