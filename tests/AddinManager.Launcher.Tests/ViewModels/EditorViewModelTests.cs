using AddinManager.Core.Manifests;
using AddinManager.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace AddinManager.Launcher.Tests.ViewModels;

/// <summary>
/// Общий заголовок зоны редактора: имя и мета-строка файла приходят из выбора, а состояние
/// (несохранённые правки, ошибка, подписи и доступность Save/Discard) — из видимой панели и
/// переключается вместе с режимом. Заголовок один на все режимы, включая комбинированные.
/// </summary>
public sealed class EditorViewModelTests
{
    private readonly FakeFileSelection _fileSelection = new();
    private readonly FakeSavablePane _formPane = new();
    private readonly FakeSavablePane _settingsPane = new();
    private readonly EditorViewModel _editor;

    public EditorViewModelTests()
    {
        _editor = new EditorViewModel(
            new FakeLocalizationService(),
            TestLocalization.For<EditorViewModel>(),
            _fileSelection,
            mode => mode switch
            {
                EditorMode.Form => _formPane,
                EditorMode.Settings => _settingsPane,
                _ => null,
            });
    }

    [Fact]
    public void HeaderWithoutSavablePane_HidesActions()
    {
        _editor.SetEditorMode(EditorMode.Entries);

        Assert.False(_editor.ShowHeaderActions);
        Assert.False(_editor.IsDirty);
        Assert.Null(_editor.HeaderError);
        Assert.False(_editor.HasHeaderError);
        Assert.Equal(string.Empty, _editor.SaveLabel);
        Assert.Equal(string.Empty, _editor.DiscardLabel);
        Assert.False(_editor.SaveActiveCommand.CanExecute(null));
        Assert.False(_editor.DiscardActiveCommand.CanExecute(null));
    }

    [Fact]
    public void HeaderShowsActivePaneState()
    {
        _formPane.IsDirty = true;
        _formPane.ErrorMessage = "invalid xml";
        _formPane.SaveButtonLabel = "Save (Ctrl+S)";
        _formPane.DiscardButtonLabel = "Discard (Esc)";

        _editor.SetEditorMode(EditorMode.Form);

        Assert.True(_editor.ShowHeaderActions);
        Assert.True(_editor.IsDirty);
        Assert.Equal("invalid xml", _editor.HeaderError);
        Assert.True(_editor.HasHeaderError);
        Assert.Equal("Save (Ctrl+S)", _editor.SaveLabel);
        Assert.Equal("Discard (Esc)", _editor.DiscardLabel);
    }

    [Fact]
    public void SwitchingMode_SwitchesHeaderToOtherPane()
    {
        _formPane.IsDirty = true;
        _formPane.SaveButtonLabel = "Form save";
        _settingsPane.SaveButtonLabel = "Settings save";

        _editor.SetEditorMode(EditorMode.Form);
        Assert.Equal("Form save", _editor.SaveLabel);
        Assert.True(_editor.IsDirty);

        _editor.SetEditorMode(EditorMode.Settings);
        Assert.Equal("Settings save", _editor.SaveLabel);
        Assert.False(_editor.IsDirty);
    }

    [Fact]
    public void PaneChanges_RefreshHeaderImmediately()
    {
        _editor.SetEditorMode(EditorMode.Form);
        HashSet<string> raised = [];
        _editor.PropertyChanged += (_, args) => raised.Add(args.PropertyName ?? string.Empty);

        _formPane.IsDirty = true;
        _formPane.ErrorMessage = "error";
        _formPane.SaveButtonLabel = "Save";

        Assert.Contains(nameof(EditorViewModel.IsDirty), raised);
        Assert.Contains(nameof(EditorViewModel.HeaderError), raised);
        Assert.Contains(nameof(EditorViewModel.HasHeaderError), raised);
        Assert.Contains(nameof(EditorViewModel.SaveLabel), raised);
        Assert.False(_editor.SaveActiveCommand.CanExecute(null));
        Assert.True(_editor.DiscardActiveCommand.CanExecute(null));

        _formPane.ErrorMessage = null;
        Assert.True(_editor.SaveActiveCommand.CanExecute(null));
    }

    [Fact]
    public void SelectedFile_ComesFromFileSelection()
    {
        var file = NewFileRow("A.addin");
        HashSet<string> raised = [];
        _editor.PropertyChanged += (_, args) => raised.Add(args.PropertyName ?? string.Empty);

        _fileSelection.SelectedFile = file;

        Assert.Same(file, _editor.SelectedFile);
        Assert.Contains(nameof(EditorViewModel.SelectedFile), raised);
    }

    [Fact]
    public void SaveAndDiscardActiveCommands_RouteToVisiblePane()
    {
        _formPane.IsDirty = true;
        _editor.SetEditorMode(EditorMode.Form);

        _editor.SaveActiveCommand.Execute(null);
        _editor.DiscardActiveCommand.Execute(null);

        Assert.Equal(1, _formPane.SaveCalls);
        Assert.Equal(1, _formPane.DiscardCalls);
        Assert.Equal(0, _settingsPane.SaveCalls);
        Assert.Equal(0, _settingsPane.DiscardCalls);
    }

    [Fact]
    public void SaveActiveCommand_IsUnavailableWhenActivePaneCannotSave()
    {
        _editor.SetEditorMode(EditorMode.Form);
        Assert.False(_editor.SaveActiveCommand.CanExecute(null));

        _formPane.IsDirty = true;
        _formPane.ErrorMessage = "invalid xml";
        Assert.False(_editor.SaveActiveCommand.CanExecute(null));

        _formPane.ErrorMessage = null;
        Assert.True(_editor.SaveActiveCommand.CanExecute(null));
    }

    private static AddinFileRowViewModel NewFileRow(string fileName)
    {
        var file = new AddinFile(
            fileName,
            AddinScope.User,
            "2025",
            Enabled: true,
            FullPath: @"C:\Addins\2025\" + fileName,
            VersionRootDirectory: @"C:\Addins\2025",
            Manifest: new AddinManifest([], null, null, null, null, [], [], []));
        return new AddinFileRowViewModel(
            new FakeAddinStore(),
            file,
            NullLogger<AddinFileRowViewModel>.Instance,
            TestLocalization.For<AddinFileRowViewModel>());
    }
}
