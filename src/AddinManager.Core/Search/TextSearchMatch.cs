namespace AddinManager.Core.Search;

/// <summary>Describes one occurrence found in the searched text.</summary>
/// <param name="Offset">The zero-based index of the first character of the occurrence in the original text.</param>
/// <param name="Length">The number of characters in the occurrence; zero for an empty regular-expression match.</param>
public readonly record struct TextSearchMatch(int Offset, int Length);
