using AddinManager.Core.Abstractions.Guard;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.Composition;
using AddinManager.Launcher.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AddinManager.Launcher.Tests.Composition;

/// <summary>
/// Корень композиции собирается вне <see cref="App"/> и отдаёт реальные реализации — smoke,
/// который ловит забытую регистрацию раньше запуска приложения.
/// </summary>
public sealed class CompositionRootTests
{
    [Fact]
    public void AddLauncherServices_ResolvesCoreAndUiServices()
    {
        using var provider = BuildProvider();

        Assert.IsType<FileSystemAddinStore>(provider.GetRequiredService<IAddinStore>());
        Assert.IsType<LinqToXmlAddinManifestParser>(provider.GetRequiredService<IAddinManifestParser>());
        Assert.IsType<UndoService>(provider.GetRequiredService<IUndoService>());
        Assert.IsType<FileCatalog>(provider.GetRequiredService<FileCatalog>());
        Assert.IsType<EditorDirtyState>(provider.GetRequiredService<IEditorDirtyState>());
        Assert.NotNull(provider.GetRequiredService<IFileRecycler>());
        Assert.NotNull(provider.GetRequiredService<IRevitProcessGuard>());
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLauncherServices();
        return services.BuildServiceProvider();
    }
}
