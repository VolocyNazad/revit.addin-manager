using System.Xml;
using System.Xml.Linq;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Manifests;
using Microsoft.Extensions.Localization;

namespace AddinManager.Core.Parsing;

/// <summary>
/// LINQ to XML реализация парсера: без зависимости от Revit API.
/// Честный round-trip: декларация, порядок детей корня и записей, чужие узлы, комментарии
/// и незнакомые атрибуты сохраняются. Сообщения об ошибках идут через
/// <see cref="IStringLocalizer{T}"/> — они показываются в UI-баннерах как есть, а resx
/// лежат рядом с классом (нейтральный — английский).
/// </summary>
public sealed class LinqToXmlAddinManifestParser(IStringLocalizer<LinqToXmlAddinManifestParser> localizer) : IAddinManifestParser
{
    /// <summary>Local names of the single-value and multi-value elements of one <c>AddIn</c> entry.</summary>
    private static class EntryTags
    {
        public const string Name = "Name";
        public const string Text = "Text";
        public const string Description = "Description";
        public const string LongDescription = "LongDescription";
        public const string Assembly = "Assembly";
        public const string AddInId = "AddInId";
        public const string FullClassName = "FullClassName";
        public const string AvailabilityClassName = "AvailabilityClassName";
        public const string VendorId = "VendorId";
        public const string VendorDescription = "VendorDescription";
        public const string VisibilityMode = "VisibilityMode";
        public const string Discipline = "Discipline";
        public const string LargeImage = "LargeImage";
        public const string SmallImage = "SmallImage";
        public const string ToolTipImage = "ToolTipImage";
    }

    /// <summary>
    /// One element of an <c>AddIn</c> entry: its tag and how to read its value from the model on write.
    /// The table is the single definition of the entry field order used by the round-trip writer.
    /// </summary>
    private sealed record EntryField(
        string Tag,
        Func<AddinEntry, string?> Value,
        Func<AddinEntry, IReadOnlyList<string>>? ListValue = null);

    private static readonly EntryField[] EntryFields =
    [
        new(EntryTags.Name, static entry => entry.Name),
        new(EntryTags.Text, static entry => entry.Text),
        new(EntryTags.Description, static entry => entry.Description),
        new(EntryTags.LongDescription, static entry => entry.LongDescription),
        new(EntryTags.Assembly, static entry => entry.AssemblyPath),
        new(EntryTags.AddInId, static entry => entry.AddInId.ToString()),
        new(EntryTags.FullClassName, static entry => entry.FullClassName),
        new(EntryTags.AvailabilityClassName, static entry => entry.AvailabilityClassName),
        new(EntryTags.VendorId, static entry => entry.VendorId),
        new(EntryTags.VendorDescription, static entry => entry.VendorDescription),
        new(EntryTags.VisibilityMode, static entry => null, static entry => entry.VisibilityModes),
        new(EntryTags.Discipline, static entry => null, static entry => entry.Disciplines),
        new(EntryTags.LargeImage, static entry => entry.LargeImage),
        new(EntryTags.SmallImage, static entry => entry.SmallImage),
        new(EntryTags.ToolTipImage, static entry => entry.ToolTipImage),
    ];

    private static readonly string[] CanonicalOrder = [.. EntryFields.Select(field => field.Tag)];

    private static readonly Dictionary<string, EntryField> EntryFieldsByTag =
        EntryFields.ToDictionary(field => field.Tag, StringComparer.Ordinal);

    /// <inheritdoc />
    public AddinManifest Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new AddinManifestFormatException(localizer["EmptyManifest"].Value);

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            throw new AddinManifestFormatException(localizer["BrokenXml", ex.Message].Value, ex);
        }

        var root = doc.Root;
        if (root is null || root.Name.LocalName != "RevitAddIns")
            throw new AddinManifestFormatException(localizer["RootElementNotFound"].Value);

        var entries = new List<AddinEntry>();
        ManifestSettings? settings = null;
        var rootUnknowns = new List<XNode>();
        var rootOrder = new List<ManifestRootChild>();
        var seenSettings = false;

        foreach (var node in root.Nodes())
        {
            if (node is not XElement el)
            {
                if (AddUnknown(rootUnknowns, node))
                    rootOrder.Add(ManifestRootChild.Unknown);
                continue;
            }

            if (el.Name.LocalName == "AddIn")
            {
                entries.Add(ParseEntry(el));
                rootOrder.Add(ManifestRootChild.Entry);
            }
            else if (el.Name.LocalName == "ManifestSettings" && !seenSettings)
            {
                settings = ParseSettings(el);
                seenSettings = true;
                rootOrder.Add(ManifestRootChild.Settings);
            }
            else
            {
                rootUnknowns.Add(Copy(el));
                rootOrder.Add(ManifestRootChild.Unknown);
            }
        }

        var before = doc.Nodes().TakeWhile(n => n != root).Where(Significant).Select(Copy).ToList();
        var after = doc.Nodes().SkipWhile(n => n != root).Skip(1).Where(Significant).Select(Copy).ToList();

        return new AddinManifest(
            entries,
            settings,
            doc.Declaration?.Version,
            doc.Declaration?.Encoding,
            doc.Declaration?.Standalone,
            before,
            after,
            rootUnknowns,
            rootOrder,
            root.Attributes().Select(attribute => new XAttribute(attribute)).ToList());
    }

    /// <inheritdoc />
    public string ToXml(AddinManifest manifest)
    {
        var root = new XElement("RevitAddIns");
        foreach (var attribute in manifest.RootAttributes ?? [])
            root.Add(new XAttribute(attribute));

        if (manifest.RootOrder is { } rootOrder)
        {
            var entryIndex = 0;
            var unknownIndex = 0;
            var settingsWritten = false;
            foreach (var child in rootOrder)
            {
                switch (child)
                {
                    case ManifestRootChild.Entry when entryIndex < manifest.Entries.Count:
                        root.Add(WriteEntry(manifest.Entries[entryIndex++]));
                        break;
                    case ManifestRootChild.Settings when manifest.Settings is not null && !settingsWritten:
                        root.Add(WriteSettings(manifest.Settings));
                        settingsWritten = true;
                        break;
                    case ManifestRootChild.Unknown when unknownIndex < manifest.RootUnknownNodes.Count:
                        root.Add(Copy(manifest.RootUnknownNodes[unknownIndex++]));
                        break;
                }
            }

            // Новые записи/узлы, которых не было в исходном порядке, — в конец.
            for (; entryIndex < manifest.Entries.Count; entryIndex++)
                root.Add(WriteEntry(manifest.Entries[entryIndex]));
            for (; unknownIndex < manifest.RootUnknownNodes.Count; unknownIndex++)
                root.Add(Copy(manifest.RootUnknownNodes[unknownIndex]));
            if (manifest.Settings is not null && !settingsWritten)
                root.Add(WriteSettings(manifest.Settings));
        }
        else
        {
            foreach (var entry in manifest.Entries)
                root.Add(WriteEntry(entry));
            if (manifest.Settings is not null)
                root.Add(WriteSettings(manifest.Settings));
            foreach (var node in manifest.RootUnknownNodes)
                root.Add(Copy(node));
        }

        var doc = new XDocument();
        if (manifest is { DeclarationVersion: not null } or { DeclarationEncoding: not null } or { DeclarationStandalone: not null })
            doc.Declaration = new XDeclaration(
                manifest.DeclarationVersion ?? "1.0",
                manifest.DeclarationEncoding ?? "utf-8",
                manifest.DeclarationStandalone);
        foreach (var node in manifest.BeforeRoot)
            doc.Add(Copy(node));
        doc.Add(root);
        foreach (var node in manifest.AfterRoot)
            doc.Add(Copy(node));

        // XDocument.ToString() декларацию не пишет — добавляем сами.
        var body = doc.ToString();
        return doc.Declaration is null ? body : doc.Declaration + Environment.NewLine + body;
    }

    private AddinEntry ParseEntry(XElement el)
    {
        var rawType = el.Attribute("Type")?.Value;
        var type = AddinManifestValidator.ParseEntryType(rawType);

        string Req(string name)
        {
            // Пустое значение допускается: новая запись создаётся с одним AddInId и пустыми
            // Assembly/FullClassName, пока форму не заполнили. Ошибкой остаётся только
            // отсутствие элемента целиком.
            var value = el.Element(N(name))?.Value;
            if (value is null)
                throw new AddinManifestFormatException(localizer["MissingRequiredElement", name].Value);
            return value;
        }

        var assembly = Req(EntryTags.Assembly);
        var guidRaw = Req(EntryTags.AddInId);
        if (!Guid.TryParse(guidRaw.Trim(), out var guid))
            throw new AddinManifestFormatException(localizer["InvalidAddInId", guidRaw].Value);
        var fullClassName = Req(EntryTags.FullClassName);

        var order = new List<string>();
        var unknowns = new List<XNode>();
        foreach (var node in el.Nodes())
        {
            if (node is XElement child)
            {
                // Чужой тег внутри записи — тоже unknown, иначе потеряем при сериализации.
                if (CanonicalOrder.Contains(child.Name.LocalName, StringComparer.Ordinal))
                    order.Add(child.Name.LocalName);
                else
                {
                    order.Add($"<#{unknowns.Count}>");
                    unknowns.Add(Copy(child));
                }
            }
            else if (Significant(node))
            {
                order.Add($"<#{unknowns.Count}>");
                unknowns.Add(Copy(node));
            }
        }

        return new AddinEntry(
            type,
            rawType,
            el.Element(N(EntryTags.Name))?.Value,
            el.Element(N(EntryTags.Text))?.Value,
            el.Element(N(EntryTags.Description))?.Value,
            el.Element(N(EntryTags.LongDescription))?.Value,
            assembly,
            guid,
            fullClassName,
            el.Element(N(EntryTags.AvailabilityClassName))?.Value,
            el.Element(N(EntryTags.VendorId))?.Value,
            el.Element(N(EntryTags.VendorDescription))?.Value,
            el.Elements(N(EntryTags.VisibilityMode)).Select(e => e.Value).ToList(),
            el.Elements(N(EntryTags.Discipline)).Select(e => e.Value).ToList(),
            el.Element(N(EntryTags.LargeImage))?.Value,
            el.Element(N(EntryTags.SmallImage))?.Value,
            el.Element(N(EntryTags.ToolTipImage))?.Value,
            order,
            unknowns,
            el.Attributes()
                .Where(attribute => attribute.Name.LocalName != "Type")
                .Select(attribute => new XAttribute(attribute))
                .ToList());
    }

    private ManifestSettings ParseSettings(XElement el)
    {
        var raw = el.Element(N("UseRevitContext"))?.Value.Trim();
        bool? useRevitContext = raw?.ToLowerInvariant() switch
        {
            "true" => true,
            "false" => false,
            null => null,
            _ => throw new AddinManifestFormatException(localizer["InvalidUseRevitContext", raw].Value),
        };

        var order = new List<string>();
        var unknowns = new List<XNode>();
        foreach (var node in el.Nodes())
        {
            if (node is XElement child)
            {
                if (child.Name.LocalName is "UseRevitContext" or "ContextName")
                    order.Add(child.Name.LocalName);
                else
                {
                    order.Add($"<#{unknowns.Count}>");
                    unknowns.Add(Copy(child));
                }
            }
            else if (Significant(node))
            {
                order.Add($"<#{unknowns.Count}>");
                unknowns.Add(Copy(node));
            }
        }

        return new ManifestSettings(
            useRevitContext,
            el.Element(N("ContextName"))?.Value,
            order,
            unknowns,
            el.Attributes().Select(attribute => new XAttribute(attribute)).ToList());
    }

    private XElement WriteEntry(AddinEntry entry)
    {
        var el = new XElement("AddIn");
        var typeValue = entry.RawType
            ?? entry.Type switch
            {
                AddinEntryType.Application => "Application",
                AddinEntryType.DBApplication => "DBApplication",
                AddinEntryType.Command => "Command",
                _ => null,
            };
        if (typeValue is not null)
            el.SetAttributeValue("Type", typeValue);
        foreach (var attribute in entry.Attributes ?? [])
            el.Add(new XAttribute(attribute));

        string? Single(string name) =>
            EntryFieldsByTag.TryGetValue(name, out EntryField? field) ? field.Value(entry) : null;

        var multiIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        int MultiIndex(string name)
        {
            multiIndex.TryGetValue(name, out var i);
            multiIndex[name] = i + 1;
            return i;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in entry.TagOrder)
        {
            if (tag.StartsWith("<#", StringComparison.Ordinal))
            {
                if (int.TryParse(tag[2..^1], out var ui) && ui >= 0 && ui < entry.UnknownNodes.Count)
                    el.Add(Copy(entry.UnknownNodes[ui]));
                continue;
            }

            if (EntryFieldsByTag.TryGetValue(tag, out EntryField? multiField) && multiField.ListValue is { } listValues)
            {
                IReadOnlyList<string> list = listValues(entry);
                var i = MultiIndex(tag);
                if (i < list.Count)
                    el.Add(new XElement(tag, list[i]));
                continue;
            }

            var value = Single(tag);
            if (value is not null)
                el.Add(new XElement(tag, value));
            seen.Add(tag);
        }

        // Новые значения, которых не было в исходном порядке, — в конец по канону.
        foreach (var tag in CanonicalOrder)
        {
            if (seen.Contains(tag))
                continue;
            if (EntryFieldsByTag.TryGetValue(tag, out EntryField? multiField) && multiField.ListValue is { } listValues)
            {
                IReadOnlyList<string> list = listValues(entry);
                var emitted = multiIndex.GetValueOrDefault(tag);
                foreach (var v in list.Skip(emitted))
                    el.Add(new XElement(tag, v));
                continue;
            }

            var value = Single(tag);
            if (value is not null)
                el.Add(new XElement(tag, value));
        }

        return el;
    }

    private XElement WriteSettings(ManifestSettings settings)
    {
        var el = new XElement("ManifestSettings");
        foreach (var attribute in settings.Attributes ?? [])
            el.Add(new XAttribute(attribute));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in settings.TagOrder)
        {
            if (tag.StartsWith("<#", StringComparison.Ordinal))
            {
                if (int.TryParse(tag[2..^1], out var ui) && ui >= 0 && ui < settings.UnknownNodes.Count)
                    el.Add(Copy(settings.UnknownNodes[ui]));
                continue;
            }

            var value = tag switch
            {
                "UseRevitContext" => settings.UseRevitContext?.ToString(),
                "ContextName" => settings.ContextName,
                _ => null,
            };
            if (value is not null)
                el.Add(new XElement(tag, value));
            seen.Add(tag);
        }

        if (!seen.Contains("UseRevitContext") && settings.UseRevitContext.HasValue)
            el.Add(new XElement("UseRevitContext", settings.UseRevitContext.Value.ToString()));
        if (!seen.Contains("ContextName") && settings.ContextName is not null)
            el.Add(new XElement("ContextName", settings.ContextName));

        return el;
    }

    private static XName N(string local) => XName.Get(local);

    private static bool Significant(XNode node) => node switch
    {
        XText t => !string.IsNullOrWhiteSpace(t.Value),
        XElement or XComment or XProcessingInstruction or XDocumentType => true,
        _ => false,
    };

    private bool AddUnknown(List<XNode> list, XNode node)
    {
        if (!Significant(node))
            return false;

        list.Add(Copy(node));
        return true;
    }

    private XNode Copy(XNode node) => node switch
    {
        XElement e => new XElement(e),
        XComment c => new XComment(c.Value),
        XProcessingInstruction pi => new XProcessingInstruction(pi.Target, pi.Data),
        XText t => new XText(t.Value),
        XDocumentType d => new XDocumentType(d.Name, d.PublicId, d.SystemId, d.InternalSubset),
        _ => throw new AddinManifestFormatException(localizer["UnsupportedXmlNode", node.NodeType].Value),
    };
}
