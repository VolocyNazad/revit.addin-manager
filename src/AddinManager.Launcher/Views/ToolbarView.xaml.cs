namespace AddinManager.Launcher.Views;

/// <summary>
/// Вид зоны шапки. Текст-снапшот показывается через <c>Text="{Binding}"</c> в XAML —
/// пустой путь биндится на сам DataContext и обновляется по <see cref="object.ToString"/>,
/// когда ViewModel поднимает PropertyChanged(null) (см. ToolbarViewModel.RefreshSnapshot).
/// </summary>
public partial class ToolbarView
{
    /// <summary>Создает вид.</summary>
    public ToolbarView() => InitializeComponent();
}
