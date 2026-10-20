using System.Windows.Media;
using AddinManager.Core.Search;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace AddinManager.Launcher.Views.Highlighting;

/// <summary>
/// Подсвечивает все совпадения поиска по разметке и, сильнее, совпадение, на котором сейчас стоит
/// навигация.
/// </summary>
/// <remarks>
/// Кисти — фиксированные полупрозрачные янтарные тона, а не ресурсы темы, по той же причине, что и
/// фон ошибочной строки редактора: текст рисуется поверх заливки, а один полупрозрачный тон читается
/// и на светлой, и на тёмной поверхности, не добавляя ещё один ключ в оба словаря темы. Текущее
/// совпадение — тот же цвет с большей непрозрачностью, поэтому воспринимается как выбранное
/// вхождение того же семейства.
/// </remarks>
internal sealed class SearchMatchTransformer : DocumentColorizingTransformer
{
    private static readonly SolidColorBrush MatchBackground = CreateBrush(0x33);
    private static readonly SolidColorBrush CurrentMatchBackground = CreateBrush(0x66);

    /// <summary>Совпадения для подсветки; пусто, когда поиск не активен.</summary>
    public IReadOnlyList<TextSearchMatch> Matches { get; set; } = [];

    /// <summary>Совпадение, подсвечиваемое как текущее, если оно есть.</summary>
    public TextSearchMatch? CurrentMatch { get; set; }

    /// <inheritdoc />
    protected override void ColorizeLine(DocumentLine line)
    {
        if (Matches.Count == 0)
            return;

        var lineStart = line.Offset;
        var lineEnd = line.EndOffset;
        foreach (var match in Matches)
        {
            if (match.Offset >= lineEnd)
                break;
            if (match.Length <= 0)
                continue;

            var start = Math.Max(match.Offset, lineStart);
            var end = Math.Min(match.Offset + match.Length, lineEnd);
            if (start >= end)
                continue;

            var brush = CurrentMatch is { } current
                        && current.Offset == match.Offset
                        && current.Length == match.Length
                ? CurrentMatchBackground
                : MatchBackground;
            ChangeLinePart(start, end, element => element.BackgroundBrush = brush);
        }
    }

    private static SolidColorBrush CreateBrush(byte alpha)
    {
        // Янтарный тон предупреждений, как у палитры редактора (#FFC107, 20% / 40% непрозрачности).
        var brush = new SolidColorBrush(Color.FromArgb(alpha, 0xFF, 0xC1, 0x07));
        brush.Freeze();
        return brush;
    }
}
