using AddinManager.Core.Manifests;
using AddinManager.Core.Storage;

namespace AddinManager.Core.Tests.Storage;

public sealed class AddinFileDuplicatesTests
{
    private static readonly Guid FirstId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ThirdId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void FindInOtherFiles_IdInAnotherFileOfSameVersion_IsReturned()
    {
        AddinFile current = NewFile("Current.addin", "2025", AddinScope.User, FirstId);
        AddinFile other = NewFile("Other.addin", "2025", AddinScope.User, SecondId);

        HashSet<Guid> otherIds = AddinFileDuplicates.FindInOtherFiles(current, [current, other]);

        Assert.Equal([SecondId], otherIds);
    }

    [Fact]
    public void FindInOtherFiles_IdAlsoPresentInCurrentFile_IsReturned()
    {
        AddinFile current = NewFile("Current.addin", "2025", AddinScope.User, FirstId);
        AddinFile other = NewFile("Other.addin", "2025", AddinScope.Machine, FirstId);

        HashSet<Guid> otherIds = AddinFileDuplicates.FindInOtherFiles(current, [current, other]);

        Assert.Equal([FirstId], otherIds);
    }

    [Fact]
    public void FindInOtherFiles_FileOfAnotherVersion_IsIgnored()
    {
        AddinFile current = NewFile("Current.addin", "2025", AddinScope.User, FirstId);
        AddinFile otherVersion = NewFile("Other.addin", "2026", AddinScope.User, SecondId);

        HashSet<Guid> otherIds = AddinFileDuplicates.FindInOtherFiles(current, [current, otherVersion]);

        Assert.Empty(otherIds);
    }

    [Fact]
    public void FindInOtherFiles_SameIdentity_IsExcluded()
    {
        AddinFile current = NewFile("Current.addin", "2025", AddinScope.User, FirstId);
        AddinFile sameIdentity = NewFile("Current.addin", "2025", AddinScope.User, SecondId);

        HashSet<Guid> otherIds = AddinFileDuplicates.FindInOtherFiles(current, [current, sameIdentity]);

        Assert.Empty(otherIds);
    }

    [Fact]
    public void FindInOtherFiles_NoOtherFiles_ReturnsEmpty()
    {
        AddinFile current = NewFile("Current.addin", "2025", AddinScope.User, FirstId);

        Assert.Empty(AddinFileDuplicates.FindInOtherFiles(current, [current]));
    }

    [Fact]
    public void FindInOtherFiles_NullFile_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => AddinFileDuplicates.FindInOtherFiles(null!, []));
    }

    [Fact]
    public void FindDuplicatedByVersion_IdInTwoFiles_IsReported()
    {
        AddinFile first = NewFile("First.addin", "2025", AddinScope.User, FirstId);
        AddinFile second = NewFile("Second.addin", "2025", AddinScope.User, FirstId);

        Dictionary<string, HashSet<Guid>> duplicated = AddinFileDuplicates.FindDuplicatedByVersion([first, second]);

        Assert.Equal([FirstId], duplicated["2025"]);
    }

    [Fact]
    public void FindDuplicatedByVersion_IdRepeatedInsideOneFile_IsNotReported()
    {
        AddinFile file = NewFile("Solo.addin", "2025", AddinScope.User, FirstId, FirstId);

        Assert.Empty(AddinFileDuplicates.FindDuplicatedByVersion([file]));
    }

    [Fact]
    public void FindDuplicatedByVersion_DifferentVersions_AreSeparate()
    {
        AddinFile current = NewFile("Current.addin", "2025", AddinScope.User, FirstId);
        AddinFile currentAgain = NewFile("CurrentAgain.addin", "2025", AddinScope.User, FirstId);
        AddinFile otherVersion = NewFile("Other.addin", "2026", AddinScope.User, FirstId);

        Dictionary<string, HashSet<Guid>> duplicated =
            AddinFileDuplicates.FindDuplicatedByVersion([current, currentAgain, otherVersion]);

        Assert.Single(duplicated);
        Assert.Equal([FirstId], duplicated["2025"]);
    }

    [Fact]
    public void FindDuplicatedByVersion_SameNameInTwoScopes_CountsAsTwoFiles()
    {
        AddinFile user = NewFile("Same.addin", "2025", AddinScope.User, FirstId);
        AddinFile machine = NewFile("Same.addin", "2025", AddinScope.Machine, FirstId);

        Dictionary<string, HashSet<Guid>> duplicated = AddinFileDuplicates.FindDuplicatedByVersion([user, machine]);

        Assert.Equal([FirstId], duplicated["2025"]);
    }

    [Fact]
    public void FindDuplicatedByVersion_OnlyUnrelatedIds_ReturnsEmpty()
    {
        AddinFile first = NewFile("First.addin", "2025", AddinScope.User, FirstId, SecondId);
        AddinFile second = NewFile("Second.addin", "2025", AddinScope.User, ThirdId);

        Assert.Empty(AddinFileDuplicates.FindDuplicatedByVersion([first, second]));
    }

    [Fact]
    public void FindDuplicatedByVersion_NullFiles_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => AddinFileDuplicates.FindDuplicatedByVersion(null!));
    }

    [Fact]
    public void FindInOtherFiles_SameIdentityWithDifferentPathAndState_IsExcluded()
    {
        AddinFile current = NewFile("Current.addin", "2025", AddinScope.User, FirstId);
        AddinFile sameIdentity = NewFile("Current.addin", "2025", AddinScope.User, SecondId) with
        {
            Enabled = false,
            FullPath = @"C:\fake\2025\disabled\Current.addin",
        };

        HashSet<Guid> otherIds = AddinFileDuplicates.FindInOtherFiles(current, [current, sameIdentity]);

        Assert.Empty(otherIds);
    }

    [Fact]
    public void FindDuplicatedByVersion_SameIdentityWithDifferentPath_CountsAsOneFile()
    {
        AddinFile first = NewFile("Same.addin", "2025", AddinScope.User, FirstId);
        AddinFile sameIdentity = first with { FullPath = @"C:\fake\2025\disabled\Same.addin", Enabled = false };

        Assert.Empty(AddinFileDuplicates.FindDuplicatedByVersion([first, sameIdentity]));
    }

    private static AddinFile NewFile(string fileName, string version, AddinScope scope, params Guid[] addInIds)
    {
        AddinEntry[] entries = [.. addInIds.Select(NewEntry)];
        var manifest = new AddinManifest(entries, null, null, null, null, [], [], []);
        string versionRoot = $@"C:\fake\{version}";
        return new AddinFile(fileName, scope, version, true, Path.Combine(versionRoot, fileName), versionRoot, manifest);
    }

    private static AddinEntry NewEntry(Guid addInId) => new(
        AddinEntryType.Application,
        "Application",
        null,
        null,
        null,
        null,
        "AddinManager.dll",
        addInId,
        "AddinManager.Command",
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
