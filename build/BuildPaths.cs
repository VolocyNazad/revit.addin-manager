namespace Revit.AddinManager.Build;

internal static class BuildPaths
{
    public static string Root { get; } = FindRoot();
    public static string Solution => Path.Combine(Root, "Revit.AddinManager.slnx");
    public static string LauncherProject => Path.Combine(Root, "src", "AddinManager.Launcher", "AddinManager.Launcher.csproj");
    public static string InstallerProject => Path.Combine(Root, "installer", "Revit.AddinManager.Installer", "Revit.AddinManager.Installer.csproj");

    public static string GetLauncherOutput(string configuration)
    {
        string directory = Path.Combine(
            Root, "src", "AddinManager.Launcher", "bin", configuration, "net10.0-windows");

        if (!File.Exists(Path.Combine(directory, "AddinManager.Launcher.exe")))
            throw new DirectoryNotFoundException($"Launcher output was not found: '{directory}'.");

        return directory;
    }

    public static string GetInstallerExecutable() =>
        Path.Combine(Root, "installer", "Revit.AddinManager.Installer", "bin", "Release", "net10.0-windows", "Revit.AddinManager.Installer.exe");

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Revit.AddinManager.slnx")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find the Revit.AddinManager repository root.");
    }
}
