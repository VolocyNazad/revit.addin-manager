using System.Windows;
using AddinManager.Core.Manifests;
using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.ViewModels.Dialogs;
using AddinManager.Launcher.Views.Dialogs;

namespace AddinManager.Launcher.Services;

/// <summary>Вопросы через собственное окно вместо системного <c>MessageBox</c> (вызывать из потока UI).</summary>
public sealed class WpfDialogService(
    Func<ConfirmDialogView> confirmViewFactory,
    Func<AddFileDialogView> addFileViewFactory,
    Func<AddEntryDialogView> addEntryViewFactory,
    Func<UpdateDialogView> updateViewFactory,
    Func<MoveCopyDialogView> moveCopyViewFactory,
    IUrlOpener urlOpener) : IDialogService
{
    /// <inheritdoc />
    public bool Confirm(string message)
    {
        var view = confirmViewFactory();
        var model = (ConfirmDialogViewModel)view.DataContext;
        model.Message = message;
        view.Owner = Application.Current?.MainWindow;
        return view.ShowDialog() == true;
    }

    /// <inheritdoc />
    public NewFileOptions? PromptNewFile()
    {
        var view = addFileViewFactory();
        var model = (AddFileDialogViewModel)view.DataContext;
        view.Owner = Application.Current?.MainWindow;
        if (view.ShowDialog() != true)
            return null;

        return new NewFileOptions(model.FileName.Trim(), model.Scope, model.Version, model.IsDisabled);
    }

    /// <inheritdoc />
    public AddinEntryType? PromptNewEntry()
    {
        var view = addEntryViewFactory();
        var model = (AddEntryDialogViewModel)view.DataContext;
        view.Owner = Application.Current?.MainWindow;
        if (view.ShowDialog() != true)
            return null;

        return model.EntryType;
    }

    /// <inheritdoc />
    public MoveCopyOptions? PromptMoveCopy(MoveCopyRequest request)
    {
        var view = moveCopyViewFactory();
        var model = (MoveCopyDialogViewModel)view.DataContext;
        model.Initialize(request);
        view.Owner = Application.Current?.MainWindow;
        if (view.ShowDialog() != true)
            return null;

        return new MoveCopyOptions(model.Version, model.Scope, model.EnableAfter, model.Overwrite);
    }

    /// <inheritdoc />
    public bool PromptUpdate(string message, string downloadUrl)
    {
        var view = updateViewFactory();
        var model = (UpdateDialogViewModel)view.DataContext;
        model.Message = message;
        view.Owner = Application.Current?.MainWindow;
        if (view.ShowDialog() != true)
            return false;

        urlOpener.Open(downloadUrl);
        return true;
    }
}
