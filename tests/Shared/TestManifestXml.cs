namespace AddinManager.Testing;

/// <summary>
/// Manifest XML builders shared by the headless tests. One definition keeps every test on the same
/// valid shape instead of ten near-identical local copies.
/// </summary>
public static class TestManifestXml
{
    /// <summary>Builds one valid <c>Application</c> entry with the given display name.</summary>
    /// <param name="name">The <c>Name</c> element value.</param>
    public static string ValidAddin(string name) => $"""
        <RevitAddIns>
          <AddIn Type="Application">
            <Name>{name}</Name>
            <Assembly>a.dll</Assembly>
            <AddInId>11111111-1111-1111-1111-111111111111</AddInId>
            <FullClassName>A.App</FullClassName>
          </AddIn>
        </RevitAddIns>
        """;
}
