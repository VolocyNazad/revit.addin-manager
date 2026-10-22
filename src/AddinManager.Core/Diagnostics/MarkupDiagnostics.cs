using System.Text.RegularExpressions;
using AddinManager.Core.Manifests;

namespace AddinManager.Core.Diagnostics;

/// <summary>
/// Диагностика сырого XML разметки: срезы пустых обязательных полей, неизвестного <c>Type</c>
/// записи и повторяющихся <c>AddInId</c> (внутри файла и в других файлах той же версии), а также
/// срез блока выбранной записи. Живёт в Core и работает по тексту, поэтому не зависит от
/// AvalonEdit и тестируется без UI; структурные правила берутся из
/// <see cref="AddinManifestValidator"/>.
/// </summary>
public static partial class MarkupDiagnostics
{
    /// <summary>
    /// Срезы проблемных мест текста: пустые обязательные элементы, неизвестный <c>Type</c> записи,
    /// каждое вхождение повторяющегося значения <c>AddInId</c> внутри текста и каждое вхождение
    /// значения из <paramref name="externalIds"/> (дубль в другом файле той же версии). Пустые
    /// невалидные значения пропускаем в поиске дублей: они уже помечены как пустые. Регистр
    /// тегов — точный, как у парсера (чужой регистр для него — отсутствие элемента, а не пустота).
    /// </summary>
    /// <param name="xml">Текст разметки; <see langword="null"/> или пусто — пустой результат.</param>
    /// <param name="externalIds">Идентификаторы других файлов той же версии; <see langword="null"/> — нет.</param>
    /// <returns>Срезы в порядке появления; пустой список, если проблем нет.</returns>
    public static IReadOnlyList<MarkupSpan> FindDiagnosticSpans(string? xml, IReadOnlySet<Guid>? externalIds = null)
    {
        if (string.IsNullOrEmpty(xml))
            return [];

        var spans = new List<MarkupSpan>();
        foreach (Match empty in EmptyFieldPattern().Matches(xml))
            spans.Add(new MarkupSpan(empty.Index, empty.Length));

        foreach (Match typeMatch in EntryTypePattern().Matches(xml))
        {
            Group type = typeMatch.Groups["type"];
            if (!AddinManifestValidator.IsRecognizedEntryType(type.Value.Trim()))
                spans.Add(new MarkupSpan(type.Index, type.Length));
        }

        var byId = new Dictionary<string, List<Match>>(StringComparer.OrdinalIgnoreCase);
        foreach (Match candidate in EntryIdPattern().Matches(xml))
        {
            var id = candidate.Groups["id"].Value.Trim();
            if (id.Length == 0)
                continue;

            if (!byId.TryGetValue(id, out var list))
                byId[id] = list = [];
            list.Add(candidate);
        }

        var marked = new HashSet<Match>();
        foreach (var list in byId.Values)
        {
            if (list.Count > 1)
            {
                foreach (var duplicate in list)
                {
                    spans.Add(new MarkupSpan(duplicate.Index, duplicate.Length));
                    marked.Add(duplicate);
                }
            }
        }

        if (externalIds is { Count: > 0 })
        {
            foreach (var list in byId.Values)
            {
                foreach (var candidate in list)
                {
                    if (marked.Contains(candidate))
                        continue;
                    if (Guid.TryParse(candidate.Groups["id"].Value.Trim(), out var guid) && externalIds.Contains(guid))
                        spans.Add(new MarkupSpan(candidate.Index, candidate.Length));
                }
            }
        }

        return spans;
    }

    /// <summary>
    /// Срез от открывающего <c>&lt;AddIn</c> до закрывающего <c>&lt;/AddIn&gt;</c> через
    /// <c>AddInId</c> записи. Вложенных <c>AddIn</c> не бывает, первое закрытие после начала —
    /// наше.
    /// </summary>
    /// <param name="xml">Текст разметки; <see langword="null"/> или пусто — результата нет.</param>
    /// <param name="addInId">Идентификатор записи.</param>
    /// <returns>Срез блока записи; <see langword="null"/>, если идентификатор или границы не нашлись.</returns>
    public static MarkupSpan? FindEntrySpan(string? xml, Guid addInId)
    {
        if (string.IsNullOrEmpty(xml))
            return null;

        Match? idMatch = null;
        foreach (Match candidate in EntryIdPattern().Matches(xml))
        {
            if (Guid.TryParse(candidate.Groups["id"].Value, out var found) && found == addInId)
            {
                idMatch = candidate;
                break;
            }
        }

        if (idMatch is null)
            return null;

        var openMatch = OpenEntryPattern().Match(xml, idMatch.Index);
        if (!openMatch.Success)
            return null;

        const string closeTag = "</AddIn>";
        var closeIndex = xml.IndexOf(closeTag, openMatch.Index, StringComparison.Ordinal);
        if (closeIndex < 0)
            return null;

        return new MarkupSpan(openMatch.Index, closeIndex + closeTag.Length - openMatch.Index);
    }

    [GeneratedRegex("<AddInId>\\s*(?<id>[^<]*?)\\s*</AddInId>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EntryIdPattern();

    [GeneratedRegex("<(Assembly|FullClassName|AddInId)\\s*(?:/\\s*>|>\\s*</\\1\\s*>)", RegexOptions.CultureInvariant)]
    private static partial Regex EmptyFieldPattern();

    [GeneratedRegex("<AddIn(?=[\\s>])[^>]*?\\bType\\s*=\\s*(?<quote>[\"'])(?<type>.*?)\\k<quote>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EntryTypePattern();

    [GeneratedRegex("<AddIn(?=[\\s>])", RegexOptions.RightToLeft | RegexOptions.CultureInvariant)]
    private static partial Regex OpenEntryPattern();
}
