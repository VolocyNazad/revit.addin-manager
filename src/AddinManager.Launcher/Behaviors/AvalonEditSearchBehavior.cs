using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AddinManager.Launcher.ViewModels;
using AddinManager.Launcher.Views.Highlighting;
using ICSharpCode.AvalonEdit;

namespace AddinManager.Launcher.Behaviors;

/// <summary>
/// Механика строки поиска Markup-редактора: подсветка совпадений, выделение текущего, хоткеи
/// <c>Ctrl+F</c>/<c>F3</c>/<c>Shift+F3</c>, перенос выделения в запрос и Escape в поле поиска.
/// Поведение висит на виде и находит редактор и поле поиска в его дереве, поэтому code-behind
/// вида остаётся только композицией.
/// </summary>
public static class AvalonEditSearchBehavior
{
    /// <summary>Состояние поиска панели.</summary>
    public static readonly DependencyProperty SearchProperty = DependencyProperty.RegisterAttached(
        "Search",
        typeof(MarkupSearchViewModel),
        typeof(AvalonEditSearchBehavior),
        new PropertyMetadata(null, OnSearchChanged));

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State",
        typeof(SearchState),
        typeof(AvalonEditSearchBehavior),
        new PropertyMetadata(null));

    /// <summary>Читает состояние поиска.</summary>
    public static MarkupSearchViewModel? GetSearch(DependencyObject element) =>
        (MarkupSearchViewModel?)element.GetValue(SearchProperty);

    /// <summary>Задаёт состояние поиска.</summary>
    public static void SetSearch(DependencyObject element, MarkupSearchViewModel? value) =>
        element.SetValue(SearchProperty, value);

    private static void OnSearchChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not FrameworkElement view)
            return;

        SearchState state = EnsureState(view);
        state.Attach((MarkupSearchViewModel?)e.NewValue);
    }

    private static SearchState EnsureState(FrameworkElement view)
    {
        if (view.GetValue(StateProperty) is SearchState existing)
            return existing;

        var state = new SearchState(view);
        view.SetValue(StateProperty, state);
        view.Loaded += state.OnViewLoaded;
        return state;
    }

    /// <summary>Состояние одного вида: найденные элементы, подписки и клавиши поиска.</summary>
    private sealed class SearchState
    {
        private readonly FrameworkElement _view;
        private readonly SearchMatchTransformer _transformer = new();
        private readonly ICommand _openSearchCommand;
        private readonly ICommand _nextMatchCommand;
        private readonly ICommand _previousMatchCommand;
        private TextEditor? _editor;
        private TextBox? _queryBox;
        private MarkupSearchViewModel? _search;
        private bool _initialized;

        internal SearchState(FrameworkElement view)
        {
            _view = view;
            _openSearchCommand = new ViewCommand(OpenSearch);
            _nextMatchCommand = new ViewCommand(GoToNextMatch);
            _previousMatchCommand = new ViewCommand(GoToPreviousMatch);
        }

        internal void Attach(MarkupSearchViewModel? search)
        {
            if (ReferenceEquals(_search, search))
                return;

            if (_search is not null)
                _search.PropertyChanged -= OnSearchPropertyChanged;

            _search = search;

            if (_search is not null)
                _search.PropertyChanged += OnSearchPropertyChanged;

            if (_initialized)
                ApplySearchState();
        }

        internal void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            if (_initialized)
                return;

            _editor = FindChild<TextEditor>(_view);
            _queryBox = _view.FindName("SearchQueryBox") as TextBox;
            if (_editor is null)
                return;

            _initialized = true;
            _editor.TextArea.TextView.LineTransformers.Add(_transformer);
            _view.InputBindings.Add(new KeyBinding(_openSearchCommand, Key.F, ModifierKeys.Control));
            _view.InputBindings.Add(new KeyBinding(_nextMatchCommand, Key.F3, ModifierKeys.None));
            _view.InputBindings.Add(new KeyBinding(_previousMatchCommand, Key.F3, ModifierKeys.Shift));
            if (_queryBox is not null)
                _queryBox.KeyDown += OnQueryKeyDown;

            ApplySearchState();
            ApplyCurrentMatch(force: true);
        }

        private void OnSearchPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is not (nameof(MarkupSearchViewModel.Matches)
                or nameof(MarkupSearchViewModel.CurrentMatch)
                or null))
            {
                return;
            }

            UpdateHighlighting();
            ApplyCurrentMatch(force: false);
        }

        private void ApplySearchState()
        {
            UpdateHighlighting();
            ApplyCurrentMatch(force: false);
        }

        private void UpdateHighlighting()
        {
            if (_editor is null)
                return;

            _transformer.Matches = _search?.Matches ?? [];
            _transformer.CurrentMatch = _search?.CurrentMatch;
            _editor.TextArea.TextView.Redraw();
        }

        /// <summary>Выделяет текущее совпадение и прокручивает к нему, не меняя документ.</summary>
        /// <param name="force">
        /// Когда <see langword="true"/>, выделение ставится даже при фокусе в редакторе: навигация
        /// должна показать своё совпадение. Иначе правка в редакторе сохраняет каретку.
        /// </param>
        private void ApplyCurrentMatch(bool force)
        {
            if (_editor is null)
                return;
            if (!force && _editor.TextArea.IsKeyboardFocusWithin)
                return;
            if (_search is not { CurrentMatch: { } match })
                return;
            if (match.Length <= 0)
                return;
            if (match.Offset < 0 || match.Offset + match.Length > _editor.Document.TextLength)
                return;

            _editor.Select(match.Offset, match.Length);
            _editor.TextArea.Caret.Offset = match.Offset;
            _editor.TextArea.Caret.BringCaretToView();
        }

        private void OnQueryKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || _search is not { } search || _editor is null)
                return;

            search.Clear();
            _editor.Focus();
            e.Handled = true;
        }

        private void OpenSearch()
        {
            if (_editor is null || _search is not { } search)
                return;

            // Ctrl+F переносит текущее выделение в запрос, как встроенные строки поиска.
            if (_editor.SelectedText is { Length: > 0 } selected)
                search.Query = selected;

            ApplyCurrentMatch(force: true);
            FocusQueryBox();
        }

        private void FocusQueryBox() =>
            _view.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (_queryBox is null)
                    return;

                _queryBox.Focus();
                _queryBox.SelectAll();
            }));

        private void GoToNextMatch()
        {
            if (_search is not { } search)
                return;

            search.NextCommand.Execute(null);
            ApplyCurrentMatch(force: true);
        }

        private void GoToPreviousMatch()
        {
            if (_search is not { } search)
                return;

            search.PreviousCommand.Execute(null);
            ApplyCurrentMatch(force: true);
        }

        private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            var children = VisualTreeHelper.GetChildrenCount(parent);
            for (var index = 0; index < children; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is T match)
                    return match;

                if (FindChild<T>(child) is { } nested)
                    return nested;
            }

            return null;
        }

        /// <summary>Адаптер действия вида к <see cref="ICommand"/> для привязок клавиш.</summary>
        private sealed class ViewCommand(Action execute) : ICommand
        {
            /// <remarks>Доступность действия постоянна, поэтому подписка переиспользует WM-опрос WPF.</remarks>
            public event EventHandler? CanExecuteChanged
            {
                add => CommandManager.RequerySuggested += value;
                remove => CommandManager.RequerySuggested -= value;
            }

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter) => execute();
        }
    }
}
