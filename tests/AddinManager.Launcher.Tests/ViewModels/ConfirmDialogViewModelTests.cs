namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Кнопки окна подтверждения пишут результат (вид подхватывает его в DialogResult и
/// закрывается); подписи — из resx под текущую культуру.
/// </summary>
public sealed class ConfirmDialogViewModelTests
{
    [Fact]
    public void Yes_SetsTrue()
    {
        var model = new ConfirmDialogViewModel(TestLocalization.For<ConfirmDialogViewModel>());
        Assert.Null(model.Result);

        model.YesCommand.Execute(null);

        Assert.True(model.Result);
    }

    [Fact]
    public void No_SetsFalse()
    {
        var model = new ConfirmDialogViewModel(TestLocalization.For<ConfirmDialogViewModel>());

        model.NoCommand.Execute(null);

        Assert.False(model.Result);
    }

    [Fact]
    public void Labels_FollowCurrentUiCulture()
    {
        var model = new ConfirmDialogViewModel(TestLocalization.For<ConfirmDialogViewModel>());

        TestCulture.RunIn("ru", () =>
        {
            Assert.Equal("Да", model.YesLabel);
            Assert.Equal("Нет", model.NoLabel);
        });
        TestCulture.RunIn("en", () =>
        {
            Assert.Equal("Yes", model.YesLabel);
            Assert.Equal("No", model.NoLabel);
        });
    }
}
