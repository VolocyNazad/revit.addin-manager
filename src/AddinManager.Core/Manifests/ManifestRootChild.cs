namespace AddinManager.Core.Manifests;

/// <summary>
/// Порядок дочерних элементов корня <c>RevitAddIns</c>: записи, блок настроек и незнакомые узлы
/// могут идти вперемешку (комментарии между записями, чужие теги). Токен не хранит индексы —
/// при записи записи и неизвестные узлы берутся по очереди, поэтому новые записи/узлы
/// дописываются в конец, а правка существующей записи сохраняет её место.
/// </summary>
public enum ManifestRootChild
{
    /// <summary>Очередной элемент <c>AddIn</c> из <see cref="AddinManifest.Entries"/>.</summary>
    Entry,

    /// <summary>Блок <c>ManifestSettings</c>, если он есть.</summary>
    Settings,

    /// <summary>Очередной незнакомый узел из <see cref="AddinManifest.RootUnknownNodes"/>.</summary>
    Unknown,
}
