namespace AddinManager.Core.Storage;

/// <summary>
/// Filesystem layout of the Revit add-in folders and the disabled-folder rule, shared by the store,
/// the change watcher and the name normalization so one place owns the paths.
/// </summary>
internal static class AddinFolderLayout
{
    internal const string DisabledFolderName = "disabled";
    internal const string AddinFilePattern = "*.addin";
    internal const string AddinFileExtension = ".addin";

    /// <summary>Builds the version-independent add-ins root under a user or machine base directory.</summary>
    internal static string GetAddinsRoot(string baseDirectory) =>
        Path.Combine(baseDirectory, "Autodesk", "Revit", "Addins");

    /// <summary>Builds the Revit-version folder under a user or machine base directory.</summary>
    internal static string GetVersionRoot(string baseDirectory, string version) =>
        Path.Combine(GetAddinsRoot(baseDirectory), version);

    /// <summary>
    /// Returns the folder that holds the file for the requested state: the version folder itself for
    /// an enabled file, its <c>disabled/</c> child otherwise.
    /// </summary>
    internal static string GetFileFolder(string versionRoot, bool enabled) =>
        enabled ? versionRoot : Path.Combine(versionRoot, DisabledFolderName);
}
