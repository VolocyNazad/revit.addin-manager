namespace AddinManager.Core.Search;

/// <summary>Controls how <see cref="TextSearchEngine.Find"/> interprets the query.</summary>
/// <param name="MatchCase">Whether the search distinguishes upper- and lower-case letters.</param>
/// <param name="WholeWords">Whether a match must occupy a whole word, so it may not start or end inside a word.</param>
/// <param name="UseRegex">Whether the query is a .NET regular expression instead of literal text.</param>
/// <remarks>
/// In literal mode a whole word is delimited by characters that are not a letter, a digit or an underscore, which keeps
/// numeric and snake_case identifiers as words. In regular-expression mode the query is wrapped in
/// <c>(?&lt;!\w)(?:...)(?!\w)</c>, so the boundary check uses the regular-expression definition of a word character.
/// </remarks>
public sealed record TextSearchOptions(bool MatchCase = false, bool WholeWords = false, bool UseRegex = false);
