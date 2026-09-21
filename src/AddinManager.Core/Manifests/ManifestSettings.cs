using System.Xml.Linq;

namespace AddinManager.Core.Manifests;

/// <summary>Блок ManifestSettings уровня файла: изоляция контекста загрузки (2026+).</summary>
public sealed record ManifestSettings(
    bool? UseRevitContext,
    string? ContextName,
    IReadOnlyList<string> TagOrder,
    IReadOnlyList<XNode> UnknownNodes);
