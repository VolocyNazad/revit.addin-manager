using System.IO;
using System.Reflection;
using System.Windows;
using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.Composition;
using AddinManager.Launcher.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AddinManager.Launcher;

/// <summary>Точка входа лаунчера: хост и композиция сервисов.</summary>
public partial class App
{
    private const long LogFileSizeLimitBytes = 20 * 1024 * 1024;
    private const int RetainedLogFileCountLimit = 14;
    private IHost? _host;
    private ISingleInstanceGuard? _singleInstanceGuard;

    /// <summary>Контейнер внедрения зависимостей приложения. Используется <see cref="ViewModelLocator"/> для получения View по типу ViewModel.</summary>
    public static IServiceProvider Services => ((App)Current)._host!.Services;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Volocy", "Revit.AddinManager", "logs", "launcher-.log");

        var loggerConfiguration = new LoggerConfiguration();
#if DEBUG
        loggerConfiguration.MinimumLevel.Debug();
#else
        loggerConfiguration.MinimumLevel.Information();
#endif
        Log.Logger = loggerConfiguration
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: LogFileSizeLimitBytes,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: RetainedLogFileCountLimit)
            .CreateLogger();

        try
        {
            try
            {
                _singleInstanceGuard = new SingleInstanceGuard();
            }
            catch (Exception exception)
            {
                Log.Warning(exception, "Single instance guard unavailable, starting without it");
                _singleInstanceGuard = null;
            }

            if (_singleInstanceGuard is not null && !_singleInstanceGuard.IsFirstInstance)
            {
                Log.Warning("Second Revit.AddinManager instance rejected, activating the first one");
                _singleInstanceGuard.SignalFirstInstance();
                _singleInstanceGuard.Dispose();
                _singleInstanceGuard = null;
                Shutdown(0);
                return;
            }

            Log.Information(
                "Starting Revit.AddinManager {Version}",
                Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "?");

            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices(services => services.AddLauncherServices())
                .Build();
            _host.Start();

            MainWindow = _host.Services.GetRequiredService<MainWindow>();
            if (_singleInstanceGuard is not null)
            {
                _singleInstanceGuard.AnotherInstanceStarted += OnAnotherInstanceStarted;
            }

            MainWindow.Show();

            base.OnStartup(e);
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Revit.AddinManager failed to start");
            Shutdown(-1);
        }
    }

    /// <summary>Выводит окно первого экземпляра на передний план по запросу второго.</summary>
    private void OnAnotherInstanceStarted(object? sender, EventArgs e)
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                var window = MainWindow;
                if (window is null)
                {
                    return;
                }

                if (!window.IsVisible)
                {
                    window.Show();
                }

                if (window.WindowState == WindowState.Minimized)
                {
                    window.WindowState = WindowState.Normal;
                }

                window.Activate();
            });
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Failed to activate the first Revit.AddinManager window");
        }
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Log.Information("Shutting down Revit.AddinManager");
            _host?.Dispose();
            if (_singleInstanceGuard is not null)
            {
                _singleInstanceGuard.AnotherInstanceStarted -= OnAnotherInstanceStarted;
                _singleInstanceGuard.Dispose();
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Revit.AddinManager failed to shut down cleanly");
        }
        finally
        {
            _host = null;
            _singleInstanceGuard = null;
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
