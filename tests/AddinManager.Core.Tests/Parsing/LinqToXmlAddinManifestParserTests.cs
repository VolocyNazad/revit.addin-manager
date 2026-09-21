using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Manifests;
using AddinManager.Core.Parsing;
using AddinManager.Core.Tests.TestDoubles;

namespace AddinManager.Core.Tests.Parsing;

public sealed class LinqToXmlAddinManifestParserTests
{
    private readonly IAddinManifestParser _sut =
        new LinqToXmlAddinManifestParser(TestLocalization.For<LinqToXmlAddinManifestParser>());

    private const string ApplicationXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <RevitAddIns>
          <AddIn Type="Application">
            <Name>Revit.Linter</Name>
            <Description>Linter</Description>
            <Assembly>C:\Addins\Revit.Linter.dll</Assembly>
            <AddInId>11111111-1111-1111-1111-111111111111</AddInId>
            <FullClassName>Revit.Linter.App</FullClassName>
            <VendorId>Volocy</VendorId>
            <VendorDescription>Volocy tools</VendorDescription>
          </AddIn>
        </RevitAddIns>
        """;

    [Fact]
    public void Parse_ApplicationEntry_AllFields()
    {
        var manifest = _sut.Parse(ApplicationXml);

        var entry = Assert.Single(manifest.Entries);
        Assert.Equal(AddinEntryType.Application, entry.Type);
        Assert.Equal("Revit.Linter", entry.Name);
        Assert.Equal(@"C:\Addins\Revit.Linter.dll", entry.AssemblyPath);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), entry.AddInId);
        Assert.Equal("Revit.Linter.App", entry.FullClassName);
        Assert.Equal("Volocy", entry.VendorId);
        Assert.Null(entry.Text);
    }

    [Fact]
    public void Parse_CommandEntry_MultiValues()
    {
        const string xml = """
            <RevitAddIns>
              <AddIn Type="Command">
                <Text>Open log</Text>
                <Assembly>a.dll</Assembly>
                <AddInId>22222222-2222-2222-2222-222222222222</AddInId>
                <FullClassName>A.Cmd</FullClassName>
                <VisibilityMode>AlwaysVisible</VisibilityMode>
                <VisibilityMode>NotVisibleInFamily</VisibilityMode>
                <Discipline>Architecture</Discipline>
              </AddIn>
            </RevitAddIns>
            """;

        var entry = Assert.Single(_sut.Parse(xml).Entries);

        Assert.Equal(AddinEntryType.Command, entry.Type);
        Assert.Equal("Open log", entry.Text);
        Assert.True(entry.VisibilityModes.SequenceEqual(["AlwaysVisible", "NotVisibleInFamily"]));
        Assert.True(entry.Disciplines.SequenceEqual(["Architecture"]));
    }

    [Fact]
    public void Parse_ManifestSettings()
    {
        const string xml = """
            <RevitAddIns>
              <AddIn Type="Application">
                <Name>A</Name>
                <Assembly>a.dll</Assembly>
                <AddInId>33333333-3333-3333-3333-333333333333</AddInId>
                <FullClassName>A.App</FullClassName>
              </AddIn>
              <ManifestSettings>
                <UseRevitContext>False</UseRevitContext>
                <ContextName>MyCtx</ContextName>
              </ManifestSettings>
            </RevitAddIns>
            """;

        var settings = _sut.Parse(xml).Settings;

        Assert.NotNull(settings);
        Assert.False(settings.UseRevitContext);
        Assert.Equal("MyCtx", settings.ContextName);
    }

    [Fact]
    public void Parse_UnknownType_DoesNotThrow()
    {
        const string xml = """
            <RevitAddIns>
              <AddIn Type="SomethingNew">
                <Assembly>a.dll</Assembly>
                <AddInId>44444444-4444-4444-4444-444444444444</AddInId>
                <FullClassName>A.X</FullClassName>
              </AddIn>
            </RevitAddIns>
            """;

        var entry = Assert.Single(_sut.Parse(xml).Entries);

        Assert.Equal(AddinEntryType.Unknown, entry.Type);
        Assert.Equal("SomethingNew", entry.RawType);
    }

    [Fact]
    public void Parse_NotXml_ThrowsFormat()
    {
        Assert.Throws<AddinManifestFormatException>(() => _sut.Parse("this is not xml {{{"));
    }

    /// <summary>
    /// Сообщения об ошибках идут из resx под текущую культуру, а не зашиты: обе культуры
    /// проверяются явно, чтобы тест не зависел от языка ОС.
    /// </summary>
    [Theory]
    [InlineData("en", "Manifest XML is empty.")]
    [InlineData("ru", "Пустой XML манифеста.")]
    public void Parse_EmptyXml_ErrorMessageFollowsCurrentCulture(string culture, string expected)
    {
        string? message = null;
        TestCulture.RunIn(culture, () =>
        {
            try
            {
                _sut.Parse("   ");
            }
            catch (AddinManifestFormatException ex)
            {
                message = ex.Message;
            }
        });

        Assert.Equal(expected, message);
    }

    [Fact]
    public void Parse_MissingRequiredField_ThrowsFormat()
    {
        const string xml = """
            <RevitAddIns>
              <AddIn Type="Application">
                <Name>NoAssembly</Name>
                <AddInId>55555555-5555-5555-5555-555555555555</AddInId>
                <FullClassName>A.App</FullClassName>
              </AddIn>
            </RevitAddIns>
            """;

        Assert.Throws<AddinManifestFormatException>(() => _sut.Parse(xml));
    }

    [Fact]
    public void Parse_BadGuid_ThrowsFormat()
    {
        const string xml = """
            <RevitAddIns>
              <AddIn Type="Application">
                <Assembly>a.dll</Assembly>
                <AddInId>not-a-guid</AddInId>
                <FullClassName>A.App</FullClassName>
              </AddIn>
            </RevitAddIns>
            """;

        Assert.Throws<AddinManifestFormatException>(() => _sut.Parse(xml));
    }

    [Fact]
    public void Parse_DuplicateAddInId_ThrowsFormat()
    {
        const string xml = """
            <RevitAddIns>
              <AddIn Type="Application">
                <Assembly>a.dll</Assembly>
                <AddInId>77777777-7777-7777-7777-777777777777</AddInId>
                <FullClassName>A.App</FullClassName>
              </AddIn>
              <AddIn Type="Command">
                <Text>Dup</Text>
                <Assembly>b.dll</Assembly>
                <AddInId>77777777-7777-7777-7777-777777777777</AddInId>
                <FullClassName>B.Cmd</FullClassName>
              </AddIn>
            </RevitAddIns>
            """;

        Assert.Throws<AddinManifestFormatException>(() => _sut.Parse(xml));
    }

    [Fact]
    public void RoundTrip_PreservesDeclarationAndUnknowns()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-16" standalone="no"?>
            <!-- vendor header -->
            <RevitAddIns>
              <AddIn Type="Application">
                <Name>A</Name>
                <Assembly>a.dll</Assembly>
                <AddInId>66666666-6666-6666-6666-666666666666</AddInId>
                <FullClassName>A.App</FullClassName>
                <CustomTag>keep me</CustomTag>
              </AddIn>
            </RevitAddIns>
            """;

        // Декларация обязана быть первым символом (иначе это уже не декларация).
        var back = _sut.ToXml(_sut.Parse(xml.TrimStart()));

        Assert.Contains("encoding=\"utf-16\"", back);
        Assert.Contains("<!-- vendor header -->", back);
        Assert.Contains("<CustomTag>keep me</CustomTag>", back);
    }

    [Fact]
    public void RoundTrip_IsIdempotent()
    {
        var once = _sut.ToXml(_sut.Parse(ApplicationXml));
        var twice = _sut.ToXml(_sut.Parse(once));

        Assert.Equal(once, twice);
    }

    [Fact]
    public void ToXml_EscapesSpecialChars()
    {
        var manifest = _sut.Parse(ApplicationXml);
        var edited = manifest with
        {
            Entries = [manifest.Entries[0] with { VendorDescription = "A&B <C>" }],
        };

        Assert.Contains("A&amp;B &lt;C&gt;", _sut.ToXml(edited));
    }
}
