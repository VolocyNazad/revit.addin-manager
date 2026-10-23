using AddinManager.Core.Abstractions;

namespace Revit.AddinManager.Build;

internal static class BuildPaths
{
    public const string ReleaseConfiguration = "Release";
    public const string TargetFramework = "net10.0-windows";

    public static string Root { get; } = FindRoot();
    public static string Changelog => Path.Combine(Root, "CHANGELOG.md");
    public static string LauncherProject => Path.Combine(Root, "src", ProductIdentity.LauncherAssemblyName, $"{ProductIdentity.LauncherAssemblyName}.csproj");
    public static string InstallerProject => Path.Combine(Root, "installer", ProductIdentity.InstallerAssemblyName, $"{ProductIdentity.InstallerAssemblyName}.csproj");

    /// <summary>Каталог собранного лаунчера, откуда инсталлятор забирает рантайм-файлы.</summary>
    public static string GetLauncherOutput()
    {
        string directory = Path.Combine(
            Root, "src", ProductIdentity.LauncherAssemblyName, "bin", ReleaseConfiguration, TargetFramework);

        if (!File.Exists(Path.Combine(directory, ProductIdentity.LauncherExecutableName)))
            throw new DirectoryNotFoundException($"Launcher output was not found: '{directory}'.");

        return directory;
    }

    /// <summary>Собранный генератор MSI (всегда из Release-конфигурации).</summary>
    public static string GetInstallerExecutable()
    {
        string executable = Path.Combine(
            Root, "installer", ProductIdentity.InstallerAssemblyName, "bin", ReleaseConfiguration, TargetFramework, ProductIdentity.InstallerExecutableName);

        if (!File.Exists(executable))
            throw new FileNotFoundException($"Installer generator was not found: '{executable}'.", executable);

        return executable;
    }

    /// <summary>Путь единственного MSI продукта в каталоге вывода.</summary>
    public static string MsiPath(string outputDirectory, string version) =>
        Path.Combine(outputDirectory, ProductIdentity.GetMsiAssetName(version));

    /// <summary>Путь заметок релиза, которые публикует GitHub.</summary>
    public static string ReleaseNotesPath(string outputDirectory, string version) =>
        Path.Combine(outputDirectory, $"release-notes-{version}.md");

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Revit.AddinManager.slnx")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find the Revit.AddinManager repository root.");
    }
}
