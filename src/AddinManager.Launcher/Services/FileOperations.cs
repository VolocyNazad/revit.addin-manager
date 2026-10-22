using System.IO;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Guard;
using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace AddinManager.Launcher.Services;

/// <summary>
/// Операции с файлами списка: создание, удаление в Корзину, перемещение и копирование.
/// Сервис владеет диалогами, мутациями <see cref="IAddinStore"/> и записью undo; зона списка
/// получает <see cref="FileOperationOutcome"/> и применяет его к виду (перечитывание, выбор,
/// баннер, тост).
/// </summary>
public sealed class FileOperations(
    IAddinStore store,
    IDialogService dialogService,
    IUndoService undoService,
    IStringLocalizer<FileOperations> localizer,
    ILogger<FileOperations> logger,
    IEditorDirtyState? dirtyState = null) : IFileOperations
{
    /// <inheritdoc />
    public FileOperationOutcome AddFile()
    {
        if (dialogService.PromptNewFile() is not { } options)
            return FileOperationOutcome.None;

        try
        {
            store.CreateFile(options.FileName, options.Version, options.Scope, options.Disabled);
        }
        catch (Exception ex) when (ex is IOException or RevitRunningException)
        {
            return new FileOperationOutcome(BannerError: ex.Message);
        }

        return new FileOperationOutcome(RefreshNeeded: true);
    }

    /// <inheritdoc />
    public FileOperationOutcome DeleteRow(AddinFileRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!dialogService.Confirm(localizer["DeleteConfirm", row.FileName]))
            return FileOperationOutcome.None;

        DeletedFile backup;
        try
        {
            backup = new DeletedFile(row.File, store.Delete(row.File));
        }
        catch (Exception ex) when (ex is IOException or RevitRunningException or UnauthorizedAccessException)
        {
            row.ErrorMessage = ex.Message;
            return FileOperationOutcome.None;
        }

        undoService.Record(new UndoDelete([backup]));
        return new FileOperationOutcome(RefreshNeeded: true);
    }

    /// <inheritdoc />
    public FileOperationOutcome DeleteSelected(IReadOnlyList<AddinFileRowViewModel> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
            return FileOperationOutcome.None;

        if (!dialogService.Confirm(localizer["DeleteMultipleConfirm", rows.Count]))
            return FileOperationOutcome.None;

        logger.LogInformation("BulkDelete: подтверждено удаление {Count} файлов", rows.Count);
        string? firstError = null;
        var backups = new List<DeletedFile>();
        foreach (AddinFileRowViewModel row in rows)
        {
            try
            {
                backups.Add(new DeletedFile(row.File, store.Delete(row.File)));
            }
            catch (Exception ex) when (ex is IOException or RevitRunningException or UnauthorizedAccessException)
            {
                logger.LogWarning(
                    ex,
                    "BulkDelete {FileName} ({Scope}, {Version}): не удалён",
                    row.FileName, row.Scope, row.Version);
                firstError ??= ex.Message;
            }
        }

        if (backups.Count > 0)
            undoService.Record(new UndoDelete(backups));

        return new FileOperationOutcome(RefreshNeeded: true, BannerError: firstError);
    }

    /// <inheritdoc />
    public FileOperationOutcome MoveCopy(AddinFileRowViewModel row, MoveCopyKind kind)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!EnsureEditorSaved())
            return FileOperationOutcome.None;

        MoveCopyOptions? options = dialogService.PromptMoveCopy(new MoveCopyRequest(row.File, kind));
        if (options is null)
            return FileOperationOutcome.None;

        try
        {
            if (kind == MoveCopyKind.Move)
            {
                var result = store.Move(row.File, options.Version, options.Scope, options.EnableAfter, options.Overwrite);
                undoService.Record(new UndoMove(result.File, row.File, result.ReplacedPath, result.ReplacedContent));
                return new FileOperationOutcome(
                    RefreshNeeded: true,
                    SelectIdentity: result.File.Identity,
                    RevealTarget: result.File);
            }

            var copy = store.Copy(row.File, options.Version, options.Scope, options.EnableAfter, options.Overwrite);
            undoService.Record(new UndoCopy(row.File, copy.File, copy.ReplacedPath, copy.ReplacedContent));
            return new FileOperationOutcome(
                RefreshNeeded: true,
                Toast: localizer["CopyDoneToMessage", copy.File.Version, copy.File.Scope]);
        }
        catch (Exception ex) when (ex is IOException or RevitRunningException or UnauthorizedAccessException)
        {
            row.ErrorMessage = ex.Message;
            return FileOperationOutcome.None;
        }
    }

    /// <summary>
    /// Не даёт молча потерять правки открытого файла перед переносом: сохраняет видимую панель
    /// или спрашивает подтверждение на отбрасывание. Отказ на любом шаге отменяет операцию.
    /// </summary>
    private bool EnsureEditorSaved()
    {
        if (dirtyState is not { IsDirty: true })
            return true;

        if (dialogService.Confirm(localizer["SaveBeforeMoveConfirm"]))
            return dirtyState.TrySave();

        return dialogService.Confirm(localizer["DiscardBeforeMoveConfirm"]);
    }
}
