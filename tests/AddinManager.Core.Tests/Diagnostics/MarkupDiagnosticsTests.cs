using AddinManager.Core.Diagnostics;

namespace AddinManager.Core.Tests.Diagnostics;

/// <summary>
/// Headless-диагностика разметки: срезы пустых полей, неизвестного типа, дублей <c>AddInId</c>
/// (в файле и в других файлах версии) и блока выбранной записи.
/// </summary>
public sealed class MarkupDiagnosticsTests
{
    private static readonly Guid FirstId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FindDiagnosticSpans_EmptyXml_ReturnsEmpty(string? xml)
    {
        Assert.Empty(MarkupDiagnostics.FindDiagnosticSpans(xml));
    }

    [Fact]
    public void FindDiagnosticSpans_ValidManifest_ReturnsEmpty()
    {
        Assert.Empty(MarkupDiagnostics.FindDiagnosticSpans(TestManifestXml.ValidAddin("Original")));
    }

    [Fact]
    public void FindDiagnosticSpans_EmptyRequiredFields_AreMarked()
    {
        const string xml = """
            <RevitAddIns>
              <AddIn Type="Application">
                <Name>Broken</Name>
                <Assembly></Assembly>
                <AddInId />
                <FullClassName>   </FullClassName>
              </AddIn>
            </RevitAddIns>
            """;

        IReadOnlyList<MarkupSpan> spans = MarkupDiagnostics.FindDiagnosticSpans(xml);

        Assert.Equal(3, spans.Count);
        var marked = spans.Select(span => xml.Substring(span.Start, span.Length)).ToList();
        Assert.Contains(marked, text => text.Contains("<Assembly>", StringComparison.Ordinal));
        Assert.Contains(marked, text => text.Contains("<AddInId", StringComparison.Ordinal));
        Assert.Contains(marked, text => text.Contains("<FullClassName>", StringComparison.Ordinal));
    }

    [Fact]
    public void FindDiagnosticSpans_UnknownType_IsMarked()
    {
        const string xml = """
            <RevitAddIns>
              <AddIn Type="SomethingNew">
                <Name>Unknown</Name>
                <Assembly>a.dll</Assembly>
                <AddInId>33333333-3333-3333-3333-333333333333</AddInId>
                <FullClassName>A.Unknown</FullClassName>
              </AddIn>
            </RevitAddIns>
            """;

        var span = Assert.Single(MarkupDiagnostics.FindDiagnosticSpans(xml));

        Assert.Equal("SomethingNew", xml.Substring(span.Start, span.Length));
    }

    [Fact]
    public void FindDiagnosticSpans_DuplicateIdInsideFile_AllOccurrencesMarked()
    {
        var xml = TwoEntriesXml(FirstId, FirstId);

        IReadOnlyList<MarkupSpan> spans = MarkupDiagnostics.FindDiagnosticSpans(xml);

        Assert.Equal(2, spans.Count);
        Assert.All(
            spans,
            span => Assert.Contains("11111111-1111-1111-1111-111111111111", xml.Substring(span.Start, span.Length), StringComparison.Ordinal));
    }

    [Fact]
    public void FindDiagnosticSpans_ExternalDuplicate_IsMarked()
    {
        IReadOnlyList<MarkupSpan> spans = MarkupDiagnostics.FindDiagnosticSpans(
            TestManifestXml.ValidAddin("Original"), new HashSet<Guid>([FirstId]));

        var span = Assert.Single(spans);
        Assert.Contains("11111111-1111-1111-1111-111111111111", TestManifestXml.ValidAddin("Original").Substring(span.Start, span.Length), StringComparison.Ordinal);
    }

    [Fact]
    public void FindDiagnosticSpans_IdAlreadyDuplicatedInFile_ExternalDoesNotDoubleMark()
    {
        const string xml = """
            <RevitAddIns>
              <AddIn Type="Application">
                <Name>First</Name>
                <Assembly>a.dll</Assembly>
                <AddInId>11111111-1111-1111-1111-111111111111</AddInId>
                <FullClassName>A.First</FullClassName>
              </AddIn>
              <AddIn Type="Application">
                <Name>Second</Name>
                <Assembly>b.dll</Assembly>
                <AddInId>11111111-1111-1111-1111-111111111111</AddInId>
                <FullClassName>A.Second</FullClassName>
              </AddIn>
            </RevitAddIns>
            """;

        IReadOnlyList<MarkupSpan> spans = MarkupDiagnostics.FindDiagnosticSpans(xml, new HashSet<Guid>([FirstId]));

        Assert.Equal(2, spans.Count);
    }

    [Fact]
    public void FindEntrySpan_ReturnsWholeEntryBlock()
    {
        var xml = TwoEntriesXml(FirstId, SecondId);

        var span = MarkupDiagnostics.FindEntrySpan(xml, SecondId);

        Assert.NotNull(span);
        var text = xml.Substring(span.Value.Start, span.Value.Length);
        Assert.StartsWith("<AddIn Type=\"Command\"", text, StringComparison.Ordinal);
        Assert.EndsWith("</AddIn>", text.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void FindEntrySpan_UnknownId_ReturnsNull()
    {
        Assert.Null(MarkupDiagnostics.FindEntrySpan(TwoEntriesXml(FirstId, SecondId), Guid.NewGuid()));
    }

    [Fact]
    public void FindEntrySpan_EmptyXml_ReturnsNull()
    {
        Assert.Null(MarkupDiagnostics.FindEntrySpan(null, FirstId));
    }

    private static string TwoEntriesXml(Guid firstId, Guid secondId) => $"""
        <RevitAddIns>
          <AddIn Type="Application">
            <Name>First</Name>
            <Assembly>a.dll</Assembly>
            <AddInId>{firstId}</AddInId>
            <FullClassName>A.First</FullClassName>
          </AddIn>
          <AddIn Type="Command">
            <Text>Second</Text>
            <Assembly>b.dll</Assembly>
            <AddInId>{secondId}</AddInId>
            <FullClassName>A.Second</FullClassName>
          </AddIn>
        </RevitAddIns>
        """;
}
