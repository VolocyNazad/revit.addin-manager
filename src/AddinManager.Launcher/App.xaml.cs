using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
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
using AddinManager.Launcher.Composition;
using AddinManager.Launcher.Services;
using AddinManager.Launcher.ViewModels;
using AddinManager.Launcher.Views;
using AddinManager.Localization;
using AddinManager.Localization.Abstractions;
using AddinManager.Theming;
using AddinManager.Theming.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AddinManager.Launcher;

/// <summary>Точка входа лаунчера: хост и композиция сервисов.</summary>
public partial class App
{
    private IHost? _host;

    /// <summary>Контейнер внедрения зависимостей приложения. Используется <see cref="ViewModelLocator"/> для получения View по типу ViewModel.</summary>
    public static IServiceProvider Services => ((App)Current)._host!.Services;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Volocy", "Revit.AddinManager", "logs", "launcher-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Information(
            "Starting Revit.AddinManager {Version}",
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "?");

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(services =>
            {
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
                services.AddSingleton<IAddinStore, FileSystemAddinStore>();
                services.AddSingleton<IAddinMarkupService, FileAddinMarkupService>();
                services.AddSingleton<IManifestSchema, RevitManifestSchema>();
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
                // Диалог подтверждения — НЕ синглтон: закрытое окно нельзя показать повторно,
                // каждый Confirm собирает свежие вид+модель. Фабрика — в корне композиции.
                services.AddTransient<ConfirmDialogViewModel>();
                services.AddTransient(provider =>
                {
                    var view = new ConfirmDialogView
                    {
                        DataContext = provider.GetRequiredService<ConfirmDialogViewModel>()
                    };
                    return view;
                });
                services.AddSingleton<Func<ConfirmDialogView>>(
                    provider => () => provider.GetRequiredService<ConfirmDialogView>());
                services.AddTransient<AddFileDialogViewModel>();
                services.AddTransient(provider =>
                {
                    var view = new AddFileDialogView
                    {
                        DataContext = provider.GetRequiredService<AddFileDialogViewModel>()
                    };
                    return view;
                });
                services.AddSingleton<Func<AddFileDialogView>>(
                    provider => () => provider.GetRequiredService<AddFileDialogView>());
                services.AddTransient<AddEntryDialogViewModel>();
                services.AddTransient(provider =>
                {
                    var view = new AddEntryDialogView
                    {
                        DataContext = provider.GetRequiredService<AddEntryDialogViewModel>()
                    };
                    return view;
                });
                services.AddSingleton<Func<AddEntryDialogView>>(
                    provider => () => provider.GetRequiredService<AddEntryDialogView>());
                services.AddTransient<UpdateDialogViewModel>();
                services.AddTransient(provider =>
                {
                    var view = new UpdateDialogView
                    {
                        DataContext = provider.GetRequiredService<UpdateDialogViewModel>()
                    };
                    return view;
                });
                services.AddSingleton<Func<UpdateDialogView>>(
                    provider => () => provider.GetRequiredService<UpdateDialogView>());
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
        _host.Start();

        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();

        base.OnStartup(e);
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Shutting down Revit.AddinManager");
        _host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
