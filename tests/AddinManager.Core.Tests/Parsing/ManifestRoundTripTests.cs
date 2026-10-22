using System.Xml.Linq;
using AddinManager.Core.Manifests;
using AddinManager.Core.Parsing;

namespace AddinManager.Core.Tests.Parsing;

/// <summary>
/// Честный round-trip после правок: порядок детей корня, комментарии/чужие узлы и незнакомые
/// атрибуты <c>RevitAddIns</c>/<c>AddIn</c>/<c>ManifestSettings</c> не теряются при сохранении.
/// </summary>
public sealed class ManifestRoundTripTests
{
    private readonly LinqToXmlAddinManifestParser _parser =
        new(TestLocalization.For<LinqToXmlAddinManifestParser>());

    [Fact]
    public void ToXml_PreservesRootChildOrderAndUnknownAttributes()
    {
        var manifest = _parser.Parse(RootWithUnknownsXml());

        XElement root = XDocument.Parse(_parser.ToXml(manifest)).Root!;

        Assert.Equal("2", (string?)root.Attribute("Version"));
        Assert.Equal("on", (string?)root.Attribute(XNamespace.Get("urn:x") + "Flag"));
        Assert.Equal(
            ["AddIn", "ManifestSettings", "AddIn", "UnknownTag"],
            root.Elements().Select(element => element.Name.LocalName));
        Assert.Equal(
            ["Application", "Command"],
            root.Elements("AddIn").Select(element => (string?)element.Attribute("Type")));
        Assert.Equal("second", (string?)root.Elements("AddIn").Last().Attribute("Custom"));
        Assert.Equal("legacy", (string?)root.Element("ManifestSettings")?.Attribute("Mode"));
        Assert.Equal("keep", root.Element("UnknownTag")?.Value);
        Assert.NotNull(root.Nodes().OfType<XComment>().SingleOrDefault());
    }

    [Fact]
    public void ParseThenToXml_ParsedAgain_KeepsEntriesSettingsAndUnknownOrder()
    {
        var manifest = _parser.Parse(RootWithUnknownsXml());

        AddinManifest reparsed = _parser.Parse(_parser.ToXml(manifest));

        Assert.Equal(2, reparsed.Entries.Count);
        Assert.NotNull(reparsed.Settings);
        Assert.Equal(
            [ManifestRootChild.Entry, ManifestRootChild.Unknown, ManifestRootChild.Settings, ManifestRootChild.Entry, ManifestRootChild.Unknown],
            reparsed.RootOrder);
        Assert.Equal("2", (string?)reparsed.RootAttributes?.Single(attribute => attribute.Name.LocalName == "Version"));
    }

    [Fact]
    public void ToXml_NewEntry_IsAppendedAfterParsedOrder()
    {
        var manifest = _parser.Parse(TestManifestXml.ValidAddin("First"));
        var added = manifest.Entries[0] with { Name = "Second" };
        var updated = manifest with { Entries = [.. manifest.Entries, added] };

        XElement root = XDocument.Parse(_parser.ToXml(updated)).Root!;

        Assert.Equal(
            ["First", "Second"],
            root.Elements("AddIn").Select(element => element.Element("Name")?.Value));
    }

    private static string RootWithUnknownsXml() => """
        <?xml version="1.0" encoding="utf-8"?>
        <RevitAddIns Version="2" xmlns:x="urn:x" x:Flag="on">
          <AddIn Type="Application" Id="first">
            <Name>First</Name>
            <Assembly>a.dll</Assembly>
            <AddInId>11111111-1111-1111-1111-111111111111</AddInId>
            <FullClassName>A.First</FullClassName>
          </AddIn>
          <!-- between entries and settings -->
          <ManifestSettings Mode="legacy">
            <UseRevitContext>true</UseRevitContext>
          </ManifestSettings>
          <AddIn Type="Command" Custom="second">
            <Text>Second</Text>
            <Assembly>b.dll</Assembly>
            <AddInId>22222222-2222-2222-2222-222222222222</AddInId>
            <FullClassName>A.Second</FullClassName>
          </AddIn>
          <UnknownTag>keep</UnknownTag>
        </RevitAddIns>
        """;
}
