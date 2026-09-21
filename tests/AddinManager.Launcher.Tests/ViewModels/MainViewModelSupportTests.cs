using System.IO;
using AddinManager.Theming;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>Кнопки поддержки и спонсорства открывают страницы GitHub (issues и sponsors).</summary>
public sealed class MainViewModelSupportTests
{
    [Fact]
    public void OpenSupport_OpensIssuesPage()
    {
        var opener = new FakeUrlOpener();
        var model = NewModel(opener);

        model.OpenSupportCommand.Execute(null);

        Assert.Equal(["https://github.com/VolocyNazad/revit.addin-manager/issues"], opener.OpenedUrls);
    }

    [Fact]
    public void OpenSponsor_OpensSponsorshipPage()
    {
        var opener = new FakeUrlOpener();
        var model = NewModel(opener);

        model.OpenSponsorCommand.Execute(null);

        Assert.Equal(["https://github.com/sponsors/VolocyNazad"], opener.OpenedUrls);
    }

    private static MainViewModel NewModel(FakeUrlOpener opener) => new(
        new ThemeService(static () => false, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "theme.txt")),
        new FakeLocalizationService(),
        TestLocalization.For<MainViewModel>(),
        new RecordingUiDispatcher(),
        new FakeRevitProcessGuard(),
        new FakeToastService(),
        new FakeDialogService(),
        new FakeUpdateChecker(),
        opener);
}
