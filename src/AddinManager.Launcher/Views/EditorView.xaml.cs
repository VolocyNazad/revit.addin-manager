using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using AddinManager.Launcher.Composition;
using AddinManager.Launcher.Converters;
using AddinManager.Launcher.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AddinManager.Launcher.Views;

/// <summary>
/// Вид зоны редактора: режимы и подпанели. Все четыре подпанели (Entries/Form/Markup/Settings)
/// подставляются в XAML через <see cref="ViewModelLocator"/>; видимость Form/Markup/Settings —
/// биндингом на Mode через <see cref="EditorModeToVisibilityConverter"/>. Подложка и текст —
/// DynamicResource на EditorAccentBrush/AppForegroundBrush из словаря темы (см. EditorView.xaml
/// и AddinManager.Theming.Wpf), без code-behind.
/// В code-behind остаётся только пересчёт ширин колонок под текущий режим.
/// </summary>
public partial class EditorView
{
    private readonly EditorViewModel _viewModel;

    /// <summary>
    /// Создает вид (только для окна разметки: без модели, панели пустые). Рантайм всегда идёт
    /// через конструктор с моделью ниже — какой брать, контейнеру подсказывает
    /// <c>ActivatorUtilitiesConstructor</c>, наличие конструктора без параметров его не сбивает.
    /// </summary>
    public EditorView()
    {
        _viewModel = null!;
        InitializeComponent();
    }

    /// <summary>Создает вид.</summary>
    /// <param name="viewModel">Модель зоны.</param>
    [ActivatorUtilitiesConstructor]
    public EditorView(EditorViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        _viewModel.PropertyChanged += OnVmChanged;
        Unloaded += OnUnloaded;
        ShowEditorPanels(_viewModel);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.PropertyChanged -= OnVmChanged;
        Unloaded -= OnUnloaded;
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e) => ShowEditorPanels(_viewModel);

    private void ShowEditorPanels(EditorViewModel editor)
    {
        var showEntries = editor.Mode is EditorMode.Entries or EditorMode.EntriesForm or EditorMode.EntriesMarkup;
        var showForm = editor.Mode is EditorMode.Form or EditorMode.EntriesForm;
        var showMarkup = editor.Mode is EditorMode.Markup or EditorMode.EntriesMarkup;
        var showSettings = editor.Mode is EditorMode.Settings;
        var showDetail = showForm || showMarkup || showSettings;
        GridLength entriesWidth = new(0);
        GridLength detailWidth = new(0);
        if (showEntries)
            entriesWidth = new GridLength(showDetail ? editor.EntriesFraction : 1, GridUnitType.Star);
        if (showDetail)
            detailWidth = new GridLength(showEntries ? 1 - editor.EntriesFraction : 2, GridUnitType.Star);
        EditorEntriesColumn.Width = entriesWidth;
        EditorDetailColumn.Width = detailWidth;
        // Видимость панелей отдельно не выставляем: колонка нулевой ширины и так не видна
        // и не занимает места, а Form/Markup внутри DetailPanel сами скрываются через
        // EditorModeToVisibilityConverter по Mode (см. EditorView.xaml).
        var split = showEntries && showDetail;
        EditorComboSplitterColumn.Width = split ? GridLength.Auto : new GridLength(0);
    }

    private void OnEditorSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        var total = EditorEntriesColumn.ActualWidth + EditorDetailColumn.ActualWidth;
        if (total > 0)
            _viewModel.EntriesFraction = EditorEntriesColumn.ActualWidth / total;
    }
}
