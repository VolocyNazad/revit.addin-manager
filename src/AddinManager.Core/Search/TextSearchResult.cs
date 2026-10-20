namespace AddinManager.Core.Search;

/// <summary>Describes the outcome of a search.</summary>
/// <param name="Matches">The found occurrences in the order they appear in the text; never <see langword="null"/>.</param>
/// <param name="Error">Why the search stopped before it covered the whole text, or <see cref="TextSearchError.None"/>.</param>
/// <remarks>
/// A result with <see cref="TextSearchError.Timeout"/> or a reached match limit still carries the matches found before
/// the search stopped, so the caller can show what is already known.
/// </remarks>
public sealed record TextSearchResult(
    IReadOnlyList<TextSearchMatch> Matches,
    TextSearchError Error = TextSearchError.None);
