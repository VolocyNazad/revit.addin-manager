namespace AddinManager.Launcher.ViewModels;

/// <summary>Раскладка главных зон. Меняется при жизни приложения.</summary>
public enum MainLayout
{
    /// <summary>Только список.</summary>
    List,

    /// <summary>Список и редактор.</summary>
    Split,

    /// <summary>Только редактор.</summary>
    Editor,
}
