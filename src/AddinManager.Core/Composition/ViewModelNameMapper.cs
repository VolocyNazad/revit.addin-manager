using System.Reflection;

namespace AddinManager.Core.Composition;

/// <summary>
/// Сопоставляет типы View и ViewModel по соглашению именования:
/// <c>X.Views.FooView</c> &lt;-&gt; <c>X.ViewModels.FooViewModel</c>.
/// </summary>
public static class ViewModelNameMapper
{
    private const string ViewSuffix = "View";
    private const string ViewModelSuffix = "ViewModel";
    private const string ViewsNamespace = ".Views";
    private const string ViewModelsNamespace = ".ViewModels";

    /// <summary>Находит тип ViewModel для вида по соглашению именования. Null — если не удалось сопоставить.</summary>
    /// <param name="viewType">Тип вида (имя должно оканчиваться на View).</param>
    /// <param name="assemblies">Сборки для поиска, по порядку.</param>
    public static Type? ResolveViewModelType(Type viewType, params Assembly[] assemblies)
    {
        if (!viewType.Name.EndsWith(ViewSuffix, StringComparison.Ordinal))
            return null;

        var viewModelName = viewType.Name[..^ViewSuffix.Length] + ViewModelSuffix;
        var viewNamespace = viewType.Namespace ?? string.Empty;
        var viewModelNamespace = viewNamespace.EndsWith(ViewsNamespace, StringComparison.Ordinal)
            ? viewNamespace[..^ViewsNamespace.Length] + ViewModelsNamespace
            : viewNamespace;
        var fullName = string.IsNullOrEmpty(viewModelNamespace)
            ? viewModelName
            : viewModelNamespace + "." + viewModelName;

        foreach (var assembly in assemblies)
        {
            var match = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
            if (match is not null)
                return match;
        }

        return null;
    }

    /// <summary>Находит тип View для ViewModel по соглашению именования (обратное к <see cref="ResolveViewModelType"/>). Null — если не удалось сопоставить.</summary>
    /// <param name="viewModelType">Тип ViewModel (имя должно оканчиваться на ViewModel).</param>
    /// <param name="assemblies">Сборки для поиска, по порядку.</param>
    public static Type? ResolveViewType(Type viewModelType, params Assembly[] assemblies)
    {
        if (!viewModelType.Name.EndsWith(ViewModelSuffix, StringComparison.Ordinal))
            return null;

        var viewName = viewModelType.Name[..^ViewModelSuffix.Length] + ViewSuffix;
        var viewModelNamespace = viewModelType.Namespace ?? string.Empty;
        var viewNamespace = viewModelNamespace.EndsWith(ViewModelsNamespace, StringComparison.Ordinal)
            ? viewModelNamespace[..^ViewModelsNamespace.Length] + ViewsNamespace
            : viewModelNamespace;
        var fullName = string.IsNullOrEmpty(viewNamespace)
            ? viewName
            : viewNamespace + "." + viewName;

        foreach (var assembly in assemblies)
        {
            var match = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
            if (match is not null)
                return match;
        }

        return null;
    }
}
