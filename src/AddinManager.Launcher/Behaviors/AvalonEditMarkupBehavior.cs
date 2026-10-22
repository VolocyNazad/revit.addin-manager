using System.Windows;
using System.Windows.Threading;
using AddinManager.Core.Diagnostics;
using AddinManager.Launcher.Views.Highlighting;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;

namespace AddinManager.Launcher.Behaviors;

/// <summary>
/// Механика AvalonEdit-редактора разметки: двусторонняя синхронизация <see cref="TextProperty"/>
/// с текстом редактора без эха программной подстановки, подсветка блока выбранной записи и
/// маркеров диагностики, тема-зависимая палитра подсветки и фокус при появлении режима.
/// Всё это — свойство редактора, а не состояние вида, поэтому вынесено из code-behind.
/// </summary>
public static class AvalonEditMarkupBehavior
{
    /// <summary>
    /// Встроенные именованные цвета AvalonEdit-подсветки XML (см. XML-Mode.xshd в самой
    /// библиотеке) и ключи ресурсов темы, которыми мы их заменяем — грамматику/токенизацию
    /// оставляем библиотечной, меняем только палитру, чтобы она не выбивалась стилистически из
    /// остального приложения и реагировала на смену темы.
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

    /// <summary>Текст редактора; изменение с любой стороны синхронизируется с моделью.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text",
        typeof(string),
        typeof(AvalonEditMarkupBehavior),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnTextChanged));

    /// <summary>Срез блока выбранной записи для выделения в редакторе.</summary>
    public static readonly DependencyProperty SelectedEntrySpanProperty = DependencyProperty.RegisterAttached(
        "SelectedEntrySpan",
        typeof(MarkupSpan?),
        typeof(AvalonEditMarkupBehavior),
        new PropertyMetadata(null, OnSelectedEntrySpanChanged));

    /// <summary>Срезы диагностики для волнистого подчёркивания.</summary>
    public static readonly DependencyProperty DiagnosticSpansProperty = DependencyProperty.RegisterAttached(
        "DiagnosticSpans",
        typeof(IReadOnlyList<MarkupSpan>),
        typeof(AvalonEditMarkupBehavior),
        new PropertyMetadata(null, OnDiagnosticSpansChanged));

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State",
        typeof(EditorState),
        typeof(AvalonEditMarkupBehavior),
        new PropertyMetadata(null));

    /// <summary>Читает текст редактора.</summary>
    public static string? GetText(DependencyObject element) => (string?)element.GetValue(TextProperty);

    /// <summary>Задаёт текст редактора.</summary>
    public static void SetText(DependencyObject element, string? value) => element.SetValue(TextProperty, value);

    /// <summary>Читает срез блока выбранной записи.</summary>
    public static MarkupSpan? GetSelectedEntrySpan(DependencyObject element) =>
        (MarkupSpan?)element.GetValue(SelectedEntrySpanProperty);

    /// <summary>Задаёт срез блока выбранной записи.</summary>
    public static void SetSelectedEntrySpan(DependencyObject element, MarkupSpan? value) =>
        element.SetValue(SelectedEntrySpanProperty, value);

    /// <summary>Читает срезы диагностики.</summary>
    public static IReadOnlyList<MarkupSpan>? GetDiagnosticSpans(DependencyObject element) =>
        (IReadOnlyList<MarkupSpan>?)element.GetValue(DiagnosticSpansProperty);

    /// <summary>Задаёт срезы диагностики.</summary>
    public static void SetDiagnosticSpans(DependencyObject element, IReadOnlyList<MarkupSpan>? value) =>
        element.SetValue(DiagnosticSpansProperty, value);

    private static void OnTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TextEditor editor)
            return;

        EditorState state = EnsureState(editor);
        if (state.SuppressEditorSync)
            return;

        var text = e.NewValue as string ?? string.Empty;
        if (editor.Text == text)
            return;

        state.SuppressEditorSync = true;
        try
        {
            editor.Text = text;
        }
        finally
        {
            state.SuppressEditorSync = false;
        }

        // Замена текста сносит выделение — накатываем текущий срез заново.
        ApplySelectedEntrySpan(editor);
    }

    private static void OnSelectedEntrySpanChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TextEditor editor)
            return;

        _ = EnsureState(editor);
        if (editor.IsLoaded)
            ApplySelectedEntrySpan(editor);
    }

    private static void OnDiagnosticSpansChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is TextEditor editor)
        {
            _ = EnsureState(editor);
            ApplyDiagnosticSpans(editor);
        }
    }

    private static EditorState EnsureState(TextEditor editor)
    {
        if (editor.GetValue(StateProperty) is EditorState existing)
            return existing;

        var state = new EditorState(editor);
        editor.SetValue(StateProperty, state);
        editor.TextArea.TextView.BackgroundRenderers.Add(state.DiagnosticRenderer);
        ConfigureEditor(editor);
        editor.TextChanged += state.OnEditorTextChanged;
        editor.Loaded += state.OnLoaded;
        editor.IsVisibleChanged += state.OnIsVisibleChanged;
        return state;
    }

    private static void ConfigureEditor(TextEditor editor)
    {
        // Встроенное распознавание ссылок AvalonEdit подчёркивает пути/URL фиксированным цветом
        // (не тема) и открывает их через Process.Start — для манифеста это не нужно.
        editor.Options.EnableHyperlinks = false;
        editor.Options.EnableEmailHyperlinks = false;

        var xmlHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(".xml");
        ApplyThemeAwareColors(xmlHighlighting);
        editor.SyntaxHighlighting = xmlHighlighting;
    }

    /// <summary>
    /// Подменяет <see cref="HighlightingColor.Foreground"/> у именованных цветов встроенного
    /// определения на <see cref="ResourceHighlightingBrush"/> — одно и то же (кэшированное в
    /// <see cref="HighlightingManager"/>) определение используется на все запуски вида, поэтому
    /// подмена идемпотентна.
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

    private static void ApplySelectedEntrySpan(TextEditor editor)
    {
        if (GetSelectedEntrySpan(editor) is not { } span)
            return;
        if (span.Start < 0 || span.Start + span.Length > editor.Document.TextLength)
            return;

        editor.Select(span.Start, span.Length);
        editor.TextArea.Caret.Offset = span.Start;
        editor.TextArea.Caret.BringCaretToView();
    }

    private static void ApplyDiagnosticSpans(TextEditor editor)
    {
        if (editor.GetValue(StateProperty) is not EditorState state)
            return;

        state.DiagnosticRenderer.SetSpans(GetDiagnosticSpans(editor) ?? []);
        editor.TextArea.TextView.Redraw();
    }

    /// <summary>Состояние одного редактора: рендерер маркеров и флаг программной подстановки текста.</summary>
    private sealed class EditorState(TextEditor editor)
    {
        internal DiagnosticBackgroundRenderer DiagnosticRenderer { get; } = new();

        internal bool SuppressEditorSync { get; set; }

        internal void OnEditorTextChanged(object? sender, EventArgs e)
        {
            if (SuppressEditorSync)
                return;

            editor.SetCurrentValue(TextProperty, editor.Text);
        }

        internal void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplySelectedEntrySpan(editor);
            ApplyDiagnosticSpans(editor);
        }

        /// <summary>
        /// При входе в режим разметки фокус переходит в редактор: иначе после переключения
        /// режима кнопкой он остаётся на тулбаре, и локальные хоткеи поиска не срабатывают.
        /// </summary>
        internal void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!editor.IsVisible || editor.TextArea.IsKeyboardFocusWithin)
                return;

            // Фокус ставится после раскладки: в момент смены видимости элемент ещё не готов принять его.
            editor.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (editor.IsVisible && !editor.TextArea.IsKeyboardFocusWithin)
                    editor.Focus();
            }));
        }
    }
}
