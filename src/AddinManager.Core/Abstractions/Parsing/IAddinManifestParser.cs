using AddinManager.Core.Manifests;

namespace AddinManager.Core.Abstractions.Parsing;

/// <summary>
/// Абстракция парсера .addin: класс &lt;-&gt; XML в обе стороны.
/// Реализации заменяемы через DI; потребители зависят только от интерфейса.
/// </summary>
public interface IAddinManifestParser
{
    /// <exception cref="AddinManager.Core.Parsing.AddinManifestFormatException">XML не манифест.</exception>
    AddinManifest Parse(string xml);

    /// <summary>Сериализует манифест обратно в XML с сохранением неизвестного.</summary>
    string ToXml(AddinManifest manifest);
}
