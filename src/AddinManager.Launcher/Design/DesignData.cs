#pragma warning disable CS1591 // Дизайн-данные для окна разметки: имена членов повторяют биндинги видов, описывать нечего.

using System.Windows.Input;
using AddinManager.Core.Manifests;
using AddinManager.Core.Storage;
using AddinManager.Launcher.ViewModels;

namespace AddinManager.Launcher.Design;

// Сэмплы для d:DataContext видов: окно разметки показывает виды с этими данными вместо
// пустых привязок. Только дизайнер (d: игнорируется в рантайме), живой код сюда не ходит.

public sealed class DesignMainViewModel
{
    public string ListOnlyTooltip { get; set; } = "Только список";
    public string SplitTooltip { get; set; } = "Список и редактор";
    public string EditorOnlyTooltip { get; set; } = "Только редактор";
    public string ThemeButtonTooltip { get; set; } = "Тема: Тёмная";
    public string LanguageButtonTooltip { get; set; } = "Язык: Русский";
    public string RevitWarningText { get; set; } = "Revit запущен (2025) — изменения .addin применятся после его перезапуска.";
    public string DismissRevitWarningTooltip { get; set; } = "Скрыть";
    public string ToastMessage { get; set; } = "Тема: Тёмная";
    public bool IsToastVisible { get; set; } = true;
    public bool ShowRevitWarning { get; set; } = true;
    public ICommand? SetMainLayoutCommand { get; set; }
    public ICommand? CycleThemeCommand { get; set; }
    public ICommand? CycleLanguageCommand { get; set; }
    public ICommand? DismissRevitWarningCommand { get; set; }
}

public sealed class DesignListViewModel
{
    public string SearchText { get; set; } = string.Empty;
    public string SearchPlaceholder { get; set; } = "Поиск по имени";
    public string RefreshTooltip { get; set; } = "Обновить (перечитать файлы с диска)";
    public ScopeFilter ScopeFilter { get; set; } = ScopeFilter.All;
    public string ScopeAllLabel { get; set; } = "Все";
    public string? VersionFilter { get; set; }
    public string VersionAllLabel { get; set; } = "Все версии";
    public ListSortOrder SortOrder { get; set; } = ListSortOrder.Name;
    public string SortLabel { get; set; } = "Сортировка:";
    public string SortNameLabel { get; set; } = "Name";
    public string SortVersionLabel { get; set; } = "Version";
    public string GroupLabel { get; set; } = "Группировать:";
    public string GroupVersionLabel { get; set; } = "Version";
    public bool GroupByScope { get; set; }
    public bool GroupByVersion { get; set; }
    public DesignFileRow? SelectedFile { get; set; } = new();
    public List<DesignFileRow> FilesView { get; } = [new(), new() { FileName = "Enscape.addin" }];
    public int SelectedCount { get; set; } = 1;
    public bool HasSelection { get; set; } = true;
    public string SelectionSummary { get; set; } = "Выбрано: 1";
    public string ShowInFolderLabel { get; set; } = "Показать в папке";
    public string ToggleSelectedLabel { get; set; } = "Выключить";
    public string DeleteSelectedLabel { get; set; } = "Удалить";
    public string SelectAllToggleLabel { get; set; } = "Снять выбор";
    public ICommand? RefreshManualCommand { get; set; }
    public ICommand? SetScopeFilterCommand { get; set; }
    public ICommand? SetVersionFilterCommand { get; set; }
    public ICommand? SetSortOrderCommand { get; set; }
    public ICommand? ToggleSelectedCommand { get; set; }
    public ICommand? ShowInFolderCommand { get; set; }
    public ICommand? DeleteSelectedCommand { get; set; }
    public ICommand? ToggleSelectAllCommand { get; set; }
}

public sealed class DesignFileRow
{
    public string FileName { get; set; } = "pyRevit.addin";
    public string MetaLine { get; set; } = "2025 · User · Application + Command · 2 entries";
    public string VendorSubtitle { get; set; } = "pyRevitLabs • RAD environment";
    public bool IsEnabled { get; set; } = true;
    public bool IsSelected { get; set; }
    public bool IsLocked { get; set; }
    public string? ErrorMessage { get; set; }
    public string? WarningMessage { get; set; } = "Повторяющийся AddInId внутри файла";
    public string WarningTooltipTitle { get; set; } = "Предупреждение";
    public string FilePathLabel { get; set; } = "Path";
    public string FullPath { get; set; } = @"C:\Users\you\AppData\Roaming\Autodesk\Revit\Addins\2025\pyRevit.addin";
    public string FileVersionLabel { get; set; } = "Version";
    public string Version { get; set; } = "2025";
    public string FileScopeLabel { get; set; } = "Scope";
    public AddinScope Scope { get; set; } = AddinScope.User;
    public string FileTypeLabel { get; set; } = "Type";
    public string TypeSummary { get; set; } = "Application + Command";
    public string FileVendorLabel { get; set; } = "Vendor";
    public string FileContentsHeader { get; set; } = "Contents";
    public List<string> EntrySummaries { get; } = ["Application — pyRevit", "Command — Открыть журнал"];
}

public sealed class DesignEntriesViewModel
{
    public string EmptySelectionPrompt { get; set; } = "Выберите файл в списке слева";
    public DesignFileRow? SelectedFile { get; set; } = new();
    public List<DesignEntryRow> Entries { get; } = [new(), new() { DisplayName = "Открыть журнал" }];
    public DesignEntryRow? SelectedEntry { get; set; }
    public string DiscardButtonLabel { get; set; } = "Отменить";
    public string SaveButtonLabel { get; set; } = "Сохранить";
    public int SelectedCount { get; set; } = 1;
    public bool HasSelection { get; set; } = true;
    public string SelectionSummary { get; set; } = "Выбрано: 1";
    public string SelectAllToggleLabel { get; set; } = "Снять выбор";
    public string DeleteSelectedLabel { get; set; } = "Удалить";
    public ICommand? ToggleSelectAllCommand { get; set; }
    public ICommand? DeleteSelectedCommand { get; set; }

    public DesignEntriesViewModel() => SelectedEntry = Entries[0];
}

public sealed class DesignEntryData
{
    public string AssemblyPath { get; set; } = @"C:\Users\you\AppData\Roaming\Autodesk\Revit\Addins\2025\pyRevit\pyRevit.dll";
    public string FullClassName { get; set; } = "pyRevit.Entry";
    public string Description { get; set; } = "RAD environment for Autodesk Revit";
}

public sealed class DesignEntryRow
{
    public string DisplayName { get; set; } = "pyRevit";
    public bool IsSelected { get; set; }
    public AddinEntryType Type { get; set; } = AddinEntryType.Application;
    public string VendorSubtitle { get; set; } = "pyRevitLabs";
    public string AddInIdText { get; set; } = "xxxxxxxx-0001";
    public string? WarningMessage { get; set; } = "Повторяющийся AddInId внутри файла";
    public string WarningTooltipTitle { get; set; } = "Предупреждение";
    public string TypeLabel { get; set; } = "Type";
    public string AssemblyLabel { get; set; } = "Assembly";
    public string ClassLabel { get; set; } = "FullClassName";
    public string VendorLabel { get; set; } = "Vendor";
    public string DescriptionLabel { get; set; } = "Description";
    public DesignEntryData Entry { get; } = new();
    public string VisibilityModesText { get; set; } = "AlwaysVisible";
    public string DisciplinesText { get; set; } = "Architecture";
}

public sealed class DesignFormViewModel
{
    public string EmptySelectionPrompt { get; set; } = "Выберите запись в списке слева";
    public object? SelectedEntry { get; set; } = new();
    public DesignFileRow? SelectedFile { get; set; } = new();
    public AddinEntryType EntryType { get; set; } = AddinEntryType.Command;
    public string EntryTypeLabel { get; set; } = "Type";
    public string NameLabel { get; set; } = "Name";
    public string ButtonTextLabel { get; set; } = "Text";
    public string DescriptionLabel { get; set; } = "Description";
    public string LongDescriptionLabel { get; set; } = "LongDescription";
    public string GenerateButtonLabel { get; set; } = "Сгенерировать";
    public string VisibilityModesLabel { get; set; } = "VisibilityMode";
    public string DisciplinesLabel { get; set; } = "Discipline";
    public string Text { get; set; } = "Открыть журнал";
    public string Description { get; set; } = "Открывает журнал выполнения скриптов";
    public string LongDescription { get; set; } = "Показывает окно журнала с выводом последнего скрипта";
    public string AssemblyPath { get; set; } = @"C:\Users\you\AppData\Roaming\Autodesk\Revit\Addins\2025\pyRevit\pyRevit.dll";
    public string AddInIdText { get; set; } = "xxxxxxxx-xxxx-xxxx-xxxx-000000000002";
    public string FullClassName { get; set; } = "pyRevit.CmdOpenLog";
    public string AvailabilityClassName { get; set; } = "pyRevit.CmdAvailability";
    public string VendorId { get; set; } = "pyRevitLabs";
    public string VendorDescription { get; set; } = "RAD environment for Autodesk Revit";
    public string LargeImage { get; set; } = @"pyRevit\img\log32.png";
    public string SmallImage { get; set; } = string.Empty;
    public string ToolTipImage { get; set; } = string.Empty;
    public bool ShowName { get; set; }
    public bool ShowText { get; set; } = true;
    public bool ShowDescription { get; set; } = true;
    public bool ShowLongDescription { get; set; } = true;
    public bool ShowAvailabilityClassName { get; set; } = true;
    public bool ShowVisibilityMode { get; set; } = true;
    public bool ShowDiscipline { get; set; } = true;
    public bool ShowLargeImage { get; set; } = true;
    public bool ShowSmallImage { get; set; } = true;
    public bool ShowToolTipImage { get; set; } = true;
    public bool IsLocked { get; set; }
    public string? ErrorMessage { get; set; }
    public string? AssemblyPathError { get; set; }
    public string? AddInIdError { get; set; }
    public string? FullClassNameError { get; set; }
    public List<SelectableOptionViewModel> VisibilityModeOptions { get; } =
    [
        new("AlwaysVisible") { IsSelected = true },
        new("NotVisibleInFamily"),
        new("NotVisibleInProject"),
    ];
    public List<SelectableOptionViewModel> DisciplineOptions { get; } =
    [
        new("Architecture") { IsSelected = true },
        new("Structure"),
        new("MEP"),
    ];
    public ICommand? DiscardCommand { get; set; }
    public ICommand? SaveCommand { get; set; }
    public ICommand? GenerateAddInIdCommand { get; set; }
}

public sealed class DesignMarkupViewModel
{
    public string EmptySelectionPrompt { get; set; } = "Выберите файл в списке слева";
    public DesignFileRow? SelectedFile { get; set; } = new();
    public bool IsDirty { get; set; } = true;
    public string UnsavedBadge { get; set; } = "не сохранено";
    public string DiscardButtonLabel { get; set; } = "Отменить";
    public string SaveButtonLabel { get; set; } = "Сохранить";
    public string? ErrorMessage { get; set; }
    public bool IsLocked { get; set; }
    public ICommand? DiscardCommand { get; set; }
    public ICommand? SaveCommand { get; set; }
}

public sealed class DesignSettingsViewModel
{
    public string EmptySelectionPrompt { get; set; } = "Выберите файл в списке слева";
    public DesignFileRow? SelectedFile { get; set; } = new();
    public bool IsSupported { get; set; } = true;
    public string UnsupportedNotice { get; set; } = "Блок ManifestSettings управляет изоляцией контекста загрузки надстройки и поддерживается начиная с Revit 2026.";
    public bool? UseRevitContext { get; set; }
    public string ContextName { get; set; } = "pyRevit";
    public string UnsetOptionLabel { get; set; } = "Не задано";
    public string YesOptionLabel { get; set; } = "Да";
    public string NoOptionLabel { get; set; } = "Нет";
    public string DiscardButtonLabel { get; set; } = "Отменить";
    public string SaveButtonLabel { get; set; } = "Сохранить";
    public string? ErrorMessage { get; set; }
    public bool IsLocked { get; set; }
    public ICommand? DiscardCommand { get; set; }
    public ICommand? SaveCommand { get; set; }
    public ICommand? SetUseRevitContextCommand { get; set; }
}

public sealed class DesignEditorViewModel
{
    public EditorMode Mode { get; set; } = EditorMode.EntriesForm;
    public string EntriesTooltip { get; set; } = "Записи манифеста";
    public string FormTooltip { get; set; } = "Форма";
    public string MarkupTooltip { get; set; } = "Разметка";
    public string EntriesFormTooltip { get; set; } = "Записи и форма";
    public string EntriesMarkupTooltip { get; set; } = "Записи и разметка";
    public string SettingsTooltip { get; set; } = "Настройки файла (ManifestSettings)";
    public ICommand? SetEditorModeCommand { get; set; }
}
