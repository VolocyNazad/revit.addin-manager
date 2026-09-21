using System.IO;
using AddinManager.Launcher.Services;
using AddinManager.Theming;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Кнопка проверки обновлений (<see cref="MainViewModel.CheckForUpdatesCommand"/>): есть
/// обновление — диалог с ссылкой, актуально — тост, ошибка проверки — тост с причиной.
/// </summary>
public sealed class MainViewModelUpdateTests
{
    [Fact]
    public void CheckForUpdates_HasUpdate_ShowsDialogWithMessageAndUrl()
    {
        var dialog = new FakeDialogService();
        var checker = new FakeUpdateChecker
        {
            Result = new UpdateCheckResult(true, "1.2.3", "https://example.com/releases", "1.0.0.0", null),
        };
        var model = NewModel(dialog, checker);

        TestCulture.RunIn("en", () =>
        {
            model.CheckForUpdatesCommand.ExecuteAsync(null).GetAwaiter().GetResult();

            Assert.Equal(["Version 1.2.3 is available (you have 1.0.0.0)."], dialog.ShownMessages);
            Assert.Equal(["https://example.com/releases"], dialog.UpdateUrls);
            Assert.False(model.IsToastVisible);
        });
    }

    [Fact]
    public void CheckForUpdates_UpToDate_ShowsToast()
    {
        var dialog = new FakeDialogService();
        var checker = new FakeUpdateChecker
        {
            Result = new UpdateCheckResult(false, "1.0.0.0", null, "1.0.0.0", null),
        };
        var model = NewModel(dialog, checker);

        TestCulture.RunIn("en", () =>
        {
            model.CheckForUpdatesCommand.ExecuteAsync(null).GetAwaiter().GetResult();

            Assert.True(model.IsToastVisible);
            Assert.Equal("You are up to date", model.ToastMessage);
            Assert.Empty(dialog.ShownMessages);
        });
    }

    [Fact]
    public void CheckForUpdates_Error_ShowsErrorToast()
    {
        var dialog = new FakeDialogService();
        var checker = new FakeUpdateChecker
        {
            Result = new UpdateCheckResult(false, null, null, "1.0.0.0", "boom"),
        };
        var model = NewModel(dialog, checker);

        TestCulture.RunIn("en", () =>
        {
            model.CheckForUpdatesCommand.ExecuteAsync(null).GetAwaiter().GetResult();

            Assert.True(model.IsToastVisible);
            Assert.Equal("Couldn't check for updates: boom", model.ToastMessage);
            Assert.Empty(dialog.ShownMessages);
        });
    }

    private static MainViewModel NewModel(FakeDialogService dialog, FakeUpdateChecker checker) => new(
        new ThemeService(static () => false, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "theme.txt")),
        new FakeLocalizationService(),
        TestLocalization.For<MainViewModel>(),
        new RecordingUiDispatcher(),
        new FakeRevitProcessGuard(),
        new FakeToastService(),
        dialog,
        checker,
        new FakeUrlOpener());
}
