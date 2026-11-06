using AddinManager.Launcher.ViewModels.Dialogs;
using AddinManager.Core;
using AddinManager.Core.Storage;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Диалог нового файла: пустое/битое имя — баннер без закрытия, годное — результат,
/// отмена — отрицательный результат. Значения по умолчанию — New manifest/User/2027/включён.
/// </summary>
public sealed class AddFileDialogViewModelTests
{
    [Fact]
    public void Defaults_UserLatestEnabled()
    {
        AddFileDialogViewModel model = null!;
        TestCulture.RunIn("en", () => model = NewModel());

        Assert.Equal("New manifest", model.FileName);
        Assert.Equal(AddinScope.User, model.Scope);
        Assert.Equal("2027", model.Version);
        Assert.False(model.IsDisabled);
        Assert.Null(model.Result);
    }

    [Fact]
    public void VersionChips_TrackSelectedVersion()
    {
        var model = NewModel();

        Assert.Equal(RevitVersionCatalog.SupportedVersions.Count, model.VersionChips.Count);
        Assert.True(model.VersionChips.Single(chip => chip.Value == "2027").IsActive);

        model.SetVersionCommand.Execute("2024");

        Assert.False(model.VersionChips.Single(chip => chip.Value == "2027").IsActive);
        Assert.True(model.VersionChips.Single(chip => chip.Value == "2024").IsActive);
    }

    [Fact]
    public void VersionChips_ShowOnlyInstalledVersions_AndDefaultToNewestInstalled()
    {
        AddFileDialogViewModel model = null!;
        TestCulture.RunIn("en", () => model = NewModel(new FakeInstalledRevitVersions("2021", "2025")));

        Assert.Equal(["2021", "2025"], model.VersionChips.Select(chip => chip.Value));
        Assert.Equal("2025", model.Version);
        Assert.True(model.VersionChips.Single(chip => chip.Value == "2025").IsActive);
    }

    [Fact]
    public void Add_EmptyName_ShowsErrorAndStaysOpen()
    {
        var model = NewModel();
        model.FileName = "   ";

        TestCulture.RunIn("ru", () =>
        {
            model.AddCommand.Execute(null);

            Assert.Equal("Введите имя файла.", model.ErrorMessage);
        });
        Assert.Null(model.Result);
    }

    [Fact]
    public void Add_BadChars_ShowsErrorAndStaysOpen()
    {
        var model = NewModel();
        model.FileName = "a/b";

        model.AddCommand.Execute(null);

        Assert.NotNull(model.ErrorMessage);
        Assert.Null(model.Result);
    }

    [Fact]
    public void Add_ValidName_SetsResult()
    {
        var model = NewModel();
        model.FileName = "Fresh";
        model.SetScopeCommand.Execute(AddinScope.Machine);
        model.SetVersionCommand.Execute("2024");

        model.AddCommand.Execute(null);

        Assert.True(model.Result);
        Assert.Null(model.ErrorMessage);
    }

    [Fact]
    public void Cancel_SetsFalse()
    {
        var model = NewModel();

        model.CancelCommand.Execute(null);

        Assert.False(model.Result);
    }

    private static AddFileDialogViewModel NewModel(FakeInstalledRevitVersions? installedVersions = null) =>
        new(
            TestLocalization.For<AddFileDialogViewModel>(),
            installedVersions ?? new FakeInstalledRevitVersions());
}
