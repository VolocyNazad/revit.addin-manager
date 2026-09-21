using System.Windows;
using Microsoft.Win32;

namespace AddinManager.Gallery.Materials;

/// <summary>Демо-окно галерки материалов: темы и тост.</summary>
public partial class MainWindow
{
    /// <summary>Создает окно.</summary>
    public MainWindow()
    {
        InitializeComponent();
        WindowMaterial.IsDarkMode = !IsSystemLightTheme();
    }

    private void OnShowToast(object sender, RoutedEventArgs e) => Toast.Visibility = Visibility.Visible;

    private void OnHideToast(object sender, RoutedEventArgs e) => Toast.Visibility = Visibility.Collapsed;

    private void OnThemeSystem(object sender, RoutedEventArgs e)
    {
        SetTheme("Fluent.xaml");
        WindowMaterial.IsDarkMode = !IsSystemLightTheme();
    }

    private void OnThemeLight(object sender, RoutedEventArgs e)
    {
        SetTheme("Fluent.Light.xaml");
        WindowMaterial.IsDarkMode = false;
    }

    private void OnThemeDark(object sender, RoutedEventArgs e)
    {
        SetTheme("Fluent.Dark.xaml");
        WindowMaterial.IsDarkMode = true;
    }

    private static bool IsSystemLightTheme() =>
        Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme",
            1) is not 0;

    private static void SetTheme(string file)
    {
        const string fluentBase = "pack://application:,,,/PresentationFramework.Fluent;component/Themes/";
        const string coreTheme = "Generic.xaml";
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries.Clear();
        dictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"{fluentBase}{file}"),
        });
        dictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/FluentWpfCore;component/Themes/{coreTheme}"),
        });
    }
}
