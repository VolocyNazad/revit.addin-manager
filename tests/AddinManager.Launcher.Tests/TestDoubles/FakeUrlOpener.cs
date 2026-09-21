using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.Services;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>Записывает открытые ссылки вместо реального браузера.</summary>
public sealed class FakeUrlOpener : IUrlOpener
{
    /// <summary>Ссылки, которые просили открыть.</summary>
    public List<string> OpenedUrls { get; } = [];

    /// <inheritdoc />
    public void Open(string url) => OpenedUrls.Add(url);
}
