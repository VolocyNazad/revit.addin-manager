using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace AddinManager.Gallery.WpfUi;

/// <summary>Демо-окно WPF-UI галерки: тема и тост.</summary>
public partial class MainWindow : FluentWindow
{
    /// <summary>Создает окно.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnShowToast(object sender, RoutedEventArgs e) => Toast.IsOpen = true;

    private void OnThemeSystem(object sender, RoutedEventArgs e) => Apply(
        ApplicationThemeManager.GetSystemTheme() is SystemTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light);

    private void OnThemeLight(object sender, RoutedEventArgs e) => Apply(ApplicationTheme.Light);

    private void OnThemeDark(object sender, RoutedEventArgs e) => Apply(ApplicationTheme.Dark);

    private static void Apply(ApplicationTheme theme) =>
        ApplicationThemeManager.Apply(theme, WindowBackdropType.Mica, true);
}
