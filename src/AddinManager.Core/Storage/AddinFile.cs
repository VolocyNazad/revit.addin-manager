using AddinManager.Core.Manifests;

namespace AddinManager.Core.Storage;

/// <summary>
/// Один .addin файл на диске: единица активации (план, раздел 2). Идентичность —
/// <c>(FileName, Scope, Version)</c>; <see cref="Enabled"/> — файл лежит в корне
/// версии, а не в <c>disabled/</c>.
/// </summary>
public sealed record AddinFile(
    string FileName,
    AddinScope Scope,
    string Version,
    bool Enabled,
    string FullPath,
    string VersionRootDirectory,
    AddinManifest Manifest);
