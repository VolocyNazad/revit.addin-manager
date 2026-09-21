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
}
