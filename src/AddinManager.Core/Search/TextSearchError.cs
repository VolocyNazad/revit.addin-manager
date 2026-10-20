namespace AddinManager.Core.Search;

/// <summary>Describes why a search stopped before it covered the whole text.</summary>
public enum TextSearchError
{
    /// <summary>The search completed; <see cref="TextSearchResult.Matches"/> holds every occurrence up to the match limit.</summary>
    None,

    /// <summary>The regular-expression pattern is not valid, so nothing was searched.</summary>
    InvalidPattern,

    /// <summary>The regular expression exceeded its time budget; the matches found before that are returned.</summary>
    Timeout,
}
