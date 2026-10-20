using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AddinManager.Launcher.ViewModels;
using AddinManager.Launcher.Views.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.Extensions.DependencyInjection;

namespace AddinManager.Launcher.Views;

/// <summary>
/// Вид подпанели разметки: AvalonEdit-редактор над <see cref="MarkupViewModel.RawXml"/>.
/// Текст синхронизируется вручную, а не через <c>Text="{Binding}"</c> на <c>TextEditor</c> —
/// это позволяет отличить правку пользователя от программной подстановки (смена выбранного
/// файла, Discard) и не перетирать текст/каретку эхом при каждом собственном вводе.
/// Механика строки поиска (фокус, выделение совпадения, подсветка) тоже живёт здесь: у
/// <c>TextEditor</c> нет dependency property для текста и выделения, а состояние поиска — в
/// <see cref="MarkupSearchViewModel"/>.
/// </summary>
public partial class MarkupView
{
    /// <summary>
    /// Встроенные именованные цвета AvalonEdit-подсветки XML (см. XML-Mode.xshd в самой
    /// библиотеке) и ключи ресурсов темы, которыми мы их заменяем — грамматику/токенизацию
    /// оставляем библиотечной, меняем только палитру, чтобы она не выбивалась стилистически из
    /// остального приложения и реагировала на смену темы. См. docs/architecture.md, "Markup editor".
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> SyntaxColorResourceKeys = new Dictionary<string, string>
    {
        ["XmlTag"] = "MarkupTagBrush",
        ["AttributeName"] = "MarkupAttributeNameBrush",
        ["AttributeValue"] = "MarkupAttributeValueBrush",
        ["Comment"] = "MarkupMetaBrush",
        ["CData"] = "MarkupMetaBrush",
        ["DocType"] = "MarkupMetaBrush",
        ["XmlDeclaration"] = "MarkupMetaBrush",
        ["Entity"] = "MarkupMetaBrush",
        ["BrokenEntity"] = "MarkupMetaBrush",
    };

    private readonly MarkupViewModel _viewModel;
    private readonly DiagnosticBackgroundRenderer _diagnosticRenderer = new();
    private readonly SearchMatchTransformer _searchTransformer = new();
    private readonly ICommand _openSearchCommand;
    private readonly ICommand _nextSearchMatchCommand;
    private readonly ICommand _previousSearchMatchCommand;
    private MarkupSearchViewModel? _search;
    private bool _suppressEditorSync;

    /// <summary>
    /// Создает вид (только для окна разметки: без модели, редактор пуст). Рантайм всегда идёт
    /// через конструктор с моделью ниже — какой брать, контейнеру подсказывает
    /// <c>ActivatorUtilitiesConstructor</c>, наличие конструктора без параметров его не сбивает.
    /// </summary>
    public MarkupView()
    {
        _viewModel = null!;
        _openSearchCommand = _nextSearchMatchCommand = _previousSearchMatchCommand = new ViewCommand(() => { });
        InitializeComponent();
        SetupEditor();
    }

    /// <summary>Создает вид.</summary>
    /// <param name="viewModel">ViewModel подпанели.</param>
    [ActivatorUtilitiesConstructor]
    public MarkupView(MarkupViewModel viewModel)
    {
        _viewModel = viewModel;
        _search = viewModel.Search;
        _openSearchCommand = new ViewCommand(OpenSearch);
        _nextSearchMatchCommand = new ViewCommand(GoToNextMatch);
        _previousSearchMatchCommand = new ViewCommand(GoToPreviousMatch);
        InitializeComponent();
        DataContext = _viewModel;
        SetupEditor();

        // Первую подсветку откладываем до Loaded: конструктор вида выполняется внутри
        // InitializeComponent главного окна, а словари темы подключаются только после него
        // (MainWindow.ApplyTheme) — ранний BringCaretToView форсирует рендер строки, чей
        // ResourceHighlightingBrush ещё не находит MarkupTagBrush, и падает.
        Loaded += OnLoaded;

        Editor.TextArea.TextView.BackgroundRenderers.Add(_diagnosticRenderer);
        Editor.TextArea.TextView.LineTransformers.Add(_searchTransformer);
        Editor.TextChanged += OnEditorTextChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _search.PropertyChanged += OnSearchPropertyChanged;
        RegisterSearchInputBindings();
        IsVisibleChanged += OnIsVisibleChanged;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// При входе в режим разметки фокус переходит в редактор: иначе после переключения режима
    /// кнопкой он остаётся на тулбаре, и локальные хоткеи поиска (<c>Ctrl+F</c>,
    /// <c>F3</c>) не срабатывают — они живут на этом виде.
    /// </summary>
    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!IsVisible || Editor.TextArea.IsKeyboardFocusWithin)
            return;

        // Фокус ставится после раскладки: в момент смены видимости элемент ещё не готов принять его.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (IsVisible && !Editor.TextArea.IsKeyboardFocusWithin)
                Editor.Focus();
        }));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        SyncEditorFromViewModel();
        ApplySelectedEntrySpan();
        ApplyDiagnosticSpans();
        UpdateSearchHighlighting();
        ApplyCurrentSearchMatch();
    }

    /// <summary>
    /// Добавляет хоткеи поиска. Они живут на самом виде, а не на редакторе, чтобы срабатывать и
    /// тогда, когда фокус в поле поиска; локальная привязка важнее оконной с тем же жестом.
    /// </summary>
    private void RegisterSearchInputBindings()
    {
        // Заменяются только привязки самого вида: объявленный в XAML Escape остаётся.
        InputBindings.Add(new KeyBinding(_openSearchCommand, Key.F, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(_nextSearchMatchCommand, Key.F3, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(_previousSearchMatchCommand, Key.F3, ModifierKeys.Shift));
    }

    private void OpenSearch()
    {
        if (_search is not { } search)
            return;

        // Ctrl+F переносит текущее выделение в запрос, как встроенные строки поиска.
        if (Editor.SelectedText is { Length: > 0 } selected)
            search.Query = selected;

        ApplyCurrentSearchMatch(force: true);
        FocusSearchQuery();
    }

    /// <summary>
    /// Переводит фокус в поле поиска и выделяет его содержимое. Запрос ставится в очередь с
    /// приоритетом ввода, чтобы поле уже было разложено и могло принять фокус.
    /// </summary>
    private void FocusSearchQuery() =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            SearchQueryBox.Focus();
            SearchQueryBox.SelectAll();
        }));

    private void GoToNextMatch()
    {
        if (_search is not { } search)
            return;

        search.NextCommand.Execute(null);
        ApplyCurrentSearchMatch(force: true);
    }

    private void GoToPreviousMatch()
    {
        if (_search is not { } search)
            return;

        search.PreviousCommand.Execute(null);
        ApplyCurrentSearchMatch(force: true);
    }

    /// <summary>
    /// Escape в поле поиска очищает поиск и возвращает клавиатуру в текст, как поиск в списке
    /// файлов. Сам редактор оставляет Escape панельному хоткею отмены правок.
    /// </summary>
    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;
        if (_search is not { } search)
            return;

        search.Clear();
        Editor.Focus();
        e.Handled = true;
    }

    private void OnSearchPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(MarkupSearchViewModel.Matches)
            or nameof(MarkupSearchViewModel.CurrentMatch)
            or null))
            return;

        UpdateSearchHighlighting();
        ApplyCurrentSearchMatch();
    }

    private void UpdateSearchHighlighting()
    {
        _searchTransformer.Matches = _search?.Matches ?? [];
        _searchTransformer.CurrentMatch = _search?.CurrentMatch;
        Editor.TextArea.TextView.Redraw();
    }

    /// <summary>Выделяет текущее совпадение и прокручивает к нему, не меняя документ.</summary>
    /// <param name="force">
    /// Когда <see langword="true"/>, выделение ставится даже при фокусе в редакторе: навигация
    /// должна показать своё совпадение. Иначе правка в редакторе сохраняет каретку — пересчёт
    /// совпадений из-за набора не должен уводить её посреди нажатия.
    /// </param>
    private void ApplyCurrentSearchMatch(bool force = false)
    {
        if (!force && Editor.TextArea.IsKeyboardFocusWithin)
            return;
        if (_search is not { CurrentMatch: { } match })
            return;
        if (match.Length <= 0)
            return;
        if (match.Offset < 0 || match.Offset + match.Length > Editor.Document.TextLength)
            return;

        Editor.Select(match.Offset, match.Length);
        Editor.TextArea.Caret.Offset = match.Offset;
        Editor.TextArea.Caret.BringCaretToView();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Editor.TextChanged -= OnEditorTextChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _search?.PropertyChanged -= OnSearchPropertyChanged;
        IsVisibleChanged -= OnIsVisibleChanged;
    }

    /// <summary>Настройка редактора без модели (ссылки, палитра подсветки).</summary>
    private void SetupEditor()
    {

        // AvalonEdit's built-in link recognition underlines paths/URLs in its own fixed color
        // (not theme-aware, not one of the tokens ApplyThemeAwareColors below covers) and turns
        // them clickable via Process.Start — neither is wanted here: a raw manifest path (e.g.
        // <Assembly>C:\...) isn't meant to be "opened", and it visually clashed with the muted
        // palette below. Off entirely, rather than reworked to look right.
        Editor.Options.EnableHyperlinks = false;
        Editor.Options.EnableEmailHyperlinks = false;

        var xmlHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(".xml");
        ApplyThemeAwareColors(xmlHighlighting);
        Editor.SyntaxHighlighting = xmlHighlighting;
    }

    /// <summary>
    /// Подменяет <see cref="HighlightingColor.Foreground"/> у
    /// именованных цветов встроенного определения на <see cref="ResourceHighlightingBrush"/> —
    /// одно и то же (кэшированное в <see cref="HighlightingManager"/>) определение используется на
    /// все запуски вида, поэтому подмена идемпотентна и делается один раз в конструкторе.
    /// </summary>
    private static void ApplyThemeAwareColors(IHighlightingDefinition definition)
    {
        foreach (var (colorName, resourceKey) in SyntaxColorResourceKeys)
        {
            var color = definition.GetNamedColor(colorName);
            if (color is not null)
                color.Foreground = new ResourceHighlightingBrush(resourceKey);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Срез — только по именованному уведомлению: широковещательный null (тот же
        // RefreshSnapshot, что дёргается на каждое нажатие через IsDirty) применять
        // подсветку не должен, иначе каретка улетала бы в начало блока при вводе.
        if (e.PropertyName is nameof(MarkupViewModel.RawXml) or null)
            SyncEditorFromViewModel();
        if (e.PropertyName is nameof(MarkupViewModel.SelectedEntrySpan))
            ApplySelectedEntrySpan();
        if (e.PropertyName is nameof(MarkupViewModel.DiagnosticSpans))
            ApplyDiagnosticSpans();
    }

    /// <summary>
    /// Механика вида: перерисовывает маркеры диагностических срезов. Выделение и каретку
    /// не трогает — в отличие от селекции блока, сюда можно заходить на каждое нажатие.
    /// </summary>
    private void ApplyDiagnosticSpans()
    {
        _diagnosticRenderer.SetSpans(_viewModel.DiagnosticSpans);
        Editor.TextArea.TextView.Redraw();
    }

    private void SyncEditorFromViewModel()
    {
        var text = _viewModel.RawXml ?? string.Empty;
        if (Editor.Text == text)
            return;

        _suppressEditorSync = true;
        Editor.Text = text;
        _suppressEditorSync = false;

        // Замена текста сносит выделение — накатываем текущий срез заново.
        ApplySelectedEntrySpan();
    }

    /// <summary>
    /// Механика вида: выделяет блок выбранной записи и прокручивает к нему. Пустой срез
    /// ничего не трогает (не сбрасываем чужое выделение в начало) — он означает лишь, что
    /// блока нет в текущем тексте.
    /// </summary>
    private void ApplySelectedEntrySpan()
    {
        if (_viewModel.SelectedEntrySpan is not { } span)
            return;
        if (span.Start < 0 || span.Start + span.Length > Editor.Document.TextLength)
            return;

        Editor.Select(span.Start, span.Length);
        Editor.TextArea.Caret.Offset = span.Start;
        Editor.TextArea.Caret.BringCaretToView();
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_suppressEditorSync)
            return;

        _viewModel.RawXml = Editor.Text;
    }

    /// <summary>Адаптер параметрического действия вида к <see cref="ICommand"/> для привязок клавиш.</summary>
    private sealed class ViewCommand(Action execute) : ICommand
    {
        /// <remarks>Доступность действия постоянна, поэтому подписка просто переиспользует WM-опрос WPF.</remarks>
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
