using System.IO;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Abstractions.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace AddinManager.Launcher.Services;

/// <summary>
/// Одноуровневый стек отмены: выполняет записанное действие через <see cref="IAddinStore"/> и
/// возвращает идентичность файла, на который зоне списка нужно вернуть выбор. Строки, view и
/// тосты сюда не входят — это остаётся у <see cref="ViewModels.ListViewModel"/>.
/// </summary>
public sealed class UndoService(
    IAddinStore store,
    ILogger<UndoService> logger,
    IStringLocalizer<UndoService> localizer) : IUndoService
{
    private UndoAction? _lastAction;

    /// <inheritdoc />
    public bool HasUndo => _lastAction is not null;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public void Record(UndoAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        _lastAction = action;
        logger.LogDebug("Undo: записано действие {Action}", action.ActionName);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public AddinFileIdentity Undo(Func<AddinFileIdentity, AddinFile?> resolveCurrentFile)
    {
        ArgumentNullException.ThrowIfNull(resolveCurrentFile);

        if (_lastAction is not { } action)
            throw new InvalidOperationException("There is no action to undo.");

        // Стек очищается до выполнения: неудачная отмена не должна оставлять действие,
        // которое нельзя повторить (то же поведение, что было у списка).
        _lastAction = null;
        Changed?.Invoke(this, EventArgs.Empty);

        logger.LogInformation("Undo: отмена действия {Action}", action.ActionName);
        return action switch
        {
            UndoToggle toggle => RestoreToggle(toggle, resolveCurrentFile),
            UndoToggleGroup group => RestoreToggleGroup(group, resolveCurrentFile),
            UndoDelete delete => RestoreDeletedFiles(delete),
            UndoMove move => RestoreMovedFile(move, resolveCurrentFile),
            UndoCopy copy => RemoveCreatedCopy(copy, resolveCurrentFile),
            UndoRename rename => RestoreRename(rename, resolveCurrentFile),
            _ => throw new NotSupportedException($"Unsupported undo action {action.GetType().Name}."),
        };
    }

    private AddinFileIdentity RestoreToggle(UndoToggle toggle, Func<AddinFileIdentity, AddinFile?> resolveCurrentFile)
    {
        AddinFile current = Resolve(toggle.After.Identity, resolveCurrentFile);
        store.SetEnabled(current, toggle.PreviousEnabled);
        return toggle.After.Identity;
    }

    private AddinFileIdentity RestoreToggleGroup(UndoToggleGroup group, Func<AddinFileIdentity, AddinFile?> resolveCurrentFile)
    {
        foreach (UndoToggle toggle in group.Toggles)
        {
            AddinFile current = Resolve(toggle.After.Identity, resolveCurrentFile);
            store.SetEnabled(current, toggle.PreviousEnabled);
        }

        return group.Toggles[0].After.Identity;
    }

    private AddinFileIdentity RestoreDeletedFiles(UndoDelete delete)
    {
        foreach (DeletedFile file in delete.Files)
            store.Restore(file.File.FullPath, file.Content);

        return delete.Files[0].File.Identity;
    }

    private AddinFileIdentity RestoreMovedFile(UndoMove move, Func<AddinFileIdentity, AddinFile?> resolveCurrentFile)
    {
        AddinFile current = Resolve(move.Moved.Identity, resolveCurrentFile);
        store.Move(current, move.Original.Version, move.Original.Scope, move.Original.Enabled, overwrite: false);
        RestoreReplacedTarget(move.ReplacedPath, move.ReplacedContent);
        return move.Original.Identity;
    }

    private AddinFileIdentity RemoveCreatedCopy(UndoCopy copy, Func<AddinFileIdentity, AddinFile?> resolveCurrentFile)
    {
        AddinFile current = Resolve(copy.Copy.Identity, resolveCurrentFile);
        store.Delete(current);
        RestoreReplacedTarget(copy.ReplacedPath, copy.ReplacedContent);
        return copy.Source.Identity;
    }

    private AddinFileIdentity RestoreRename(UndoRename rename, Func<AddinFileIdentity, AddinFile?> resolveCurrentFile)
    {
        AddinFile current = Resolve(rename.After.Identity, resolveCurrentFile);
        store.Rename(current, rename.Before.FileName);
        return rename.Before.Identity;
    }

    private AddinFile Resolve(AddinFileIdentity identity, Func<AddinFileIdentity, AddinFile?> resolveCurrentFile)
    {
        if (resolveCurrentFile(identity) is { } current)
            return current;

        logger.LogWarning(
            "Undo: файл {FileName} ({Scope}, {Version}) больше не существует",
            identity.FileName, identity.Scope, identity.Version);
        throw new IOException(localizer["UndoFileMissing"]);
    }

    private void RestoreReplacedTarget(string? path, byte[]? content)
    {
        if (path is null || content is null)
            return;

        store.Restore(path, content);
    }
}
