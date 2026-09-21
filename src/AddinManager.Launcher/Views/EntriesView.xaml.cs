namespace AddinManager.Launcher.Views;

/// <summary>
/// Вид подпанели записей: список записей выбранного файла, целиком декларативно (см.
/// EntriesView.xaml) — DataContext подставляется конвенцией (<c>AddModule&lt;TView&gt;()</c>),
/// код-бихайнд здесь не нужен.
/// </summary>
public partial class EntriesView
{
    /// <summary>Создает вид.</summary>
    public EntriesView() => InitializeComponent();
}
