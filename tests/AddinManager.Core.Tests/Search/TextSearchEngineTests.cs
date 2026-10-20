using AddinManager.Core.Search;

namespace AddinManager.Core.Tests.Search;

/// <summary>
/// Хедлесс-движок поиска по тексту разметки: литералы, регистр, целые слова, регулярные выражения,
/// ошибки паттерна и таймаут, лимит совпадений. Движок не знает про WPF — проверяется как чистая функция.
/// </summary>
public sealed class TextSearchEngineTests
{
    [Fact]
    public void CaseSensitiveQuery_MatchesSameCaseOnly()
    {
        var result = TextSearchEngine.Find("Foo foo", "Foo", new TextSearchOptions(MatchCase: true));

        Assert.Equal([new TextSearchMatch(0, 3)], result.Matches);
        Assert.Equal(TextSearchError.None, result.Error);
    }

    [Fact]
    public void CaseInsensitiveQuery_MatchesBothCases_ByDefault()
    {
        var result = TextSearchEngine.Find("Foo foo", "foo", new TextSearchOptions());

        Assert.Equal([new TextSearchMatch(0, 3), new TextSearchMatch(4, 3)], result.Matches);
        Assert.Equal(TextSearchError.None, result.Error);
    }

    [Fact]
    public void WholeWordQuery_MatchesWordBetweenSeparators()
    {
        var result = TextSearchEngine.Find("the cat, sat", "cat", new TextSearchOptions(WholeWords: true));

        Assert.Equal([new TextSearchMatch(4, 3)], result.Matches);
    }

    [Fact]
    public void WholeWordQuery_DoesNotMatchInsideLongerWord()
    {
        var result = TextSearchEngine.Find("concatenate cat", "cat", new TextSearchOptions(WholeWords: true));

        Assert.Equal([new TextSearchMatch(12, 3)], result.Matches);
    }

    [Fact]
    public void WholeWordQuery_TreatsDigitAsWordCharacter()
    {
        var result = TextSearchEngine.Find("cat2 cat", "cat", new TextSearchOptions(WholeWords: true));

        Assert.Equal([new TextSearchMatch(5, 3)], result.Matches);
    }

    [Fact]
    public void WholeWordQuery_TreatsUnderscoreAsWordCharacter()
    {
        var result = TextSearchEngine.Find("cat_ cat", "cat", new TextSearchOptions(WholeWords: true));

        Assert.Equal([new TextSearchMatch(5, 3)], result.Matches);
    }

    [Fact]
    public void WholeWordQuery_RecognizesCyrillicLetters()
    {
        var result = TextSearchEngine.Find("Кот котлета", "кот", new TextSearchOptions(WholeWords: true));

        Assert.Equal([new TextSearchMatch(0, 3)], result.Matches);
    }

    [Fact]
    public void MultipleOccurrences_KeepTextOrderAndOffsets()
    {
        var result = TextSearchEngine.Find("ab ab ab", "ab", new TextSearchOptions());

        Assert.Equal(
            [new TextSearchMatch(0, 2), new TextSearchMatch(3, 2), new TextSearchMatch(6, 2)],
            result.Matches);
    }

    [Fact]
    public void OccurrencesOnDifferentLines_KeepAbsoluteOffsets()
    {
        var result = TextSearchEngine.Find("first\nneedle\nlast needle", "needle", new TextSearchOptions());

        Assert.Equal([new TextSearchMatch(6, 6), new TextSearchMatch(18, 6)], result.Matches);
    }

    [Fact]
    public void SameCharacters_AreNotMatchedTwice()
    {
        var result = TextSearchEngine.Find("aaa", "aa", new TextSearchOptions());

        Assert.Equal([new TextSearchMatch(0, 2)], result.Matches);
    }

    [Fact]
    public void AdjacentOccurrences_AreFoundBackToBack()
    {
        var result = TextSearchEngine.Find("aaaa", "aa", new TextSearchOptions());

        Assert.Equal([new TextSearchMatch(0, 2), new TextSearchMatch(2, 2)], result.Matches);
    }

    [Fact]
    public void TextWithoutOccurrences_ReturnsNoMatchesAndNoError()
    {
        var result = TextSearchEngine.Find("abc", "xyz", new TextSearchOptions());

        Assert.Empty(result.Matches);
        Assert.Equal(TextSearchError.None, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \t")]
    public void MissingOrBlankQuery_ReturnsNoMatchesAndNoError(string? query)
    {
        var result = TextSearchEngine.Find("abc", query, new TextSearchOptions(UseRegex: true));

        Assert.Empty(result.Matches);
        Assert.Equal(TextSearchError.None, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MissingOrEmptyText_ReturnsNoMatches(string? text)
    {
        var result = TextSearchEngine.Find(text, "abc", new TextSearchOptions());

        Assert.Empty(result.Matches);
        Assert.Equal(TextSearchError.None, result.Error);
    }

    [Fact]
    public void NullOptions_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => TextSearchEngine.Find("abc", "abc", null!));
    }

    [Fact]
    public void RegexQuery_MatchesPattern()
    {
        var result = TextSearchEngine.Find("a1 b2", @"\d", new TextSearchOptions(UseRegex: true));

        Assert.Equal([new TextSearchMatch(1, 1), new TextSearchMatch(4, 1)], result.Matches);
    }

    [Fact]
    public void CaseSensitiveRegex_MatchesSameCaseOnly()
    {
        var result = TextSearchEngine.Find("Foo foo", "foo", new TextSearchOptions(MatchCase: true, UseRegex: true));

        Assert.Equal([new TextSearchMatch(4, 3)], result.Matches);
    }

    [Fact]
    public void Regex_IgnoresCaseByDefault()
    {
        var result = TextSearchEngine.Find("Foo foo", "foo", new TextSearchOptions(UseRegex: true));

        Assert.Equal([new TextSearchMatch(0, 3), new TextSearchMatch(4, 3)], result.Matches);
    }

    [Fact]
    public void RegexWithWholeWords_DoesNotMatchInsideLongerWord()
    {
        var result = TextSearchEngine.Find(
            "concatenate cat",
            "cat",
            new TextSearchOptions(WholeWords: true, UseRegex: true));

        Assert.Equal([new TextSearchMatch(12, 3)], result.Matches);
    }

    [Fact]
    public void EmptyRegexMatch_DoesNotLoopForever()
    {
        var result = TextSearchEngine.Find("abc", "x*", new TextSearchOptions(UseRegex: true));

        Assert.Equal(
            [
                new TextSearchMatch(0, 0),
                new TextSearchMatch(1, 0),
                new TextSearchMatch(2, 0),
                new TextSearchMatch(3, 0),
            ],
            result.Matches);
        Assert.Equal(TextSearchError.None, result.Error);
    }

    [Fact]
    public void InvalidRegex_ReportsInvalidPattern()
    {
        var result = TextSearchEngine.Find("abc", "(", new TextSearchOptions(UseRegex: true));

        Assert.Empty(result.Matches);
        Assert.Equal(TextSearchError.InvalidPattern, result.Error);
    }

    [Fact]
    public void InvalidRegexWithWholeWords_ReportsInvalidPattern()
    {
        var result = TextSearchEngine.Find("abc", "[", new TextSearchOptions(WholeWords: true, UseRegex: true));

        Assert.Equal(TextSearchError.InvalidPattern, result.Error);
    }

    [Fact]
    public void RegexExceedingTimeBudget_ReportsTimeout()
    {
        var text = new string('a', 100_000) + "!";

        var result = TextSearchEngine.Find(text, "(a+)+$", new TextSearchOptions(UseRegex: true));

        Assert.Equal(TextSearchError.Timeout, result.Error);
    }

    [Fact]
    public void LongText_StopsAtMatchLimit()
    {
        var text = new string('a', TextSearchEngine.MatchLimit + 5);

        var result = TextSearchEngine.Find(text, "a", new TextSearchOptions());

        Assert.Equal(TextSearchEngine.MatchLimit, result.Matches.Count);
        Assert.Equal(0, result.Matches[0].Offset);
        Assert.Equal(TextSearchEngine.MatchLimit - 1, result.Matches[^1].Offset);
        Assert.Equal(TextSearchError.None, result.Error);
    }
}
