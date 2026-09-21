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
            new Files(Path.Combine(sourceDirectory, "*.*"))),
        new Dir($@"%ProgramMenuFolder%\{Vendor}",
            new ExeFileShortcut("Revit Addin Manager", @"[INSTALLDIR]AddinManager.Launcher.exe", string.Empty)),
        new Dir(@"%DesktopFolder%",
            new ExeFileShortcut("Revit Addin Manager", @"[INSTALLDIR]AddinManager.Launcher.exe", string.Empty)),
        new Dir(@"%AppDataFolder%\Microsoft\Internet Explorer\Quick Launch",
            new ExeFileShortcut("Revit Addin Manager", @"[INSTALLDIR]AddinManager.Launcher.exe", string.Empty)),
    ],
};

project.RemoveDialogsBetween(NativeDialogs.WelcomeDlg, NativeDialogs.InstallDirDlg);
project.BuildMsi();
return 0;

static string GenerateProductGuid(string productName, Version version)
{
    string input = $"{productName}-{version.Major}.{version.Minor}.{version.Build}";
    byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
    return new Guid(hash).ToString();
}
