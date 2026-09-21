namespace AddinManager.Launcher.ViewModels;

/// <summary>Фильтр списка по scope — поверх <see cref="Core.Storage.AddinScope"/>.</summary>
public enum ScopeFilter
{
    /// <summary>Без фильтра.</summary>
    All,

    /// <summary>Только <see cref="Core.Storage.AddinScope.User"/>.</summary>
    User,

    /// <summary>Только <see cref="Core.Storage.AddinScope.Machine"/>.</summary>
    Machine,
}
