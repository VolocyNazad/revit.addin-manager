using AddinManager.Core.Manifests;

namespace AddinManager.Core.Storage;

/// <summary>
/// Cross-file <c>AddInId</c> rules shared by the plugin list, the entries zone and the markup editor.
/// Duplicates are counted only inside one Revit version, and a file is identified by
/// <see cref="AddinFileIdentity"/> rather than by path, because rows are refreshed and recreated.
/// </summary>
public static class AddinFileDuplicates
{
    /// <summary>
    /// Finds the identifiers that occur in another file of the same Revit version. The file itself is
    /// excluded by identity, so its own entries never make an identifier a cross-file duplicate.
    /// </summary>
    /// <param name="file">The file the caller is inspecting.</param>
    /// <param name="files">Every scanned file, across all versions and scopes.</param>
    /// <returns>The identifiers found in the other files of the same version; empty when there are none.</returns>
    public static HashSet<Guid> FindInOtherFiles(AddinFile file, IEnumerable<AddinFile> files)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(files);

        var otherIds = new HashSet<Guid>();
        foreach (AddinFile candidate in files)
        {
            if (candidate.Version != file.Version)
                continue;
            if (candidate.Identity == file.Identity)
                continue;
            foreach (AddinEntry entry in candidate.Manifest.Entries)
                otherIds.Add(entry.AddInId);
        }

        return otherIds;
    }

    /// <summary>
    /// Finds the identifiers that occur in at least two distinct files of the same Revit version.
    /// An identifier repeated inside one file is not a cross-file duplicate.
    /// </summary>
    /// <param name="files">Every scanned file, across all versions and scopes.</param>
    /// <returns>
    /// The duplicated identifiers grouped by Revit version; versions without cross-file duplicates are omitted.
    /// </returns>
    public static Dictionary<string, HashSet<Guid>> FindDuplicatedByVersion(IEnumerable<AddinFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var filesByIdentity = new Dictionary<(string Version, Guid AddInId), HashSet<AddinFileIdentity>>();
        foreach (AddinFile file in files)
        {
            foreach (Guid addInId in file.Manifest.Entries.Select(entry => entry.AddInId).Distinct())
            {
                (string Version, Guid AddInId) key = (file.Version, addInId);
                if (!filesByIdentity.TryGetValue(key, out HashSet<AddinFileIdentity>? identities))
                    filesByIdentity[key] = identities = [];
                identities.Add(file.Identity);
            }
        }

        var duplicatedByVersion = new Dictionary<string, HashSet<Guid>>(StringComparer.Ordinal);
        foreach (KeyValuePair<(string Version, Guid AddInId), HashSet<AddinFileIdentity>> pair in filesByIdentity)
        {
            if (pair.Value.Count < 2)
                continue;
            if (!duplicatedByVersion.TryGetValue(pair.Key.Version, out HashSet<Guid>? duplicatedIds))
                duplicatedByVersion[pair.Key.Version] = duplicatedIds = [];
            duplicatedIds.Add(pair.Key.AddInId);
        }

        return duplicatedByVersion;
    }
}
