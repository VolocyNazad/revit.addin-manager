using AddinManager.Core.Manifests;

namespace AddinManager.Core.Tests.Manifests;

public sealed class AddinManifestValidatorTests
{
    private static readonly Guid FirstId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Validate_NoEntries_ReportsNoEntries()
    {
        IReadOnlyList<ManifestIssue> issues = AddinManifestValidator.Validate(NewManifest());

        ManifestIssue issue = Assert.Single(issues);
        Assert.Equal(ManifestIssueKind.NoEntries, issue.Kind);
    }

    [Fact]
    public void Validate_CompleteEntries_ReportsNothing()
    {
        IReadOnlyList<ManifestIssue> issues = AddinManifestValidator.Validate(
            NewManifest(NewEntry(FirstId), NewEntry(SecondId)));

        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_EmptyAssembly_ReportsEmptyRequiredValueForTheEntry()
    {
        IReadOnlyList<ManifestIssue> issues = AddinManifestValidator.Validate(
            NewManifest(NewEntry(FirstId, assembly: string.Empty)));

        ManifestIssue issue = Assert.Single(issues, item => item.Kind == ManifestIssueKind.EmptyRequiredValue);
        Assert.Equal(FirstId, issue.AddInId);
    }

    [Fact]
    public void Validate_EmptyFullClassName_ReportsEmptyRequiredValue()
    {
        IReadOnlyList<ManifestIssue> issues = AddinManifestValidator.Validate(
            NewManifest(NewEntry(FirstId, fullClassName: string.Empty)));

        Assert.Single(issues, item => item.Kind == ManifestIssueKind.EmptyRequiredValue);
    }

    [Fact]
    public void Validate_UnknownType_ReportsTheRawType()
    {
        IReadOnlyList<ManifestIssue> issues = AddinManifestValidator.Validate(
            NewManifest(NewEntry(FirstId, AddinEntryType.Unknown, rawType: "SomethingNew")));

        ManifestIssue issue = Assert.Single(issues, item => item.Kind == ManifestIssueKind.UnknownEntryType);
        Assert.Equal(FirstId, issue.AddInId);
        Assert.Equal("SomethingNew", issue.RawType);
    }

    [Fact]
    public void Validate_DuplicateAddInId_ReportsItOnceForTheFile()
    {
        IReadOnlyList<ManifestIssue> issues = AddinManifestValidator.Validate(
            NewManifest(NewEntry(FirstId), NewEntry(FirstId)));

        ManifestIssue issue = Assert.Single(issues, item => item.Kind == ManifestIssueKind.DuplicateAddInIdInFile);
        Assert.Equal(FirstId, issue.AddInId);
        Assert.Single(issues);
    }

    [Fact]
    public void ValidateEntry_ReportsOnlyEntryProblems()
    {
        AddinEntry entry = NewEntry(FirstId, assembly: string.Empty);

        IReadOnlyList<ManifestIssue> issues = AddinManifestValidator.ValidateEntry(entry);

        Assert.Single(issues, item => item.Kind == ManifestIssueKind.EmptyRequiredValue);
    }

    [Fact]
    public void FindDuplicateAddInIds_ReturnsOnlyIdentifiersWithMoreThanOneEntry()
    {
        IReadOnlySet<Guid> duplicated = AddinManifestValidator.FindDuplicateAddInIds(
            [NewEntry(FirstId), NewEntry(SecondId), NewEntry(FirstId)]);

        Assert.Equal([FirstId], duplicated);
    }

    [Theory]
    [InlineData("Application", true)]
    [InlineData("application", true)]
    [InlineData("DBApplication", true)]
    [InlineData("Command", true)]
    [InlineData("SomethingNew", false)]
    [InlineData(null, false)]
    public void IsRecognizedEntryType_MatchesTheParserTypes(string? rawType, bool expected)
    {
        Assert.Equal(expected, AddinManifestValidator.IsRecognizedEntryType(rawType));
    }

    [Theory]
    [InlineData("Application", AddinEntryType.Application)]
    [InlineData("DBApplication", AddinEntryType.DBApplication)]
    [InlineData("Command", AddinEntryType.Command)]
    [InlineData("unknown", AddinEntryType.Unknown)]
    [InlineData(null, AddinEntryType.Unknown)]
    public void ParseEntryType_MapsTheRawValue(string? rawType, AddinEntryType expected)
    {
        Assert.Equal(expected, AddinManifestValidator.ParseEntryType(rawType));
    }

    private static AddinManifest NewManifest(params AddinEntry[] entries) =>
        new(entries, null, null, null, null, [], [], []);

    private static AddinEntry NewEntry(
        Guid addInId,
        AddinEntryType type = AddinEntryType.Application,
        string? rawType = "Application",
        string assembly = "a.dll",
        string fullClassName = "A.App") =>
        new(
            type,
            rawType,
            "Entry",
            null,
            null,
            null,
            assembly,
            addInId,
            fullClassName,
            null,
            null,
            null,
            [],
            [],
            null,
            null,
            null,
            [],
            []);
}
