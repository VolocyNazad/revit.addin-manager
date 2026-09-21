using System.Windows;
using System.Windows.Media;
using AddinManager.Launcher.ViewModels;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace AddinManager.Launcher.Views.Highlighting;

/// <summary>
/// Волнистое подчёркивание диагностических срезов (<see cref="MarkupViewModel.DiagnosticSpans"/>
/// — пустые обязательные поля и повторяющиеся <c>AddInId</c>): семантические маркеры поверх
/// синтаксической подсветки. Выделение и каретку не трогает (в отличие от селекции блока
/// выбранной записи), поэтому вид просто перерисовывает слой при каждом новом списке срезов.
/// Цвет — тот же красный ошибок, что у строчных и полевых сообщений (#E5484D).
/// </summary>
public sealed class DiagnosticBackgroundRenderer : IBackgroundRenderer
{
    private static readonly Pen MarkerPen = CreateMarkerPen();

    private static Pen CreateMarkerPen()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D)), 1.2);
        pen.Freeze();
        return pen;
    }

    private IReadOnlyList<TextSpan> _spans = [];

    /// <inheritdoc />
    public KnownLayer Layer => KnownLayer.Selection;

    /// <summary>Заменяет срезы (вызывает вид при новых <see cref="MarkupViewModel.DiagnosticSpans"/>).</summary>
    public void SetSpans(IEnumerable<TextSpan> spans) => _spans = spans.ToList();

    /// <inheritdoc />
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_spans.Count == 0 || textView.Document is null)
            return;

        foreach (var span in _spans)
        {
            var start = Math.Max(0, span.Start);
            var end = Math.Min(textView.Document.TextLength, span.Start + span.Length);
            if (end <= start)
                continue;

            var segment = new TextSegment { StartOffset = start, EndOffset = end };
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                DrawWavyLine(drawingContext, rect);
        }
    }

    private static void DrawWavyLine(DrawingContext drawingContext, Rect rect)
    {
        const double wavelength = 6.0;
        const double amplitude = 1.6;

        var baseline = rect.Bottom - 0.5;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var first = true;
            for (var x = rect.Left; x <= rect.Right; x += 1.0)
            {
                var y = baseline - amplitude * Math.Abs(Math.Sin((x - rect.Left) / wavelength * Math.PI));
                var point = new Point(x, y);
                if (first)
                {
                    context.BeginFigure(point, false, false);
                    first = false;
                }
                else
                {
                    context.LineTo(point, true, false);
                }
            }
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, MarkerPen, geometry);
    }
}
