namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>Диалог обновления: кнопки закрывают окно результатом, по умолчанию — без результата.</summary>
public sealed class UpdateDialogViewModelTests
{
    [Fact]
    public void Initial_NoResultAndEmptyMessage()
    {
        var model = NewModel();

        Assert.Null(model.Result);
        Assert.Equal(string.Empty, model.Message);
    }

    [Fact]
    public void Download_SetsTrue()
    {
        var model = NewModel();

        model.DownloadCommand.Execute(null);

        Assert.True(model.Result);
    }

    [Fact]
    public void Later_SetsFalse()
    {
        var model = NewModel();

        model.LaterCommand.Execute(null);

        Assert.False(model.Result);
    }

    private static UpdateDialogViewModel NewModel() =>
        new(TestLocalization.For<UpdateDialogViewModel>());
}
