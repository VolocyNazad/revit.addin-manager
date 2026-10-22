namespace AddinManager.Core;

/// <summary>
/// The Revit versions this application supports. One owner for the version list and for the default
/// version, so the list is not restated across the launcher.
/// </summary>
public static class RevitVersionCatalog
{
    /// <summary>Supported version identifiers, oldest first (2021–2027).</summary>
    public static IReadOnlyList<string> SupportedVersions { get; } =
        ["2021", "2022", "2023", "2024", "2025", "2026", "2027"];

    /// <summary>The newest supported version, used as the default in the version pickers.</summary>
    public static string Latest => SupportedVersions[^1];
}
