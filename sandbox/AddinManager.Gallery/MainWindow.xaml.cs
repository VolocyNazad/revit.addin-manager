using System.Windows;

namespace AddinManager.Gallery;

/// <summary>Демо-окно галерки: переключение темы и показ тоста.</summary>
public partial class MainWindow
{
    /// <summary>Создает окно.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnShowToast(object sender, RoutedEventArgs e) => Toast.Visibility = Visibility.Visible;

    private void OnHideToast(object sender, RoutedEventArgs e) => Toast.Visibility = Visibility.Collapsed;

    private void OnThemeSystem(object sender, RoutedEventArgs e) => SetTheme("Fluent.xaml");

    private void OnThemeLight(object sender, RoutedEventArgs e) => SetTheme("Fluent.Light.xaml");

    private void OnThemeDark(object sender, RoutedEventArgs e) => SetTheme("Fluent.Dark.xaml");

    private static void SetTheme(string file)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries.Clear();
        dictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/PresentationFramework.Fluent;component/Themes/{file}"),
        });
    }
}
