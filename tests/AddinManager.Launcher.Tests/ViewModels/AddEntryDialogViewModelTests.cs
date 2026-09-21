using AddinManager.Core.Manifests;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Диалог новой записи: выбор типа чипами, добавить закрывает результатом, отмена — без.
/// </summary>
public sealed class AddEntryDialogViewModelTests
{
    [Fact]
    public void Defaults_ApplicationAndNoResult()
    {
        var model = NewModel();

        Assert.Equal(AddinEntryType.Application, model.EntryType);
        Assert.Null(model.Result);
    }

    [Fact]
    public void SetEntryType_SwitchesType()
    {
        var model = NewModel();

        model.SetEntryTypeCommand.Execute(AddinEntryType.Command);

        Assert.Equal(AddinEntryType.Command, model.EntryType);
    }

    [Fact]
    public void Add_SetsTrue()
    {
        var model = NewModel();
        model.SetEntryTypeCommand.Execute(AddinEntryType.DBApplication);

        model.AddCommand.Execute(null);

        Assert.True(model.Result);
    }

    [Fact]
    public void Cancel_SetsFalse()
    {
        var model = NewModel();

        model.CancelCommand.Execute(null);

        Assert.False(model.Result);
    }

    [Fact]
    public void Labels_EnglishTokens()
    {
        var model = NewModel();

        TestCulture.RunIn("en", () =>
        {
            Assert.Equal("Type", model.TypeLabel);
            Assert.Equal("Add", model.AddButtonLabel);
            Assert.Equal("Cancel", model.CancelButtonLabel);
        });
    }

    private static AddEntryDialogViewModel NewModel() =>
        new(TestLocalization.For<AddEntryDialogViewModel>());
}
