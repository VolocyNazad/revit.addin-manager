using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;

namespace AddinManager.Launcher.Views.Highlighting;

/// <summary>
/// Мост между статичной раскраской токенов AvalonEdit и темой приложения: на каждый рендер
/// достаёт актуальную кисть по ключу ресурса из дерева <see cref="TextView"/>
/// (тот же механизм, что стоит за <c>DynamicResource</c>), так что переключение Light/Dark
/// (<c>AddinManager.Theming.Wpf.ThemeDictionaries.Apply</c>) сразу видно и в подсветке
/// синтаксиса — без отдельной подписки вида на смену темы. Ключа может не быть (первый рендер
/// до подключения словарей темы), а у контекста — вида (проход подсветки вне визуального
/// дерева): в обоих случаях серая заглушка вместо падения. См.
/// docs/architecture.md, раздел "Markup editor".
/// </summary>
internal sealed class ResourceHighlightingBrush(string resourceKey) : HighlightingBrush
{
    /// <inheritdoc />
    public override Brush GetBrush(ITextRunConstructionContext context) =>
        context?.TextView?.TryFindResource(resourceKey) as Brush ?? Brushes.Gray;
}
