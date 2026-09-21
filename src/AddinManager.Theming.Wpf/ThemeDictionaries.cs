using System.Windows;

namespace AddinManager.Theming.Wpf;

/// <summary>
/// Переключает набор кистей темы целиком: <c>Themes/Light.xaml</c> и <c>Themes/Dark.xaml</c>
/// задают одни и те же ключи (<c>AppBackgroundBrush</c>, <c>AppForegroundBrush</c>,
/// <c>EditorAccentBrush</c>, <c>CaptionHoverBrush</c>) с разными значениями. Потребители
/// используют <c>DynamicResource</c> и сами ничего не знают про светлую/тёмную тему.
/// </summary>
public static class ThemeDictionaries
{
    private static readonly Uri LightUri = new(
        "pack://application:,,,/AddinManager.Theming.Wpf;component/Themes/Light.xaml");

    private static readonly Uri DarkUri = new(
        "pack://application:,,,/AddinManager.Theming.Wpf;component/Themes/Dark.xaml");

    /// <summary>Подставляет словарь кистей нужной темы в ресурсы приложения.</summary>
    /// <param name="isDark">Темная ли тема сейчас (см. <c>IThemeService.IsDark</c> в <c>AddinManager.Theming</c>, другая сборка, отсюда без <c>cref</c>).</param>
    public static void Apply(bool isDark)
    {
        var app = Application.Current;
        var dictionaries = app.Resources.MergedDictionaries;
        var uri = isDark ? DarkUri : LightUri;

        for (var i = dictionaries.Count - 1; i >= 0; i--)
        {
            if (dictionaries[i].Source == LightUri || dictionaries[i].Source == DarkUri)
                dictionaries.RemoveAt(i);
        }

        dictionaries.Add(new ResourceDictionary { Source = uri });

        // AppForegroundBrush — алиас на текстовую Fluent-кисть. Алиас через StaticResource
        // внутри Light/Dark.xaml не сработал (в тёмной теме иконки оставались чёрными),
        // поэтому берём кисть напрямую через FindResource и кладём под свой ключ здесь.
        app.Resources["AppForegroundBrush"] = app.FindResource(isDark
            ? "TextFillColorLightPrimaryBrush"
            : "TextFillColorDarkPrimaryBrush");
    }
}
