namespace AddinManager.Core.Storage;

/// <summary>Откуда взят .addin файл: пользовательская или машинная папка Revit.</summary>
public enum AddinScope
{
    /// <summary><c>%APPDATA%\Autodesk\Revit\Addins\&lt;version&gt;</c>.</summary>
    User,

    /// <summary><c>%PROGRAMDATA%\Autodesk\Revit\Addins\&lt;version&gt;</c>.</summary>
    Machine,
}
