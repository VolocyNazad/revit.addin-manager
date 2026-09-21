using AddinManager.Core.Abstractions.Guard;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Guard;
using AddinManager.Core.Parsing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace AddinManager.Core.Storage;

/// <summary>Файловая реализация <see cref="IAddinMarkupService"/>: читает/пишет .addin файл как текст.</summary>
public sealed class FileAddinMarkupService(
    IAddinManifestParser parser,
    ILogger<FileAddinMarkupService> logger,
    IRevitProcessGuard guard,
    IStringLocalizer<FileAddinMarkupService> localizer) : IAddinMarkupService
{
    /// <inheritdoc />
    public string ReadRaw(AddinFile file)
    {
        logger.LogDebug("ReadRaw({FileName}, {Scope}, {Version})", file.FileName, file.Scope, file.Version);
        return File.ReadAllText(file.FullPath);
    }

    /// <inheritdoc />
    public string? Validate(string xml)
    {
        try
        {
            parser.Parse(xml);
            return null;
        }
        catch (AddinManifestFormatException ex)
        {
            return ex.Message;
        }
    }

    /// <inheritdoc />
    public void Save(AddinFile file, string xml)
    {
        if (guard.IsRunning)
        {
            logger.LogWarning(
                "Save({FileName}, {Scope}, {Version}): отклонено — запущен Revit",
                file.FileName, file.Scope, file.Version);
            throw new RevitRunningException(localizer["BlockedByRunningRevit"]);
        }

        var error = Validate(xml);
        if (error is not null)
        {
            logger.LogWarning(
                "Save({FileName}, {Scope}, {Version}): отклонено валидацией — {Error}",
                file.FileName, file.Scope, file.Version, error);
            throw new AddinManifestFormatException(error);
        }

        var tempPath = file.FullPath + ".tmp";
        var backupPath = file.FullPath + ".bak";

        try
        {
            File.WriteAllText(tempPath, xml);

            if (File.Exists(file.FullPath))
                File.Replace(tempPath, file.FullPath, backupPath);
            else
                File.Move(tempPath, file.FullPath);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Save({FileName}, {Scope}, {Version}): не удалось записать {Path}",
                file.FileName, file.Scope, file.Version, file.FullPath);

            // Не throw; — тот же экземпляр не должен уйти наверх залогированным дважды (S2139), см. FileSystemAddinStore.SetEnabled.
            throw new IOException($"Не удалось сохранить '{file.FullPath}'.", ex);
        }

        logger.LogInformation(
            "Save({FileName}, {Scope}, {Version}): сохранено, {Length} симв., резервная копия — {Backup}",
            file.FileName, file.Scope, file.Version, xml.Length, backupPath);
    }
}
