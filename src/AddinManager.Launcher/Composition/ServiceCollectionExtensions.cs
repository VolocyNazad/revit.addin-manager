using System.Windows;
using AddinManager.Core.Composition;
using Microsoft.Extensions.DependencyInjection;

namespace AddinManager.Launcher.Composition;

/// <summary>
/// Модуль вида: регистрирует вид вместе с его ViewModel (имя по <see cref="ViewModelNameMapper"/>)
/// и ставит ViewModel в DataContext при создании. Вид создаётся через <see cref="ActivatorUtilities"/>,
/// поэтому у него может быть конструктор с любыми зависимостями, разрешаемыми контейнером
/// (а не только конструктор без параметров).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Регистрирует модуль вида как Singleton.</summary>
    /// <typeparam name="TView">Вид.</typeparam>
    public static IServiceCollection AddModule<TView>(this IServiceCollection services)
        where TView : FrameworkElement
    {
        var viewModel = ViewModelNameMapper.ResolveViewModelType(
                typeof(TView),
                typeof(TView).Assembly)
            ?? throw new InvalidOperationException($"No view model found for view {typeof(TView).FullName}.");
        services.AddSingleton(viewModel);
        services.AddSingleton(provider =>
        {
            var view = (TView)ActivatorUtilities.CreateInstance(provider, typeof(TView));
            view.DataContext = provider.GetRequiredService(viewModel);
            return view;
        });
        return services;
    }

    /// <summary>
    /// Регистрирует диалог: transient ViewModel, собираемый контейнером, и свой вид поверх неё.
    /// Закрытое окно нельзя показать повторно, поэтому вид и модель создаются на каждый вызов,
    /// а фабрика вида — Singleton (в неё попадает уже готовый экземпляр).
    /// </summary>
    /// <typeparam name="TViewModel">Модель диалога.</typeparam>
    /// <typeparam name="TView">Окно диалога с конструктором без параметров.</typeparam>
    public static IServiceCollection AddDialog<TViewModel, TView>(this IServiceCollection services)
        where TViewModel : class
        where TView : Window, new()
    {
        services.AddTransient<TViewModel>();
        services.AddTransient(provider =>
        {
            var view = new TView
            {
                DataContext = provider.GetRequiredService<TViewModel>(),
            };
            return view;
        });
        services.AddSingleton<Func<TView>>(provider => () => provider.GetRequiredService<TView>());
        return services;
    }
}
