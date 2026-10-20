using System.Text.RegularExpressions;

namespace AddinManager.Core.Search;

/// <summary>Finds occurrences of a literal or regular-expression query in text.</summary>
/// <remarks>
/// The engine is headless and stateless: it compares strings only and never touches the file system, WPF or Revit.
/// Searching stops after <see cref="MatchLimit"/> matches, and a regular expression is limited to
/// <see cref="RegexTimeout"/>; both cases return the matches found before the stop. Occurrences never overlap:
/// the next search starts after the previous match, so <c>aa</c> in <c>aaa</c> is found once.
/// </remarks>
public static class TextSearchEngine
{
    /// <summary>The maximum number of matches a single search returns.</summary>
    public const int MatchLimit = 10_000;

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly TextSearchResult EmptyResult = new(Array.Empty<TextSearchMatch>());

    /// <summary>Finds occurrences of <paramref name="query"/> in <paramref name="text"/>.</summary>
    /// <param name="text">The text to search; <see langword="null"/> is treated as empty.</param>
    /// <param name="query">The literal text or the regular expression to find; <see langword="null"/> is treated as empty.</param>
    /// <param name="options">How to interpret the query.</param>
    /// <returns>The matches and the reason the search stopped early, if it did.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// A <see langword="null"/>, empty or whitespace-only query returns an empty result without an error, so the caller
    /// can run the engine on every keystroke. A literal query is compared with <see cref="StringComparison.Ordinal"/>,
    /// or <see cref="StringComparison.OrdinalIgnoreCase"/> unless <see cref="TextSearchOptions.MatchCase"/> is set; a
    /// regular expression is compiled with <see cref="RegexOptions.CultureInvariant"/> and, unless
    /// <see cref="TextSearchOptions.MatchCase"/> is set, <see cref="RegexOptions.IgnoreCase"/>. An invalid pattern
    /// returns <see cref="TextSearchError.InvalidPattern"/> and no matches. A regular expression that exceeds its time
    /// budget returns <see cref="TextSearchError.Timeout"/> together with the matches found so far. An empty
    /// regular-expression match advances by one character, so a pattern such as <c>x*</c> cannot loop forever. Offsets
    /// and lengths index the original text, so a match that spans a line break includes the line break in its length.
    /// </remarks>
    public static TextSearchResult Find(string? text, string? query, TextSearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrEmpty(text))
        {
            return EmptyResult;
        }

        return options.UseRegex
            ? FindRegex(text, query, options)
            : FindLiteral(text, query, options);
    }

    private static TextSearchResult FindLiteral(string text, string query, TextSearchOptions options)
    {
        StringComparison comparison = options.MatchCase
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        List<TextSearchMatch> matches = [];

        int start = 0;
        while (start <= text.Length - query.Length)
        {
            int offset = text.IndexOf(query, start, comparison);
            if (offset < 0)
            {
                break;
            }

            if (options.WholeWords && !IsWholeWord(text, offset, query.Length))
            {
                // The candidate starts inside a longer word; the next candidate may still begin one character later.
                start = offset + 1;
                continue;
            }

            matches.Add(new TextSearchMatch(offset, query.Length));
            if (matches.Count >= MatchLimit)
            {
                break;
            }

            start = offset + query.Length;
        }

        return new TextSearchResult(matches);
    }

    private static TextSearchResult FindRegex(string text, string query, TextSearchOptions options)
    {
        string pattern = options.WholeWords ? $@"(?<!\w)(?:{query})(?!\w)" : query;
        RegexOptions regexOptions = RegexOptions.CultureInvariant;
        if (!options.MatchCase)
        {
            regexOptions |= RegexOptions.IgnoreCase;
        }

        Regex regex;
        try
        {
            regex = new Regex(pattern, regexOptions, RegexTimeout);
        }
        catch (ArgumentException)
        {
            return new TextSearchResult(Array.Empty<TextSearchMatch>(), TextSearchError.InvalidPattern);
        }

        List<TextSearchMatch> matches = [];
        int start = 0;
        try
        {
            while (start <= text.Length)
            {
                Match match = regex.Match(text, start);
                if (!match.Success)
                {
                    break;
                }

                matches.Add(new TextSearchMatch(match.Index, match.Length));
                if (matches.Count >= MatchLimit)
                {
                    break;
                }

                // An empty match would otherwise be found at the same index forever.
                start = match.Index + Math.Max(match.Length, 1);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return new TextSearchResult(matches, TextSearchError.Timeout);
        }

        return new TextSearchResult(matches);
    }

    private static bool IsWholeWord(string text, int offset, int length)
    {
        if (offset > 0 && IsWordCharacter(text[offset - 1]))
        {
            return false;
        }

        int end = offset + length;
        return end >= text.Length || !IsWordCharacter(text[end]);
    }

    private static bool IsWordCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';
}
