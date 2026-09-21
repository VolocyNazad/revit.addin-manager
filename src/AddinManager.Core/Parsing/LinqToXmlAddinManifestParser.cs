using System.Xml;
using System.Xml.Linq;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Manifests;
using Microsoft.Extensions.Localization;

namespace AddinManager.Core.Parsing;

/// <summary>
/// LINQ to XML реализация парсера: без зависимости от Revit API.
/// Честный round-trip: декларация, порядок тегов, чужие узлы и комментарии сохраняются.
/// Сообщения об ошибках идут через <see cref="IStringLocalizer{T}"/> — они показываются
/// в UI-баннерах как есть, а resx лежат рядом с классом (нейтральный — английский).
/// </summary>
public sealed class LinqToXmlAddinManifestParser : IAddinManifestParser
{
    private readonly IStringLocalizer<LinqToXmlAddinManifestParser> _localizer;

    /// <summary>Создает парсер.</summary>
    /// <param name="localizer">Строки сообщений об ошибках.</param>
    public LinqToXmlAddinManifestParser(IStringLocalizer<LinqToXmlAddinManifestParser> localizer)
    {
        _localizer = localizer;
    }
    private static readonly string[] CanonicalOrder =
    [
        "Name", "Text", "Description", "LongDescription", "Assembly", "AddInId",
        "FullClassName", "AvailabilityClassName", "VendorId", "VendorDescription",
        "VisibilityMode", "Discipline", "LargeImage", "SmallImage", "ToolTipImage",
    ];

    /// <inheritdoc />
    public AddinManifest Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new AddinManifestFormatException(_localizer["EmptyManifest"].Value);

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            throw new AddinManifestFormatException(_localizer["BrokenXml", ex.Message].Value, ex);
        }

        var root = doc.Root;
        if (root is null || root.Name.LocalName != "RevitAddIns")
            throw new AddinManifestFormatException(_localizer["RootElementNotFound"].Value);

        var entries = new List<AddinEntry>();
        ManifestSettings? settings = null;
        var rootUnknowns = new List<XNode>();
        var seenSettings = false;

        foreach (var node in root.Nodes())
        {
            if (node is not XElement el)
            {
                AddUnknown(rootUnknowns, node);
                continue;
            }

            if (el.Name.LocalName == "AddIn")
            {
                var entry = ParseEntry(el);
                if (entries.Any(e => e.AddInId == entry.AddInId))
                    throw new AddinManifestFormatException(_localizer["DuplicateAddInId", entry.AddInId].Value);
                entries.Add(entry);
            }
            else if (el.Name.LocalName == "ManifestSettings" && !seenSettings)
            {
                settings = ParseSettings(el);
                seenSettings = true;
            }
            else
                rootUnknowns.Add(Copy(el));
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
            rootUnknowns);
    }

    /// <inheritdoc />
    public string ToXml(AddinManifest manifest)
    {
        var root = new XElement("RevitAddIns");
        foreach (var entry in manifest.Entries)
            root.Add(WriteEntry(entry));
        if (manifest.Settings is not null)
            root.Add(WriteSettings(manifest.Settings));
        foreach (var node in manifest.RootUnknownNodes)
            root.Add(Copy(node));

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
        var type = rawType?.ToLowerInvariant() switch
        {
            "application" => AddinEntryType.Application,
            "dbapplication" => AddinEntryType.DBApplication,
            "command" => AddinEntryType.Command,
            _ => AddinEntryType.Unknown,
        };

        string Req(string name)
        {
            // Пустое значение допускается: новая запись создаётся с одним AddInId и пустыми
            // Assembly/FullClassName, пока форму не заполнили. Ошибкой остаётся только
            // отсутствие элемента целиком.
            var value = el.Element(N(name))?.Value;
            if (value is null)
                throw new AddinManifestFormatException(_localizer["MissingRequiredElement", name].Value);
            return value;
        }

        var assembly = Req("Assembly");
        var guidRaw = Req("AddInId");
        if (!Guid.TryParse(guidRaw.Trim(), out var guid))
            throw new AddinManifestFormatException(_localizer["InvalidAddInId", guidRaw].Value);
        var fullClassName = Req("FullClassName");

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
            el.Element(N("Name"))?.Value,
            el.Element(N("Text"))?.Value,
            el.Element(N("Description"))?.Value,
            el.Element(N("LongDescription"))?.Value,
            assembly,
            guid,
            fullClassName,
            el.Element(N("AvailabilityClassName"))?.Value,
            el.Element(N("VendorId"))?.Value,
            el.Element(N("VendorDescription"))?.Value,
            el.Elements(N("VisibilityMode")).Select(e => e.Value).ToList(),
            el.Elements(N("Discipline")).Select(e => e.Value).ToList(),
            el.Element(N("LargeImage"))?.Value,
            el.Element(N("SmallImage"))?.Value,
            el.Element(N("ToolTipImage"))?.Value,
            order,
            unknowns);
    }

    private ManifestSettings ParseSettings(XElement el)
    {
        var raw = el.Element(N("UseRevitContext"))?.Value.Trim();
        bool? useRevitContext = raw?.ToLowerInvariant() switch
        {
            "true" => true,
            "false" => false,
            null => null,
            _ => throw new AddinManifestFormatException(_localizer["InvalidUseRevitContext", raw].Value),
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
            unknowns);
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

        string? Single(string name) => name switch
        {
            "Name" => entry.Name,
            "Text" => entry.Text,
            "Description" => entry.Description,
            "LongDescription" => entry.LongDescription,
            "Assembly" => entry.AssemblyPath,
            "AddInId" => entry.AddInId.ToString(),
            "FullClassName" => entry.FullClassName,
            "AvailabilityClassName" => entry.AvailabilityClassName,
            "VendorId" => entry.VendorId,
            "VendorDescription" => entry.VendorDescription,
            "LargeImage" => entry.LargeImage,
            "SmallImage" => entry.SmallImage,
            "ToolTipImage" => entry.ToolTipImage,
            _ => null,
        };

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

            if (tag is "VisibilityMode" or "Discipline")
            {
                var list = tag == "VisibilityMode" ? entry.VisibilityModes : entry.Disciplines;
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
            if (tag is "VisibilityMode" or "Discipline")
            {
                var list = tag == "VisibilityMode" ? entry.VisibilityModes : entry.Disciplines;
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

    private void AddUnknown(List<XNode> list, XNode node)
    {
        if (Significant(node))
            list.Add(Copy(node));
    }

    private XNode Copy(XNode node) => node switch
    {
        XElement e => new XElement(e),
        XComment c => new XComment(c.Value),
        XProcessingInstruction pi => new XProcessingInstruction(pi.Target, pi.Data),
        XText t => new XText(t.Value),
        XDocumentType d => new XDocumentType(d.Name, d.PublicId, d.SystemId, d.InternalSubset),
        _ => throw new AddinManifestFormatException(_localizer["UnsupportedXmlNode", node.NodeType].Value),
    };
}
