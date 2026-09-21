using System.Windows;
using System.Windows.Markup;
using AddinManager.Core.Composition;
using Microsoft.Extensions.DependencyInjection;

namespace AddinManager.Launcher.Composition;

/// <summary>
/// XAML-расширение разметки: по типу ViewModel находит соответствующий тип View
/// (по соглашению <see cref="ViewModelNameMapper"/>) и возвращает его singleton-экземпляр из контейнера внедрения зависимостей.
/// Пример: <c>Content="{composition:ViewModelLocator {x:Type viewmodels:ToolbarViewModel}}"</c>.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class ViewModelLocator : MarkupExtension
{
    /// <summary>Тип ViewModel, для которой нужно получить View.</summary>
    [ConstructorArgument("viewModelType")]
    public Type? ViewModelType { get; set; }

    /// <summary>Создает расширение без параметра (тип задается через свойство <see cref="ViewModelType"/>).</summary>
    public ViewModelLocator()
    {
    }

    /// <summary>Создает расширение с типом ViewModel, переданным позиционным аргументом в XAML.</summary>
    /// <param name="viewModelType">Тип ViewModel.</param>
    public ViewModelLocator(Type viewModelType)
    {
        ViewModelType = viewModelType;
    }

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (ViewModelType is null)
            throw new InvalidOperationException($"{nameof(ViewModelType)} is not set.");

        var viewType = ViewModelNameMapper.ResolveViewType(ViewModelType, ViewModelType.Assembly)
            ?? throw new InvalidOperationException($"No view found for view model {ViewModelType.FullName}.");

        // Окно разметки: приложения (а с ним и контейнера) нет — создаём вид напрямую, чтобы
        // были видны вложенные панели со своими d:DataContext-данными. Не вышло — пусто вместо
        // падения: дизайнер ронять нельзя. В рантайме Current всегда наш App, сюда не заходим.
        if (Application.Current is not App)
        {
            try
            {
                return Activator.CreateInstance(viewType)!;
            }
            catch (Exception)
            {
                return DependencyProperty.UnsetValue;
            }
        }

        return App.Services.GetRequiredService(viewType);
    }
}
