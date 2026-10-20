using System.Globalization;
using AddinManager.Core.Search;
using Microsoft.Extensions.Logging;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Состояние постоянного поиска по разметке поверх настоящего движка: пересчёт после запроса,
/// параметров и смены документа, переходы по кругу, сброс, локализованный статус и Debug-лог
/// неверного паттерна. Строки берутся из реальных resx, поэтому пропавший ключ падает здесь.
/// </summary>
public sealed class MarkupSearchViewModelTests
{
    private readonly RecordingLogger<MarkupSearchViewModel> _logger = new();

    [Fact]
    public void SettingQuery_SelectsFirstMatchAndReportsStatus()
    {
        var search = Create("alpha beta alpha");

        search.Query = "alpha";

        Assert.Equal("alpha", search.Query);
        Assert.Equal(2, search.Matches.Count);
        Assert.Equal(new TextSearchMatch(0, 5), search.Matches[0]);
        Assert.Equal(0, search.CurrentIndex);
        Assert.Equal(new TextSearchMatch(0, 5), search.CurrentMatch);
        Assert.True(search.HasMatches);
        Assert.True(search.HasStatus);
        Assert.Equal(Status(1, 2), search.StatusText);
    }

    [Fact]
    public void EmptyQuery_HasNoMatchesAndNoStatus()
    {
        var search = Create("alpha");

        search.Query = "   ";

        Assert.True(search.IsQueryEmpty);
        Assert.Empty(search.Matches);
        Assert.False(search.HasMatches);
        Assert.False(search.HasStatus);
        Assert.Equal(string.Empty, search.StatusText);
    }

    [Fact]
    public void ChangingQuery_RecomputesMatchesAndStatus()
    {
        var search = Create("alpha beta alpha");
        search.Query = "alpha";
        Assert.Equal(Status(1, 2), search.StatusText);

        search.Query = "beta";

        Assert.Single(search.Matches);
        Assert.Equal(new TextSearchMatch(6, 4), search.CurrentMatch);
        Assert.Equal(Status(1, 1), search.StatusText);
    }

    [Fact]
    public void ChangingDocument_KeepsCurrentIndexWhileItExists()
    {
        var search = Create("alpha alpha alpha");
        search.Query = "alpha";
        search.Next();
        search.Next();
        Assert.Equal(2, search.CurrentIndex);

        search.SetDocumentText("alpha beta alpha alpha");

        Assert.Equal(3, search.Matches.Count);
        Assert.Equal(2, search.CurrentIndex);
        Assert.Equal(Status(3, 3), search.StatusText);
    }

    [Fact]
    public void ShorterDocument_ClampsCurrentIndex()
    {
        var search = Create("alpha alpha alpha");
        search.Query = "alpha";
        search.Next();
        search.Next();
        Assert.Equal(2, search.CurrentIndex);

        search.SetDocumentText("alpha");

        Assert.Single(search.Matches);
        Assert.Equal(0, search.CurrentIndex);
        Assert.Equal(Status(1, 1), search.StatusText);
    }

    [Fact]
    public void NextCommand_WrapsToFirstMatch()
    {
        var search = Create("alpha alpha alpha");
        search.Query = "alpha";

        search.Next();
        search.Next();
        Assert.Equal(new TextSearchMatch(12, 5), search.CurrentMatch);

        search.Next();

        Assert.Equal(0, search.CurrentIndex);
        Assert.Equal(new TextSearchMatch(0, 5), search.CurrentMatch);
    }

    [Fact]
    public void PreviousCommand_WrapsToLastMatch()
    {
        var search = Create("alpha alpha alpha");
        search.Query = "alpha";

        search.Previous();

        Assert.Equal(2, search.CurrentIndex);
        Assert.Equal(new TextSearchMatch(12, 5), search.CurrentMatch);
    }

    [Fact]
    public void MatchCaseOption_RecomputesMatches()
    {
        var search = Create("Alpha alpha");
        search.Query = "Alpha";
        Assert.Equal(2, search.Matches.Count);

        search.MatchCase = true;

        Assert.Single(search.Matches);
        Assert.Equal(new TextSearchMatch(0, 5), search.CurrentMatch);
    }

    [Fact]
    public void WholeWordsOption_OnlyMatchesWholeWords()
    {
        var search = Create("cat category");
        search.Query = "cat";
        Assert.Equal(2, search.Matches.Count);

        search.WholeWords = true;

        Assert.Single(search.Matches);
        Assert.Equal(new TextSearchMatch(0, 3), search.CurrentMatch);
    }

    [Fact]
    public void UseRegexOption_MatchesPattern()
    {
        var search = Create("rule 1\nrule 22");
        search.UseRegex = true;

        search.Query = @"rule \d+";

        Assert.Equal(2, search.Matches.Count);
        Assert.Equal(new TextSearchMatch(0, 6), search.Matches[0]);
        Assert.Equal(new TextSearchMatch(7, 7), search.Matches[1]);
    }

    [Fact]
    public void InvalidPattern_ReportsErrorAndNoMatches()
    {
        var search = Create("alpha");
        search.UseRegex = true;

        search.Query = "(";

        Assert.Empty(search.Matches);
        Assert.False(search.HasMatches);
        Assert.Equal(-1, search.CurrentIndex);
        Assert.Null(search.CurrentMatch);
        Assert.True(search.HasStatus);
        Assert.Equal(TestLocalization.For<MarkupSearchViewModel>()["MarkupSearchInvalidPattern"].Value, search.StatusText);
    }

    [Fact]
    public void QueryWithoutMatches_ReportsNoMatches()
    {
        var search = Create("alpha");

        search.Query = "missing";

        Assert.Equal(TestLocalization.For<MarkupSearchViewModel>()["MarkupSearchNoMatches"].Value, search.StatusText);
        Assert.True(search.HasStatus);
        Assert.False(search.HasMatches);
        Assert.Equal(-1, search.CurrentIndex);
    }

    [Fact]
    public void Clear_DropsQueryMatchesAndStatus()
    {
        var search = Create("alpha beta alpha");
        search.Query = "alpha";
        search.Next();
        Assert.True(search.HasMatches);

        search.Clear();

        Assert.Equal(string.Empty, search.Query);
        Assert.True(search.IsQueryEmpty);
        Assert.Empty(search.Matches);
        Assert.False(search.HasMatches);
        Assert.Equal(-1, search.CurrentIndex);
        Assert.Null(search.CurrentMatch);
        Assert.Equal(string.Empty, search.StatusText);
        Assert.False(search.HasStatus);
    }

    [Fact]
    public void Commands_MoveBetweenMatches()
    {
        var search = Create("alpha alpha alpha");
        search.Query = "alpha";
        Assert.Equal(0, search.CurrentIndex);

        search.NextCommand.Execute(null);
        Assert.Equal(1, search.CurrentIndex);

        search.PreviousCommand.Execute(null);
        Assert.Equal(0, search.CurrentIndex);
    }

    [Fact]
    public void LocalizedLabels_ComeFromResources()
    {
        var search = Create();
        var localizer = TestLocalization.For<MarkupSearchViewModel>();

        Assert.Equal(localizer["MarkupSearchPlaceholder"].Value, search.Placeholder);
        Assert.Equal(localizer["MarkupSearchNextTooltip"].Value, search.NextTooltip);
        Assert.Equal(localizer["MarkupSearchPreviousTooltip"].Value, search.PreviousTooltip);
        Assert.Equal(localizer["MarkupSearchMatchCaseTooltip"].Value, search.MatchCaseTooltip);
        Assert.Equal(localizer["MarkupSearchWholeWordsTooltip"].Value, search.WholeWordsTooltip);
        Assert.Equal(localizer["MarkupSearchUseRegexTooltip"].Value, search.UseRegexTooltip);
        Assert.All(
            [
                search.Placeholder, search.NextTooltip, search.PreviousTooltip,
                search.MatchCaseTooltip, search.WholeWordsTooltip, search.UseRegexTooltip,
            ],
            value => Assert.False(string.IsNullOrWhiteSpace(value)));
    }

    [Fact]
    public void RefreshingLocalizedText_NotifiesLabelsAndStatus()
    {
        var search = Create("alpha alpha");
        search.Query = "alpha";
        HashSet<string> raised = [];
        search.PropertyChanged += (_, args) => raised.Add(args.PropertyName!);

        search.RefreshLocalizedText();

        Assert.Contains(nameof(MarkupSearchViewModel.Placeholder), raised);
        Assert.Contains(nameof(MarkupSearchViewModel.NextTooltip), raised);
        Assert.Contains(nameof(MarkupSearchViewModel.PreviousTooltip), raised);
        Assert.Contains(nameof(MarkupSearchViewModel.MatchCaseTooltip), raised);
        Assert.Contains(nameof(MarkupSearchViewModel.WholeWordsTooltip), raised);
        Assert.Contains(nameof(MarkupSearchViewModel.UseRegexTooltip), raised);
        Assert.Contains(nameof(MarkupSearchViewModel.StatusText), raised);
    }

    [Fact]
    public void InvalidPattern_LoggedAtDebugOnly()
    {
        var search = Create("alpha");
        search.UseRegex = true;

        search.Query = "(";

        Assert.True(_logger.HasEntry(LogLevel.Debug, "Неверный паттерн"));
        Assert.DoesNotContain(_logger.Entries, entry => entry.Level >= LogLevel.Information);
    }

    private MarkupSearchViewModel Create(string? documentText = null)
    {
        var search = new MarkupSearchViewModel(TestLocalization.For<MarkupSearchViewModel>(), _logger);
        if (documentText is not null)
            search.SetDocumentText(documentText);

        return search;
    }

    private static string Status(int current, int count) => TestLocalization.For<MarkupSearchViewModel>()[
        "MarkupSearchMatchStatus",
        current.ToString(CultureInfo.InvariantCulture),
        count.ToString(CultureInfo.InvariantCulture)].Value;
}
