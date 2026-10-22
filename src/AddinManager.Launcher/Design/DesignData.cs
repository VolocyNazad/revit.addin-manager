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
    public string ScopeAllChipLabel { get; set; } = "Все · 2";
    public string ScopeUserChipLabel { get; set; } = "User · 1";
    public string ScopeMachineChipLabel { get; set; } = "Machine · 1";
    public string? VersionFilter { get; set; }
    public List<FilterChipViewModel> VersionChips { get; } =
    [
        new("Все версии · 2", null) { IsActive = true },
        new("2021 · 0", "2021"),
        new("2022 · 0", "2022"),
        new("2023 · 0", "2023"),
        new("2024 · 0", "2024"),
        new("2025 · 2", "2025"),
        new("2026 · 0", "2026"),
        new("2027 · 0", "2027"),
    ];
    public EnabledFilter EnabledFilter { get; set; } = EnabledFilter.All;
    public string EnabledFilterLabel { get; set; } = "Состояние:";
    public string EnabledAllChipLabel { get; set; } = "Все · 2";
    public string EnabledOnChipLabel { get; set; } = "Включённые · 2";
    public string EnabledOffChipLabel { get; set; } = "Выключенные · 0";
    public ListSortOrder SortOrder { get; set; } = ListSortOrder.Name;
    public bool SortDescending { get; set; }
    public string SortDirectionGlyph { get; set; } = "↑";
    public string SortDirectionTooltip { get; set; } = "Изменить направление сортировки";
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
    public string RenameMenuLabel { get; set; } = "Переименовать (F2)";
    public string MoveMenuLabel { get; set; } = "Переместить в…";
    public string CopyToMenuLabel { get; set; } = "Копировать в…";
    public string ToggleSelectedLabel { get; set; } = "Выключить";
    public string DeleteSelectedLabel { get; set; } = "Удалить";
    public string SelectAllToggleLabel { get; set; } = "Снять выбор";
    public ICommand? RefreshManualCommand { get; set; }
    public ICommand? SetScopeFilterCommand { get; set; }
    public ICommand? SetVersionFilterCommand { get; set; }
    public ICommand? SetEnabledFilterCommand { get; set; }
    public ICommand? SetSortOrderCommand { get; set; }
    public ICommand? ToggleSortDirectionCommand { get; set; }
    public bool HasUndo { get; set; }
    public ICommand? UndoLastActionCommand { get; set; }
    public ICommand? ToggleSelectedCommand { get; set; }
    public ICommand? ShowInFolderCommand { get; set; }
    public ICommand? BeginRenameCommand { get; set; }
    public ICommand? MoveToCommand { get; set; }
    public ICommand? CopyToCommand { get; set; }
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
    public bool IsRenaming { get; set; }
    public string? RenameText { get; set; } = "pyRevit.addin";
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
    public ICommand? GenerateAddInIdCommand { get; set; }
}

public sealed class DesignMarkupViewModel
{
    public string EmptySelectionPrompt { get; set; } = "Выберите файл в списке слева";
    public DesignFileRow? SelectedFile { get; set; } = new();
    public bool IsLocked { get; set; }
    public DesignMarkupSearchViewModel Search { get; } = new();
}

public sealed class DesignMarkupSearchViewModel
{
    public string Query { get; set; } = "Assembly";
    public bool MatchCase { get; set; }
    public bool WholeWords { get; set; }
    public bool UseRegex { get; set; }
    public string Placeholder { get; set; } = "Найти в разметке";
    public string StatusText { get; set; } = "1 из 3";
    public bool HasStatus { get; set; } = true;
    public bool IsQueryEmpty { get; set; }
    public string NextTooltip { get; set; } = "Следующее совпадение";
    public string PreviousTooltip { get; set; } = "Предыдущее совпадение";
    public string MatchCaseTooltip { get; set; } = "Учитывать регистр";
    public string WholeWordsTooltip { get; set; } = "Только целые слова";
    public string UseRegexTooltip { get; set; } = "Использовать регулярное выражение";
    public ICommand? NextCommand { get; set; }
    public ICommand? PreviousCommand { get; set; }
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
    public bool IsLocked { get; set; }
    public ICommand? SetUseRevitContextCommand { get; set; }
}

public sealed class DesignEditorViewModel
{
    public EditorMode Mode { get; set; } = EditorMode.EntriesForm;
    public DesignFileRow? SelectedFile { get; set; } = new();
    public bool IsDirty { get; set; } = true;
    public string UnsavedBadge { get; set; } = "не сохранено";
    public string SaveLabel { get; set; } = "Сохранить (Ctrl+S)";
    public string DiscardLabel { get; set; } = "Отменить (Esc)";
    public string? HeaderError { get; set; }
    public bool HasHeaderError { get; set; }
    public bool ShowHeaderActions { get; set; } = true;
    public string EntriesTooltip { get; set; } = "Записи манифеста";
    public string FormTooltip { get; set; } = "Форма";
    public string MarkupTooltip { get; set; } = "Разметка";
    public string EntriesFormTooltip { get; set; } = "Записи и форма";
    public string EntriesMarkupTooltip { get; set; } = "Записи и разметка";
    public string SettingsTooltip { get; set; } = "Настройки файла (ManifestSettings)";
    public ICommand? SetEditorModeCommand { get; set; }
    public ICommand? SaveActiveCommand { get; set; }
    public ICommand? DiscardActiveCommand { get; set; }
}

public sealed class DesignConfirmDialogViewModel
{
    public string Message { get; set; } = "Удалить файл pyRevit.addin? Он будет перемещён в Корзину.";
    public bool? Result { get; set; }
    public string YesLabel { get; set; } = "Удалить";
    public string NoLabel { get; set; } = "Отмена";
    public ICommand? YesCommand { get; set; }
    public ICommand? NoCommand { get; set; }
}

public sealed class DesignAddFileDialogViewModel
{
    public string FileNameLabel { get; set; } = "Имя файла";
    public string FileName { get; set; } = "MyPlugin.addin";
    public string ScopeLabel { get; set; } = "Область";
    public AddinScope Scope { get; set; } = AddinScope.User;
    public string VersionLabel { get; set; } = "Версия Revit";
    public string DisabledLabel { get; set; } = "Создать отключённым";
    public bool IsDisabled { get; set; }
    public string? ErrorMessage { get; set; }
    public string CancelButtonLabel { get; set; } = "Отмена";
    public string AddButtonLabel { get; set; } = "Создать";
    public bool? Result { get; set; }
    public List<FilterChipViewModel> VersionChips { get; } =
    [
        new("2025", "2025"),
        new("2026", "2026") { IsActive = true },
        new("2027", "2027"),
    ];
    public ICommand? SetScopeCommand { get; set; }
    public ICommand? SetVersionCommand { get; set; }
    public ICommand? AddCommand { get; set; }
    public ICommand? CancelCommand { get; set; }
}

public sealed class DesignAddEntryDialogViewModel
{
    public string TypeLabel { get; set; } = "Тип записи";
    public AddinEntryType EntryType { get; set; } = AddinEntryType.Application;
    public bool? Result { get; set; }
    public string AddButtonLabel { get; set; } = "Добавить";
    public string CancelButtonLabel { get; set; } = "Отмена";
    public ICommand? SetEntryTypeCommand { get; set; }
    public ICommand? AddCommand { get; set; }
    public ICommand? CancelCommand { get; set; }
}

public sealed class DesignUpdateDialogViewModel
{
    public string TitleLabel { get; set; } = "Доступно обновление";
    public string Message { get; set; } = "Revit.AddinManager 1.5.0 уже доступна. Скачать сейчас?";
    public string DownloadLabel { get; set; } = "Скачать";
    public string LaterLabel { get; set; } = "Позже";
    public bool? Result { get; set; }
    public ICommand? DownloadCommand { get; set; }
    public ICommand? LaterCommand { get; set; }
}

public sealed class DesignMoveCopyDialogViewModel
{
    public string Title { get; set; } = "Переместить файл";
    public string SourceLabel { get; set; } = "Источник";
    public string FileName { get; set; } = "pyRevit.addin";
    public string VersionLabel { get; set; } = "Версия Revit";
    public List<FilterChipViewModel> VersionChips { get; } =
    [
        new("2025", "2025"),
        new("2026", "2026") { IsActive = true },
        new("2027", "2027"),
    ];
    public string ScopeLabel { get; set; } = "Область";
    public AddinScope Scope { get; set; } = AddinScope.User;
    public string EnableAfterLabel { get; set; } = "Включить после переноса";
    public bool EnableAfter { get; set; } = true;
    public string TargetLabel { get; set; } = "Целевой путь";
    public string TargetPath { get; set; } =
        @"C:\Users\you\AppData\Roaming\Autodesk\Revit\Addins\2026\pyRevit.addin";
    public string? SameFolderError { get; set; }
    public string? ConflictWarning { get; set; } = "Файл с таким именем уже есть в 2026 — он будет заменён.";
    public string OverwriteLabel { get; set; } = "Заменить существующий файл";
    public bool Overwrite { get; set; } = true;
    public string? CrossVersionWarning { get; set; } = "Перенос между версиями Revit: проверьте совместимость сборки.";
    public string? SettingsWarning { get; set; }
    public string? ErrorMessage { get; set; }
    public string ConfirmButtonLabel { get; set; } = "Переместить";
    public string CancelButtonLabel { get; set; } = "Отмена";
    public bool? Result { get; set; }
    public ICommand? SetVersionCommand { get; set; }
    public ICommand? SetScopeCommand { get; set; }
    public ICommand? ConfirmCommand { get; set; }
    public ICommand? CancelCommand { get; set; }
}
