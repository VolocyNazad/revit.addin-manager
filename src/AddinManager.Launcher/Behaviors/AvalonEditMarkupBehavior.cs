using System.Windows;
using System.Windows.Threading;
using AddinManager.Core.Diagnostics;
using AddinManager.Launcher.Views.Highlighting;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using VolocyNazad.Wpf.DependencyProperties;

namespace AddinManager.Launcher.Behaviors;

/// <summary>
/// Механика AvalonEdit-редактора разметки: двусторонняя синхронизация <see cref="TextProperty"/>
/// с текстом редактора без эха программной подстановки, подсветка блока выбранной записи и
/// маркеров диагностики, тема-зависимая палитра подсветки и фокус при появлении режима.
/// Всё это — свойство редактора, а не состояние вида, поэтому вынесено из code-behind.
/// </summary>
[GenerateAttachedDependencyProperty<string>(
    "Text",
    IsNullable = true,
    Options = FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
    XmlDocumentation = "Текст редактора; изменение с любой стороны синхронизируется с моделью.",
    GetterXmlDocumentation = "<summary>Читает текст редактора.</summary>",
    SetterXmlDocumentation = "<summary>Задаёт текст редактора.</summary>")]
[GenerateAttachedDependencyProperty<MarkupSpan?>(
    "SelectedEntrySpan",
    XmlDocumentation = "Срез блока выбранной записи для выделения в редакторе.",
    GetterXmlDocumentation = "<summary>Читает срез блока выбранной записи.</summary>",
    SetterXmlDocumentation = "<summary>Задаёт срез блока выбранной записи.</summary>")]
[GenerateAttachedDependencyProperty<IReadOnlyList<MarkupSpan>>(
    "DiagnosticSpans",
    IsNullable = true,
    XmlDocumentation = "Срезы диагностики для волнистого подчёркивания.",
    GetterXmlDocumentation = "<summary>Читает срезы диагностики.</summary>",
    SetterXmlDocumentation = "<summary>Задаёт срезы диагностики.</summary>")]
public static partial class AvalonEditMarkupBehavior
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

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State",
        typeof(EditorState),
        typeof(AvalonEditMarkupBehavior),
        new PropertyMetadata(null));

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
