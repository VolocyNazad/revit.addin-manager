using System.Globalization;
using System.Net.Http;
using AddinManager.Core.Abstractions.Guard;
using AddinManager.Core.Abstractions.Manifests;
using AddinManager.Core.Abstractions.Parsing;
using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Guard;
using AddinManager.Core.Manifests;
using AddinManager.Core.Parsing;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Abstractions.Composition;
using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.Services;
using AddinManager.Launcher.ViewModels;
using AddinManager.Launcher.ViewModels.Dialogs;
using AddinManager.Launcher.Views;
using AddinManager.Launcher.Views.Dialogs;
using AddinManager.Localization;
using AddinManager.Localization.Abstractions;
using AddinManager.Theming;
using AddinManager.Theming.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AddinManager.Launcher.Composition;

/// <summary>
/// Корень композиции лаунчера: собирает граф сервисов, зон и диалогов. Вынесен из
/// <see cref="App.OnStartup"/>, чтобы точка входа занималась только жизненным циклом приложения.
/// </summary>
public static class CompositionRoot
{
    /// <summary>Регистрирует весь граф лаунчера: Core-сервисы, UI-сервисы, зоны, диалоги и главное окно.</summary>
    /// <param name="services">Коллекция сервисов хоста.</param>
    /// <returns>Та же коллекция для цепочки вызовов.</returns>
    public static IServiceCollection AddLauncherServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Локализация
        // remark: resx лежат рядом с классами: пустой ResourcesPath оставляет базовым именем
        // полное имя типа — IStringLocalizer{Class} резолвится без соглашений.
        services.AddLocalization(options => options.ResourcesPath = string.Empty);
        var localizationService = new LocalizationService(() => CultureInfo.CurrentUICulture);
        localizationService.ApplyCurrentCulture();
        services.AddSingleton<ILocalizationService>(localizationService);
        // Темы
        services.AddSingleton<IThemeService>(_ => new ThemeService(ThemeHelper.IsSystemDark));
        services.AddSingleton<IAddinManifestParser, LinqToXmlAddinManifestParser>();
        services.AddSingleton<IFileRecycler, ShellFileRecycler>();
        services.AddSingleton<IAddinStore, FileSystemAddinStore>();
        services.AddSingleton<IAddinMarkupService, FileAddinMarkupService>();
        services.AddSingleton<IManifestSchema, RevitManifestSchema>();
        // Каталог строк списка: сканирование диска, пересоздание строк и кросс-файловые
        // предупреждения; зона списка получает его как один совместный сервис.
        services.AddSingleton<FileCatalog>();
        // Слежение за внешними изменениями .addin-файлов (см. ListViewModel) — singleton,
        // как и остальные абстракции диска здесь: один живой FileSystemWatcher на всё
        // приложение, останавливается вместе с ним через Dispose (host.Dispose() ниже
        // распускает все зарегистрированные IDisposable-синглтоны).
        services.AddSingleton<IAddinChangeWatcher, FileSystemAddinChangeWatcher>();
        // Сторож запущенного Revit (см. MainViewModel): один опрос на приложение, тоже
        // останавливается через Dispose хоста.
        services.AddSingleton<IRevitProcessGuard>(_ => new PollingRevitProcessGuard(RevitProccesesHelper.DetectRunningRevitVersions));
        services.AddSingleton<IUiDispatcher, WpfDispatcher>();
        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<IClipboardService, WpfClipboardService>();
        services.AddSingleton<IEditorDirtyState, EditorDirtyState>();
        services.AddSingleton<IUndoService, UndoService>();
        services.AddSingleton<IFileOperations, FileOperations>();
        services.AddSingleton<IUrlOpener, UrlOpener>();
        services.AddSingleton<IFolderOpener, ExplorerFolderOpener>();
        services.AddSingleton(_ =>
        {
            // GitHub API требует User-Agent, иначе отвечает 403.
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Revit.AddinManager");
            return client;
        });
        services.AddSingleton<IUpdateChecker, GitHubUpdateChecker>();
        services.AddSingleton<IAddinFileRowViewModelFactory, AddinFileRowViewModelFactory>();
        services.AddSingleton<IAddinEntryRowViewModelFactory, AddinEntryRowViewModelFactory>();
        services.AddSingleton<ISelectableOptionViewModelFactory, SelectableOptionViewModelFactory>();
        services.AddSingleton<MarkupSearchViewModel>();
        services.AddModule<ToolbarView>();
        services.AddModule<ListView>();
        services.AddModule<EntriesView>();
        services.AddModule<FormView>();
        services.AddModule<MarkupView>();
        services.AddModule<ManifestSettingsView>();
        services.AddModule<EditorView>();
        // Выбор и каталог зон — контракты, имплементированные самими зонами
        // (см. docs/architecture.md, раздел MVVM): те же singleton-экземпляры,
        // что созданы выше через AddModule, только под узким срезом.
        services.AddSingleton<IFileSelection>(provider => provider.GetRequiredService<ListViewModel>());
        services.AddSingleton<IEntrySelection>(provider => provider.GetRequiredService<EntriesViewModel>());
        services.AddSingleton<IAddinFileCatalog>(provider => provider.GetRequiredService<ListViewModel>());
        // Роутер сохранения (см. EditorViewModel.SaveActive): режим редактора -> видимая
        // панель с сохранением. Ленивый Func, как фабрики диалогов ниже: сами панели
        // резолвятся только в момент сохранения, циклов в DI нет.
        services.AddSingleton<Func<EditorMode, ISavablePane?>>(provider => mode => mode switch
        {
            EditorMode.Form or EditorMode.EntriesForm => provider.GetRequiredService<FormViewModel>(),
            EditorMode.Markup or EditorMode.EntriesMarkup => provider.GetRequiredService<MarkupViewModel>(),
            EditorMode.Settings => provider.GetRequiredService<ManifestSettingsViewModel>(),
            _ => null,
        });
        // Диалоги — НЕ синглтоны: закрытое окно нельзя показать повторно, каждый вызов
        // собирает свежие вид+модель. Фабрики живут в корне композиции.
        services.AddDialog<ConfirmDialogViewModel, ConfirmDialogView>();
        services.AddDialog<AddFileDialogViewModel, AddFileDialogView>();
        services.AddDialog<AddEntryDialogViewModel, AddEntryDialogView>();
        services.AddDialog<UpdateDialogViewModel, UpdateDialogView>();
        services.AddDialog<MoveCopyDialogViewModel, MoveCopyDialogView>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        return services;
    }
}
