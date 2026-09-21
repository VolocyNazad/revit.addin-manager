namespace AddinManager.Launcher.ViewModels;

/// <summary>Порядок сортировки строк списка (<see cref="ListViewModel.Files"/>).</summary>
public enum ListSortOrder
{
    /// <summary>По имени файла.</summary>
    Name,

    /// <summary>По scope, затем по имени.</summary>
    Scope,

    /// <summary>По версии, затем по имени.</summary>
    Version,
}
