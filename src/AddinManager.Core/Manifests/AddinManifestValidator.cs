namespace AddinManager.Core.Manifests;

/// <summary>Kinds of structural problems found in a manifest or in one of its entries.</summary>
public enum ManifestIssueKind
{
    /// <summary>The file has no <c>AddIn</c> entries at all.</summary>
    NoEntries,

    /// <summary>A required field is empty; the entry is an unfinished draft.</summary>
    EmptyRequiredValue,

    /// <summary>The entry <c>Type</c> is not one of the types this application understands.</summary>
    UnknownEntryType,

    /// <summary>The same <c>AddInId</c> appears in more than one entry of the same file.</summary>
    DuplicateAddInIdInFile,
}

/// <summary>One structural problem: its kind plus the entry it belongs to, when there is one.</summary>
/// <param name="Kind">What is wrong.</param>
/// <param name="AddInId">Identifier of the affected entry, when the issue is entry-specific.</param>
/// <param name="RawType">The unrecognized <c>Type</c> value, when the issue is an unknown type.</param>
public sealed record ManifestIssue(
    ManifestIssueKind Kind,
    Guid? AddInId = null,
    string? RawType = null);

/// <summary>
/// The single structural rule set of an <c>.addin</c> manifest. The parser, the list rows and the
/// markup editor all ask this class instead of repeating the checks; empty required values are a
/// draft warning, not a load error, and a duplicate <c>AddInId</c> keeps the file readable so the
/// user can fix it.
/// </summary>
public static class AddinManifestValidator
{
    /// <summary>Checks the whole manifest.</summary>
    /// <param name="manifest">The parsed manifest.</param>
    /// <returns>The issues found, in the order they are shown to the user.</returns>
    public static IReadOnlyList<ManifestIssue> Validate(AddinManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (manifest.Entries.Count == 0)
            return [new ManifestIssue(ManifestIssueKind.NoEntries)];

        var issues = new List<ManifestIssue>();
        foreach (AddinEntry entry in manifest.Entries)
            issues.AddRange(ValidateEntry(entry));

        foreach (Guid duplicatedAddInId in FindDuplicateAddInIds(manifest.Entries))
            issues.Add(new ManifestIssue(ManifestIssueKind.DuplicateAddInIdInFile, duplicatedAddInId));

        return issues;
    }

    /// <summary>Checks one entry for its own problems (not the duplicates shared with other entries).</summary>
    /// <param name="entry">The entry to check.</param>
    /// <returns>The entry-specific issues; empty when the entry is complete and recognized.</returns>
    public static IReadOnlyList<ManifestIssue> ValidateEntry(AddinEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var issues = new List<ManifestIssue>();
        if (string.IsNullOrWhiteSpace(entry.AssemblyPath) || string.IsNullOrWhiteSpace(entry.FullClassName))
            issues.Add(new ManifestIssue(ManifestIssueKind.EmptyRequiredValue, entry.AddInId));

        if (entry.Type == AddinEntryType.Unknown)
            issues.Add(new ManifestIssue(ManifestIssueKind.UnknownEntryType, entry.AddInId, entry.RawType));

        return issues;
    }

    /// <summary>Finds the identifiers that occur in more than one entry of the same file.</summary>
    /// <param name="entries">The entries of one file.</param>
    /// <returns>The duplicated identifiers.</returns>
    public static IReadOnlySet<Guid> FindDuplicateAddInIds(IReadOnlyCollection<AddinEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries
            .GroupBy(entry => entry.AddInId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();
    }

    /// <summary>Maps a raw <c>Type</c> attribute to the entry type; an unknown value becomes <see cref="AddinEntryType.Unknown"/>.</summary>
    /// <param name="rawType">The raw attribute value, or <see langword="null"/> when the attribute is absent.</param>
    /// <returns>The parsed entry type.</returns>
    public static AddinEntryType ParseEntryType(string? rawType) => rawType?.ToLowerInvariant() switch
    {
        "application" => AddinEntryType.Application,
        "dbapplication" => AddinEntryType.DBApplication,
        "command" => AddinEntryType.Command,
        _ => AddinEntryType.Unknown,
    };

    /// <summary>Gets whether a raw <c>Type</c> value is one this application understands.</summary>
    /// <param name="rawType">The raw attribute value.</param>
    /// <returns><see langword="true"/> for a recognized type.</returns>
    public static bool IsRecognizedEntryType(string? rawType) =>
        ParseEntryType(rawType) != AddinEntryType.Unknown;
}
