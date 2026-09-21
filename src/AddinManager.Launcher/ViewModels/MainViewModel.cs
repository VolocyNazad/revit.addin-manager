using AddinManager.Core.Abstractions.Guard;
using AddinManager.Core.Guard;
using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.Services;
using AddinManager.Localization;
using AddinManager.Localization.Abstractions;
using AddinManager.Theming;
using AddinManager.Theming.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Localization;

namespace AddinManager.Launcher.ViewModels;

/// <summary>
/// Состояние главного окна: раскладка, тема и язык. Зонные ViewModel (Toolbar/List/Editor/...) в композицию
/// главного окна не входят — каждый вид получает свою ViewModel напрямую через DependencyInjection/ViewModelLocator.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>Сколько висит тост-подтверждение смены темы/языка.</summary>
    private static readonly TimeSpan ToastLifetime = TimeSpan.FromSeconds(2.5);

#pragma warning disable S1075 // Намеренные ссылки на GitHub-страницы приложения.
    /// <summary>Страница issues репозитория — куда ведёт кнопка "Поддержка".</summary>
    private const string SupportUrl = "https://github.com/VolocyNazad/revit.addin-manager/issues";

    /// <summary>Страница спонсорства — куда ведёт кнопка "Спонсорство".</summary>
    private const string SponsorUrl = "https://github.com/sponsors/VolocyNazad";
#pragma warning restore S1075

    private readonly IThemeService _themeService;
    private readonly ILocalizationService _localizationService;
    private readonly IStringLocalizer<MainViewModel> _localizer;
    private readonly IUiDispatcher _dispatcher;
    private readonly IRevitProcessGuard _guard;
    private readonly IDialogService _dialogService;
    private readonly IUpdateChecker _updateChecker;
    private readonly IUrlOpener _urlOpener;
    private CancellationTokenSource? _toastLifetime;
    private int _toastGeneration;

    [ObservableProperty]
    private MainLayout _mainLayout = MainLayout.Split;

    [ObservableProperty]
    private double _listFraction = 0.30;

    [ObservableProperty]
    private string? _toastMessage;

    [ObservableProperty]
    private bool _isToastVisible;

    /// <summary>Revit запущен прямо сейчас (по последнему опросу сторожа).</summary>
    [ObservableProperty]
    private bool _isRevitRunning;

    /// <summary>Предупреждение о запущенном Revit скрыто крестиком до его закрытия.</summary>
    [ObservableProperty]
    private bool _isRevitWarningDismissed;

    partial void OnListFractionChanged(double value)
    {
        var clamped = double.IsNaN(value) ? 0.6 : Math.Clamp(value, 0.1, 0.9);
        if (Math.Abs(clamped - value) > 0.0001 || double.IsNaN(value))
            ListFraction = clamped;
    }

    /// <summary>Создает модель.</summary>
    /// <param name="themeService">Сервис темы.</param>
    /// <param name="localizationService">Сервис языка.</param>
    /// <param name="localizer">Строки шапки окна.</param>
    /// <param name="dispatcher">Маршалинг гашения тоста и событий сторожа в поток UI.</param>
    /// <param name="guard">Сторож запущенного Revit — баннер сам появляется/исчезает.</param>
    /// <param name="toastService">Шина тостов — чужие просьбы показываем как свои.</param>
    /// <param name="dialogService">Диалог "доступно обновление".</param>
    /// <param name="updateChecker">Проверка новой версии.</param>
    /// <param name="urlOpener">Открытие ссылок поддержки/скачивания в браузере.</param>
    public MainViewModel(
        IThemeService themeService,
        ILocalizationService localizationService,
        IStringLocalizer<MainViewModel> localizer,
        IUiDispatcher dispatcher,
        IRevitProcessGuard guard,
        IToastService toastService,
        IDialogService dialogService,
        IUpdateChecker updateChecker,
        IUrlOpener urlOpener)
    {
        _themeService = themeService;
        _localizationService = localizationService;
        _localizer = localizer;
        _dispatcher = dispatcher;
        _guard = guard;
        _dialogService = dialogService;
        _updateChecker = updateChecker;
        _urlOpener = urlOpener;
        _localizationService.LanguageChanged += (_, _) => RefreshSnapshot();
        // Событие сторожа может прийти не из потока UI (таймер опроса) — маршалим так же,
        // как ListViewModel маршалит FileSystemWatcher.
        _guard.Changed += (_, _) => _dispatcher.Invoke(SyncRevitState);
        _guard.Start();
        SyncRevitState();
        toastService.ToastRequested += (_, e) => ShowToast(e.Message);
    }

    /// <summary>Переключить раскладку главных зон.</summary>
    [RelayCommand]
    public void SetMainLayout(MainLayout layout) => MainLayout = layout;

    /// <summary>Выбранная тема оформления.</summary>
    public AppTheme AppTheme
    {
        get => _themeService.Theme;
        set
        {
            _themeService.SetTheme(value);
            RefreshSnapshot();
        }
    }

    /// <summary>Круговая смена темы: система, светлая, темная.</summary>
    [RelayCommand]
    public void CycleTheme()
    {
        AppTheme = AppTheme switch
        {
            AppTheme.System => AppTheme.Light,
            AppTheme.Light => AppTheme.Dark,
            _ => AppTheme.System,
        };
        ShowToast(ThemeButtonTooltip);
    }

    /// <summary>Выбранный язык интерфейса.</summary>
    public AppLanguage AppLanguage
    {
        get => _localizationService.Language;
        set
        {
            _localizationService.SetLanguage(value);
            RefreshSnapshot();
        }
    }

    /// <summary>Круговая смена языка: система, русский, английский.</summary>
    [RelayCommand]
    public void CycleLanguage()
    {
        AppLanguage = AppLanguage switch
        {
            AppLanguage.System => AppLanguage.Russian,
            AppLanguage.Russian => AppLanguage.English,
            _ => AppLanguage.System,
        };
        ShowToast(LanguageButtonTooltip);
    }

    /// <summary>Тултип кнопки раскладки "только список".</summary>
    public string ListOnlyTooltip => _localizer["Layout_ListOnlyTooltip"];

    /// <summary>Тултип кнопки раскладки "список и редактор".</summary>
    public string SplitTooltip => _localizer["Layout_SplitTooltip"];

    /// <summary>Тултип кнопки раскладки "только редактор".</summary>
    public string EditorOnlyTooltip => _localizer["Layout_EditorOnlyTooltip"];

    /// <summary>Тултип кнопки темы ("Тема: ...").</summary>
    public string ThemeButtonTooltip => string.Format(_localizer["ThemeButtonTooltip"], ThemeDisplayName(AppTheme));

    /// <summary>Тултип кнопки языка ("Язык: ...").</summary>
    public string LanguageButtonTooltip =>
        string.Format(_localizer["LanguageButtonTooltip"], LanguageDisplayName(AppLanguage));

    /// <summary>Тултип кнопки проверки обновлений.</summary>
    public string UpdateButtonTooltip => _localizer["UpdateButtonTooltip"];

    /// <summary>Тултип кнопки поддержки.</summary>
    public string SupportButtonTooltip => _localizer["SupportButtonTooltip"];

    /// <summary>Тултип кнопки спонсорства.</summary>
    public string SponsorButtonTooltip => _localizer["SponsorButtonTooltip"];

    private string ThemeDisplayName(AppTheme theme) => _localizer[$"ThemeName_{theme}"];

    private string LanguageDisplayName(AppLanguage language) => _localizer[$"LanguageName_{language}"];

    /// <summary>
    /// Показывает тост и гасит через <see cref="ToastLifetime"/>: повторный показ отменяет
    /// предыдущий таймер, поколение страхует от гашения нового тоста старым.
    /// </summary>
    private void ShowToast(string message)
    {
        _toastLifetime?.Cancel();
        _toastLifetime?.Dispose();
        ToastMessage = message;
        IsToastVisible = true;

        var lifetime = _toastLifetime = new CancellationTokenSource();
        var generation = ++_toastGeneration;
        _ = HideToastAfterDelayAsync(lifetime.Token, generation);
    }

    private async Task HideToastAfterDelayAsync(CancellationToken token, int generation)
    {
        try
        {
            await Task.Delay(ToastLifetime, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (generation == _toastGeneration)
            _dispatcher.Invoke(() => IsToastVisible = false);
    }

    /// <summary>Текст висящего предупреждения о запущенном Revit, со списком версий.</summary>
    public string RevitWarningText =>
        string.Format(_localizer["RevitRunningWarning"], string.Join(", ", _guard.RunningVersions.OrderBy(version => version, StringComparer.Ordinal)));

    /// <summary>Тултип крестика предупреждения.</summary>
    public string DismissRevitWarningTooltip => _localizer["RevitWarning_DismissTooltip"];

    /// <summary>Показывать предупреждение: Revit запущен и его не скрывали крестиком.</summary>
    public bool ShowRevitWarning => IsRevitRunning && !IsRevitWarningDismissed;

    /// <summary>Скрыть предупреждение до закрытия Revit (следующий запуск покажет снова).</summary>
    [RelayCommand]
    public void DismissRevitWarning() => IsRevitWarningDismissed = true;

    /// <summary>Внеочередной опрос сторожа (фокус окна).</summary>
    public void RefreshRevitGuard() => _guard.CheckNow();

    /// <summary>
    /// Проверяет новую версию: есть обновление — диалог с предложением скачать, нет — тост
    /// "актуально", ошибка сети/GitHub — тост с причиной.
    /// </summary>
    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        var result = await _updateChecker.CheckAsync(CancellationToken.None);

        if (result.Error is not null)
        {
            ShowToast(string.Format(_localizer["UpdateCheckError"], result.Error));
            return;
        }

        if (result.HasUpdate)
        {
            var message = string.Format(
                _localizer["UpdateAvailableMessage"], result.LatestVersion, result.CurrentVersion);
            _dialogService.PromptUpdate(message, result.DownloadUrl!);
        }
        else
        {
            ShowToast(_localizer["UpdateUpToDate"]);
        }
    }

    /// <summary>Открывает страницу issues репозитория (помощь и баги) в браузере.</summary>
    [RelayCommand]
    private void OpenSupport() => _urlOpener.Open(SupportUrl);

    /// <summary>Открывает страницу спонсорства в браузере.</summary>
    [RelayCommand]
    private void OpenSponsor() => _urlOpener.Open(SponsorUrl);

    partial void OnIsRevitRunningChanged(bool value) => RefreshSnapshot();

    partial void OnIsRevitWarningDismissedChanged(bool value) => RefreshSnapshot();

    private void SyncRevitState()
    {
        IsRevitRunning = _guard.IsRunning;
        if (!IsRevitRunning)
            IsRevitWarningDismissed = false;
    }

    private void RefreshSnapshot() => OnPropertyChanged((string?)null);

    /// <inheritdoc />
    public override string ToString() => $"Main(Layout={MainLayout}, Split={ListFraction:0.##}, Theme={AppTheme})";
}
