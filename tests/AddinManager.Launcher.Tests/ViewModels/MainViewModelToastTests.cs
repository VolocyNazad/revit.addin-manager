using System.IO;
using AddinManager.Localization;
using AddinManager.Theming;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Тост-подтверждение смены темы/языка (<see cref="MainViewModel.ToastMessage"/>/
/// <see cref="MainViewModel.IsToastVisible"/>): текст — тот же, что в тултипе кнопки, культуру
/// выставляет сам тест. Автоскрытие по таймеру не тестируем (время), только появление и смену.
/// </summary>
public sealed class MainViewModelToastTests
{
    [Fact]
    public void Initial_NoToast()
    {
        var model = NewModel();

        Assert.False(model.IsToastVisible);
        Assert.Null(model.ToastMessage);
    }

    [Fact]
    public void CycleTheme_ShowsToastWithNewThemeName()
    {
        TestCulture.RunIn("en", () =>
        {
            var model = NewModel();
            Assert.Equal(AppTheme.System, model.AppTheme);

            model.CycleThemeCommand.Execute(null);

            Assert.True(model.IsToastVisible);
            Assert.Equal("Theme: Light", model.ToastMessage);

            model.CycleThemeCommand.Execute(null);

            Assert.True(model.IsToastVisible);
            Assert.Equal("Theme: Dark", model.ToastMessage);
        });
    }

    [Fact]
    public void CycleLanguage_ShowsToastWithNewLanguageName()
    {
        TestCulture.RunIn("ru", () =>
        {
            var model = NewModel();
            Assert.Equal(AppLanguage.System, model.AppLanguage);

            model.CycleLanguageCommand.Execute(null);

            Assert.True(model.IsToastVisible);
            Assert.Equal("Язык: Русский", model.ToastMessage);
        });
    }

    [Fact]
    public void ToastRequested_ShowsToast()
    {
        var toast = new FakeToastService();
        var model = NewModel(toast);

        toast.RaiseToastRequested("hello");

        Assert.True(model.IsToastVisible);
        Assert.Equal("hello", model.ToastMessage);
    }

    private static MainViewModel NewModel() => NewModel(new FakeToastService());

    private static MainViewModel NewModel(FakeToastService toast) => new(
        new ThemeService(static () => false, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "theme.txt")),
        new FakeLocalizationService(),
        TestLocalization.For<MainViewModel>(),
        new RecordingUiDispatcher(),
        new FakeRevitProcessGuard(),
        toast,
        new FakeDialogService(),
        new FakeUpdateChecker(),
        new FakeUrlOpener());
}
