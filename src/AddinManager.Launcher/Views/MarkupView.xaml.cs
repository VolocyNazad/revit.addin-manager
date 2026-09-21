using System.ComponentModel;
using System.Windows;
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
    private bool _suppressEditorSync;

    /// <summary>
    /// Создает вид (только для окна разметки: без модели, редактор пуст). Рантайм всегда идёт
    /// через конструктор с моделью ниже — какой брать, контейнеру подсказывает
    /// <c>ActivatorUtilitiesConstructor</c>, наличие конструктора без параметров его не сбивает.
    /// </summary>
    public MarkupView()
    {
        _viewModel = null!;
        InitializeComponent();
        SetupEditor();
    }

    /// <summary>Создает вид.</summary>
    /// <param name="viewModel">ViewModel подпанели.</param>
    [ActivatorUtilitiesConstructor]
    public MarkupView(MarkupViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
        SetupEditor();

        // Первую подсветку откладываем до Loaded: конструктор вида выполняется внутри
        // InitializeComponent главного окна, а словари темы подключаются только после него
        // (MainWindow.ApplyTheme) — ранний BringCaretToView форсирует рендер строки, чей
        // ResourceHighlightingBrush ещё не находит MarkupTagBrush, и падает.
        Loaded += OnLoaded;

        Editor.TextArea.TextView.BackgroundRenderers.Add(_diagnosticRenderer);
        Editor.TextChanged += OnEditorTextChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        SyncEditorFromViewModel();
        ApplySelectedEntrySpan();
        ApplyDiagnosticSpans();
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

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Editor.TextChanged -= OnEditorTextChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
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
}
