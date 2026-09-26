using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using AddinManager.Launcher.Composition;
using AddinManager.Launcher.ViewModels;
using AddinManager.Theming;
using AddinManager.Theming.Abstractions;
using AddinManager.Theming.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace AddinManager.Launcher;

/// <summary>Главное окно: хром и зоны. Состояние раскладки — в <see cref="MainViewModel"/>.</summary>
public partial class MainWindow
{
    private readonly MainViewModel _viewModel;
    private readonly IThemeService _themeService;

    /// <summary>
    /// Создает окно (только для окна разметки: без модели, зоны пустые). Рантайм всегда идёт
    /// через конструктор с моделью ниже — какой брать, контейнеру подсказывает
    /// <c>ActivatorUtilitiesConstructor</c>, наличие конструктора без параметров его не сбивает.
    /// </summary>
    public MainWindow()
    {
        _viewModel = null!;
        _themeService = null!;
        InitializeComponent();
    }

    /// <summary>Создает окно. Дочерние виды (шапка, список, редактор) подставляются в XAML через <see cref="ViewModelLocator"/>.</summary>
    /// <param name="viewModel">Модель раскладки.</param>
    /// <param name="themeService">Сервис темы.</param>
    [ActivatorUtilitiesConstructor]
    public MainWindow(
        MainViewModel viewModel,
        IThemeService themeService)
    {
        _viewModel = viewModel;
        _themeService = themeService;
        InitializeComponent();
        DataContext = _viewModel;
        ApplyTheme();
        SyncLayout(_viewModel);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Activated += OnActivated;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        Activated -= OnActivated;
        Loaded -= OnLoaded;
    }

    /// <summary>Механика вида: первая загрузка окна — тихая проверка обновлений (см. MainViewModel).</summary>
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        _ = _viewModel.CheckForUpdatesOnStartupAsync();
    }

    /// <summary>Механика вида: возврат фокуса — внеочередной опрос сторожа Revit (план, раздел 5).</summary>
    private void OnActivated(object? sender, EventArgs e) => _viewModel.RefreshRevitGuard();

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle)
        {
            ApplyTheme();
        }
    }

    /// <summary>
    /// Подставляет словарь кистей нужной темы (см. AddinManager.Theming.Wpf/Themes). Все цвета —
    /// в самих словарях; здесь только решение, какой из них сейчас активен.
    /// </summary>
    private void ApplyTheme() => ThemeDictionaries.Apply(_themeService.IsDark);

    private void OnMinimize(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void OnMaxRestore(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.AppTheme) or null)
            ApplyTheme();
        if (e.PropertyName is nameof(MainViewModel.MainLayout) or null)
            SyncLayout(_viewModel);
    }

    private void SyncLayout(MainViewModel viewModel)
    {
        var list = viewModel.MainLayout is MainLayout.List or MainLayout.Split;
        var editor = viewModel.MainLayout is MainLayout.Split or MainLayout.Editor;
        ApplyLayout(list, editor);
        ShowListButton.Opacity = viewModel.MainLayout == MainLayout.List ? 1 : 0.45;
        ShowSplitButton.Opacity = viewModel.MainLayout == MainLayout.Split ? 1 : 0.45;
        ShowEditorButton.Opacity = viewModel.MainLayout == MainLayout.Editor ? 1 : 0.45;
    }

    private void ApplyLayout(bool list, bool editor)
    {
        ListColumn.Width = list ? new GridLength(_viewModel.ListFraction, GridUnitType.Star) : new GridLength(0);
        EditorColumn.Width = editor ? new GridLength(1 - _viewModel.ListFraction, GridUnitType.Star) : new GridLength(0);
        var splitVisible = list && editor;
        SplitterColumn.Width = splitVisible ? GridLength.Auto : new GridLength(0);
        PaneSplitter.Visibility = splitVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPaneSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        var total = ListColumn.ActualWidth + EditorColumn.ActualWidth;
        if (total > 0)
            _viewModel.ListFraction = ListColumn.ActualWidth / total;
    }

    private void OnClose(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    // Перетаскивание за TitleBar обрабатывает сам WindowChrome (зона не помечена
    // IsHitTestVisibleInChrome) — обработчики мыши не нужны. Если чего-то не хватит —
    // возвращать MouseLeftButtonDown/Up на TitleBar.

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}
