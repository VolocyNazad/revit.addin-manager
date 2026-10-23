namespace AddinManager.Core.Abstractions;

/// <summary>
/// Single source of product-wide identity: vendor, product and repository names, GitHub
/// destinations, MSI asset naming and Windows synchronization names.
/// </summary>
/// <remarks>
/// Path composition stays with the owning module (the launcher builds <c>%APPDATA%</c> and
/// <c>%LOCALAPPDATA%</c> paths, the installer builds Program Files paths); this type holds only
/// the shared segments and names. The file is source-linked into the installer and the build
/// pipeline, which are separate build units and do not reference Core.
/// </remarks>
public static class ProductIdentity
{
    /// <summary>Gets the per-user data folder segment below <c>LocalApplicationData</c> and <c>ApplicationData</c>.</summary>
    public const string CompanyDirectoryName = "Volocy";

    /// <summary>Gets the product folder segment below the company folder.</summary>
    public const string ProductDirectoryName = "Revit.AddinManager";

    /// <summary>Gets the product name used for the MSI and Programs and Features.</summary>
    public const string ProductName = "Revit.AddinManager";

    /// <summary>Gets the human-readable name used by shortcuts and the main window.</summary>
    public const string DisplayName = "Revit Addin Manager";

    /// <summary>Gets the vendor used for the per-machine installation directory and MSI manufacturer.</summary>
    public const string InstallVendor = "VolocyNazad";

    /// <summary>Gets the launcher assembly name.</summary>
    public const string LauncherAssemblyName = "AddinManager.Launcher";

    /// <summary>Gets the launcher executable file name.</summary>
    public const string LauncherExecutableName = "AddinManager.Launcher.exe";

    /// <summary>Gets the installer generator assembly name.</summary>
    public const string InstallerAssemblyName = "Revit.AddinManager.Installer";

    /// <summary>Gets the installer generator executable file name.</summary>
    public const string InstallerExecutableName = "Revit.AddinManager.Installer.exe";

    /// <summary>Gets the launcher log folder name below the product data folder.</summary>
    public const string LogsFolderName = "logs";

    /// <summary>Gets the rolling launcher log file name.</summary>
    public const string LauncherLogFileName = "launcher-.log";

    /// <summary>Gets the language preference file name below the product settings folder.</summary>
    public const string LanguageFileName = "language.txt";

    /// <summary>Gets the theme preference file name below the product settings folder.</summary>
    public const string ThemeFileName = "theme.txt";

    /// <summary>Gets the MSI release asset file name prefix.</summary>
    public const string MsiAssetPrefix = "RevitAddinManager-";

    /// <summary>Gets the MSI release asset file extension.</summary>
    public const string MsiAssetExtension = ".msi";

    /// <summary>Gets the single-instance mutex name qualified with the local Windows session.</summary>
    public const string MutexName = @"Local\Volocy.Revit.AddinManager.SingleInstance";

    /// <summary>Gets the activation event name used to restore the first instance.</summary>
    public const string ActivationEventName = @"Local\Volocy.Revit.AddinManager.ShowFirstInstance";

    /// <summary>Gets the GitHub organization that owns the repository.</summary>
    public const string GitHubOrganization = "VolocyNazad";

    /// <summary>Gets the GitHub repository name.</summary>
    public const string GitHubRepository = "revit.addin-manager";

    /// <summary>Gets the public GitHub host.</summary>
    public const string GitHubHost = "github.com";

    /// <summary>Gets the absolute HTTPS address of the GitHub repository.</summary>
    public static string RepositoryUrl => $"https://{GitHubHost}/{GitHubOrganization}/{GitHubRepository}";

    /// <summary>Gets the absolute HTTPS address of the issue tracker.</summary>
    public static string SupportUrl => $"{RepositoryUrl}/issues";

    /// <summary>Gets the absolute HTTPS address of the author's sponsorship page.</summary>
    public static string SponsorUrl => $"https://{GitHubHost}/sponsors/{GitHubOrganization}";

    /// <summary>Gets the absolute HTTPS address of the releases page.</summary>
    public static string ReleasesUrl => $"{RepositoryUrl}/releases";

    /// <summary>Gets the GitHub API endpoint for the latest stable release.</summary>
    public static string LatestReleaseApiUrl =>
        $"https://api.{GitHubHost}/repos/{GitHubOrganization}/{GitHubRepository}/releases/latest";

    /// <summary>Builds the MSI base file name (without the extension) for a stable version.</summary>
    /// <param name="version">The stable version text, for example <c>1.5.0</c>.</param>
    /// <returns>The file name prefix and version.</returns>
    public static string GetMsiBaseName(string version) => $"{MsiAssetPrefix}{version}";

    /// <summary>Builds the MSI release asset file name for a stable version.</summary>
    /// <param name="version">The stable version text, for example <c>1.5.0</c>.</param>
    /// <returns>The exact release asset name, including the extension.</returns>
    public static string GetMsiAssetName(string version) => $"{GetMsiBaseName(version)}{MsiAssetExtension}";
}
