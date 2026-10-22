using AddinManager.Launcher.ViewModels.Dialogs;
using AddinManager.Core.Manifests;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Services;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Диалог цели move/copy: значения по умолчанию, путь, конфликт, замена, предупреждения
/// о версии/ManifestSettings и состояние enabled.
/// </summary>
public sealed class MoveCopyDialogViewModelTests
{
    [Fact]
    public void Initialize_DefaultsToSourceFolder_AndDisablesConfirm()
    {
        var vm = NewDialog();

        vm.Initialize(new MoveCopyRequest(NewFile("A.addin", "2025"), MoveCopyKind.Move));

        Assert.Equal("2025", vm.Version);
        Assert.Equal(AddinScope.User, vm.Scope);
        Assert.True(vm.EnableAfter);
        Assert.True(vm.IsSameFolder);
        Assert.False(vm.CanConfirm);
        Assert.NotNull(vm.SameFolderError);
        TestCulture.RunIn("ru", () => Assert.Equal("Переместить файл", vm.Title));
        TestCulture.RunIn("en", () => Assert.Equal("Move file", vm.Title));
    }

    [Fact]
    public void VersionChips_TrackSelectedVersion()
    {
        var vm = NewDialog();
        vm.Initialize(new MoveCopyRequest(NewFile("A.addin", "2025"), MoveCopyKind.Move));

        Assert.True(vm.VersionChips.Single(chip => chip.Value == "2025").IsActive);
        Assert.False(vm.VersionChips.Single(chip => chip.Value == "2026").IsActive);

        vm.Version = "2026";

        Assert.True(vm.VersionChips.Single(chip => chip.Value == "2026").IsActive);
        Assert.False(vm.VersionChips.Single(chip => chip.Value == "2025").IsActive);
    }

    [Fact]
    public void ChangingTarget_UpdatesPathAndWarnsAboutCrossVersion()
    {
        var file = NewFile("A.addin", "2025");
        var vm = NewDialog();
        vm.Initialize(new MoveCopyRequest(file, MoveCopyKind.Move));

        vm.Version = "2026";
        vm.Scope = AddinScope.Machine;

        Assert.Equal(@"C:\fake\2026\A.addin", vm.TargetPath);
        Assert.False(vm.IsSameFolder);
        Assert.True(vm.CanConfirm);
        Assert.NotNull(vm.CrossVersionWarning);
        Assert.True(vm.HasWarnings);
    }

    [Fact]
    public void TargetConflict_RequiresExplicitOverwrite()
    {
        var store = new FakeAddinStore();
        var file = NewFile("A.addin", "2025");
        store.SetFiles("2026", NewFileWith(store, "A.addin", "2026", AddinScope.Machine));
        var vm = NewDialog(store);
        vm.Initialize(new MoveCopyRequest(file, MoveCopyKind.Copy));
        vm.Version = "2026";
        vm.Scope = AddinScope.Machine;

        Assert.True(vm.TargetExists);
        Assert.True(vm.ShowConflict);
        Assert.False(vm.CanConfirm);
        Assert.NotNull(vm.ConflictWarning);

        vm.Overwrite = true;

        Assert.True(vm.CanConfirm);
    }

    [Fact]
    public void SettingsWarning_WhenTargetVersionDoesNotSupportManifestSettings()
    {
        var file = NewFileWithSettings("2026");
        var vm = NewDialog();
        vm.Initialize(new MoveCopyRequest(file, MoveCopyKind.Copy));
        vm.Version = "2025";

        Assert.NotNull(vm.SettingsWarning);
        Assert.True(vm.HasWarnings);
    }

    [Fact]
    public void EnableAfterFalse_UsesDisabledPath()
    {
        var file = NewFile("A.addin", "2025");
        var vm = NewDialog();
        vm.Initialize(new MoveCopyRequest(file, MoveCopyKind.Copy));
        vm.Version = "2026";
        vm.EnableAfter = false;

        Assert.Contains("disabled", vm.TargetPath);
    }

    [Fact]
    public void CancelAndConfirm_SetResult()
    {
        var file = NewFile("A.addin", "2025");
        var vm = NewDialog();
        vm.Initialize(new MoveCopyRequest(file, MoveCopyKind.Copy));
        vm.Version = "2026";

        vm.ConfirmCommand.Execute(null);
        Assert.True(vm.Result);

        vm.Result = null;
        vm.CancelCommand.Execute(null);
        Assert.False(vm.Result);
    }

    private static MoveCopyDialogViewModel NewDialog(FakeAddinStore? store = null) =>
        new(
            store ?? new FakeAddinStore(),
            new RevitManifestSchema(),
            TestLocalization.For<MoveCopyDialogViewModel>());

    private static AddinFile NewFile(string fileName, string version) =>
        new(fileName, AddinScope.User, version, Enabled: true,
            FullPath: $@"C:\fake\{version}\{fileName}",
            VersionRootDirectory: $@"C:\fake\{version}",
            Manifest: EmptyManifest);

    private static AddinFile NewFileWithSettings(string version) =>
        NewFile("A.addin", version) with
        {
            Manifest = new AddinManifest([], new ManifestSettings(null, null, [], []), null, null, null, [], [], []),
        };

    private static AddinFile NewFileWith(FakeAddinStore store, string fileName, string version, AddinScope scope)
    {
        var path = store.GetTargetPath(fileName, version, scope, enabled: true);
        return new AddinFile(fileName, scope, version, Enabled: true,
            FullPath: path,
            VersionRootDirectory: $@"C:\fake\{version}",
            Manifest: EmptyManifest);
    }

    private static AddinManifest EmptyManifest => new([], null, null, null, null, [], [], []);
}
