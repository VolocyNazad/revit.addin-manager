using AddinManager.Core.Abstractions.Manifests;
using AddinManager.Core.Manifests;

namespace AddinManager.Core.Tests.Manifests;

public sealed class RevitManifestSchemaTests
{
    private readonly IManifestSchema _sut = new RevitManifestSchema();

    [Theory]
    [InlineData("2021")]
    [InlineData("2027")]
    public void AvailableTypes_IncludesAllThreeTypes_ForEveryVersionInOurRange(string version)
    {
        // DBApplication has been in the Revit API since 2012 (IExternalDBApplication), long
        // before our supported 2021-2027 range — the plan's original "DBApplication since 2022"
        // hypothesis did not hold up under research, see RevitManifestSchema's doc comment.
        var types = _sut.AvailableTypes(version);

        Assert.Contains(AddinEntryType.Application, types);
        Assert.Contains(AddinEntryType.DBApplication, types);
        Assert.Contains(AddinEntryType.Command, types);
    }

    [Fact]
    public void Fields_Application_HasNameNotText()
    {
        var fields = _sut.Fields("2024", AddinEntryType.Application);

        Assert.Contains(AddinEntryField.Name, fields);
        Assert.DoesNotContain(AddinEntryField.Text, fields);
        Assert.DoesNotContain(AddinEntryField.VisibilityMode, fields);
    }

    [Fact]
    public void Fields_DBApplication_MatchesApplication()
    {
        var applicationFields = _sut.Fields("2024", AddinEntryType.Application);
        var dbApplicationFields = _sut.Fields("2024", AddinEntryType.DBApplication);

        Assert.Equal(applicationFields, dbApplicationFields);
    }

    [Fact]
    public void Fields_Command_HasTextAndUiOptionsNotName()
    {
        var fields = _sut.Fields("2024", AddinEntryType.Command);

        Assert.Contains(AddinEntryField.Text, fields);
        Assert.Contains(AddinEntryField.VisibilityMode, fields);
        Assert.Contains(AddinEntryField.Discipline, fields);
        Assert.Contains(AddinEntryField.AvailabilityClassName, fields);
        Assert.DoesNotContain(AddinEntryField.Name, fields);
    }

    [Fact]
    public void Fields_AllTypes_ShareVendorFields()
    {
        Assert.Contains(AddinEntryField.VendorId, _sut.Fields("2024", AddinEntryType.Application));
        Assert.Contains(AddinEntryField.VendorId, _sut.Fields("2024", AddinEntryType.Command));
        Assert.Contains(AddinEntryField.VendorDescription, _sut.Fields("2024", AddinEntryType.DBApplication));
    }

    [Theory]
    [InlineData("2025", false)]
    [InlineData("2026", true)]
    [InlineData("2027", true)]
    public void SupportsManifestSettings_OnlyFrom2026(string version, bool expected) =>
        Assert.Equal(expected, _sut.SupportsManifestSettings(version));

    [Fact]
    public void VisibilityModeValues_AndDisciplineValues_AreNotEmpty()
    {
        Assert.NotEmpty(_sut.VisibilityModeValues);
        Assert.NotEmpty(_sut.DisciplineValues);
    }
}
