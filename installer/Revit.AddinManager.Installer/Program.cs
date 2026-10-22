using System.Security.Cryptography;
using System.Text;
using WixSharp;
using WixSharp.CommonTasks;
using WixSharp.Controls;

const string ProductName = "Revit.AddinManager";
const string Vendor = "VolocyNazad";

// Stable product family id: never change, otherwise MajorUpgrade stops replacing old installs.
const string UpgradeCode = "e71ca8ca-d5e5-4a8a-a692-06a1fb708156";

if (args.Length != 3)
{
    Console.Error.WriteLine(
        "Usage: Revit.AddinManager.Installer <product-version> <source-directory> <output-directory>");
    return 1;
}

if (!Version.TryParse(args[0], out Version? version))
{
    Console.Error.WriteLine($"Invalid product version: '{args[0]}'. Expected format: major.minor.patch.");
    return 1;
}

string sourceDirectory = Path.GetFullPath(args[1]);
string outputDirectory = Path.GetFullPath(args[2]);

if (!Directory.Exists(sourceDirectory))
{
    Console.Error.WriteLine($"Source directory does not exist: '{sourceDirectory}'.");
    return 1;
}

Directory.CreateDirectory(outputDirectory);

string productGuid = GenerateProductGuid(ProductName, version);

// В Program Files ставим только рантайм: символы отладки и XML-документация не нужны
// пользователю и раздувают установку. Собираем отфильтрованную копию во временной папке,
// чтобы не зависеть от того, как именно был собран источник.
string stagingDirectory = Path.Combine(Path.GetTempPath(), $"Revit.AddinManager-install-{Guid.NewGuid():N}");
try
{
    CopyRuntimeFiles(sourceDirectory, stagingDirectory);

    Project project = new()
    {
        MajorUpgrade = MajorUpgrade.Default,
        UpgradeCode = new Guid(UpgradeCode),
        GUID = new Guid(productGuid),
        Version = version,
        Name = ProductName,
        OutDir = outputDirectory,
        OutFileName = $"RevitAddinManager-{version}",
        ControlPanelInfo =
        {
            Name = ProductName,
            Manufacturer = Vendor,
            Comments = "Pre-launch manager for Autodesk Revit .addin manifests.",
            HelpLink = "https://github.com/VolocyNazad/revit.addin-manager",
        },
        Platform = WixSharp.Platform.x64,
        UI = WUI.WixUI_InstallDir,
        Scope = InstallScope.perMachine,
        Dirs =
        [
            new InstallDir($@"%ProgramFiles64Folder%\{Vendor}\{ProductName}",
                new Files(Path.Combine(stagingDirectory, "*.*"))),
            new Dir($@"%ProgramMenuFolder%\{Vendor}",
                new ExeFileShortcut("Revit Addin Manager", @"[INSTALLDIR]AddinManager.Launcher.exe", string.Empty)),
            new Dir(@"%DesktopFolder%",
                new ExeFileShortcut("Revit Addin Manager", @"[INSTALLDIR]AddinManager.Launcher.exe", string.Empty)),
        ],
    };

    project.RemoveDialogsBetween(NativeDialogs.WelcomeDlg, NativeDialogs.InstallDirDlg);
    project.BuildMsi();
}
finally
{
    if (Directory.Exists(stagingDirectory))
        Directory.Delete(stagingDirectory, recursive: true);
}

return 0;

static void CopyRuntimeFiles(string sourceDirectory, string targetDirectory)
{
    foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
    {
        if (file.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        string destination = Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, file));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        System.IO.File.Copy(file, destination);
    }
}

static string GenerateProductGuid(string productName, Version version)
{
    string input = $"{productName}-{version.Major}.{version.Minor}.{version.Build}";
    byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
    return new Guid(hash).ToString();
}
