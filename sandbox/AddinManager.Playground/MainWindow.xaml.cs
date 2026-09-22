using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;

namespace AddinManager.Playground;

/// <summary>Песочница разметки: вставка XAML-фрагмента и живой предпросмотр через XamlReader.</summary>
public partial class MainWindow
{
    private const string PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Создает окно.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnSourceKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            OnRender(sender, e);
            e.Handled = true;
        }
    }

    private void OnRender(object sender, RoutedEventArgs e)
    {
        var text = Source.Text.Trim();
        if (text.Length == 0)
        {
            ShowMessage("Вставьте разметку в левое поле.");
            return;
        }

        try
        {
            switch (XamlReader.Parse(WithNamespaces(text)))
            {
                case FrameworkElement element:
                    Preview.Content = element;
                    HideMessage();
                    break;
                case ResourceDictionary dictionary:
                    Preview.Resources.MergedDictionaries.Add(dictionary);
                    ShowMessage("Словарь подключён к предпросмотру. Вставьте разметку, чтобы увидеть результат.");
                    break;
                default:
                    ShowMessage("Корень должен быть элементом интерфейса или словарём ресурсов.");
                    break;
            }
        }
        catch (Exception ex)
        {
            ShowMessage(ex.GetBaseException().Message);
        }
    }

    private static string WithNamespaces(string text) =>
        text.Contains("xmlns", StringComparison.Ordinal)
            ? text
            : $"<Grid xmlns=\"{PresentationNamespace}\" xmlns:x=\"{XamlNamespace}\">{text}</Grid>";

    private void ShowMessage(string text)
    {
        MessageText.Text = text;
        MessageText.Visibility = Visibility.Visible;
    }

    private void HideMessage() => MessageText.Visibility = Visibility.Collapsed;
}
