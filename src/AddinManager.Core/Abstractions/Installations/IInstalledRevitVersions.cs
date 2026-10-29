namespace AddinManager.Core.Abstractions.Installations;

/// <summary>
/// The Revit versions installed on this machine. UI filters and version pickers show only these,
/// so a version that is not installed does not occupy a chip. The probe reports supported versions
/// only (see <see cref="RevitVersionCatalog"/>).
/// </summary>
public interface IInstalledRevitVersions
{
    /// <summary>
    /// Returns the installed supported versions, oldest first. When the installation probe is
    /// unavailable or finds nothing, every supported version is returned so the UI stays usable;
    /// the fallback is logged.
    /// </summary>
    IReadOnlyList<string> GetVersions();
}
