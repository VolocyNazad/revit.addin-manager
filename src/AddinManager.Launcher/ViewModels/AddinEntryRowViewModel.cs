using System.ComponentModel;
using AddinManager.Core.Manifests;
using Microsoft.Extensions.Localization;

namespace AddinManager.Launcher.ViewModels;

/// <summary>
/// Строка подпанели записей: один <see cref="AddinEntry"/> внутри файла, выбранного в зоне
/// списка (план, раздел 6 — Type-бейдж, отображаемое имя). Записи здесь не
/// редактируются и не имеют тоглов (план, раздел 2: "entries have no toggles"); структурное
/// редактирование выбранной записи — отдельная зона (<see cref="FormViewModel"/>), которая
/// читает эту строку через <c>EntriesViewModel.SelectedEntry</c>.
/// Не <c>ObservableObject</c>: <see cref="AddinEntry"/> — неизменяемая запись, и после
/// построения строки её отображаемые свойства никогда не меняются; единственное
/// изменяемое состояние — пакетный выбор <see cref="IsSelected"/> с ручным уведомлением.
/// </summary>
/// <param name="entry">Исходная запись манифеста.</param>
/// <param name="index">
/// Порядковый номер записи в файле, считая с 1 — используется только как запасное имя,
/// когда у записи нет ни <see cref="AddinEntry.Name"/>, ни <see cref="AddinEntry.Text"/>.
/// </param>
/// <param name="localizer">Строка запасного имени.</param>
/// <param name="duplicateInFile">
/// <c>AddInId</c> записи повторяется в других записях того же файла.
/// </param>
/// <param name="duplicateAcrossFiles">
/// <c>AddInId</c> записи встречается в других файлах той же версии Revit.
/// </param>
public sealed class AddinEntryRowViewModel(
    AddinEntry entry,
    int index,
    IStringLocalizer<AddinEntryRowViewModel> localizer,
    bool duplicateInFile = false,
    bool duplicateAcrossFiles = false) : INotifyPropertyChanged
{
    private bool _isSelected;

    /// <summary>Исходная запись.</summary>
    public AddinEntry Entry { get; } = entry;

    /// <summary>
    /// Пакетный выбор строки (чекбокс слева, bulk-удаление в <see cref="EntriesViewModel"/>).
    /// Единственное изменяемое состояние строки — остальное неизменно после построения,
    /// поэтому здесь ручной <see cref="INotifyPropertyChanged"/>, а не
    /// <c>ObservableObject</c>: генератору там нечего генерировать.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Type записи (план, раздел 6 — бейдж <c>Application/DBApplication/Command</c>).</summary>
    public AddinEntryType Type => Entry.Type;

    /// <summary>Отображаемое имя: <c>Name ?? Text</c> (план, раздел 6), иначе — порядковый номер.</summary>
    public string DisplayName => Entry.Name ?? Entry.Text ?? localizer["EntryFallbackName", index];

    /// <summary>Карточка-тултип: подпись "Тип". Имена тегов (AddInId, Assembly, ...) — технические токены, не переводятся.</summary>
    public string TypeLabel => localizer["EntryTooltip_TypeLabel"];

    /// <summary>Карточка-тултип: подпись "Сборка".</summary>
    public string AssemblyLabel => localizer["EntryTooltip_AssemblyLabel"];

    /// <summary>Карточка-тултип: подпись "Класс".</summary>
    public string ClassLabel => localizer["EntryTooltip_ClassLabel"];

    /// <summary>Карточка-тултип: подпись "Вендор".</summary>
    public string VendorLabel => localizer["EntryTooltip_VendorLabel"];

    /// <summary>Карточка-тултип: подпись "Описание".</summary>
    public string DescriptionLabel => localizer["EntryTooltip_DescriptionLabel"];

    /// <summary>Подзаголовок "Vendor • VendorDescription", если задан хотя бы один.</summary>
    public string? VendorSubtitle
    {
        get
        {
            var parts = new[] { Entry.VendorId, Entry.VendorDescription }.Where(p => !string.IsNullOrWhiteSpace(p));
            var joined = string.Join(" • ", parts);
            return joined.Length == 0 ? null : joined;
        }
    }

    /// <summary>Уникальный идентификатор записи, приглушённым моноширинным текстом в строке.</summary>
    public string AddInIdText => Entry.AddInId.ToString();

    /// <summary>Значения VisibilityMode через запятую — <see langword="null"/>, если ни одного не задано (для карточки-тултипа).</summary>
    public string? VisibilityModesText => Entry.VisibilityModes.Count > 0 ? string.Join(", ", Entry.VisibilityModes) : null;

    /// <summary>Значения Discipline через запятую — <see langword="null"/>, если ни одного не задано (для карточки-тултипа).</summary>
    public string? DisciplinesText => Entry.Disciplines.Count > 0 ? string.Join(", ", Entry.Disciplines) : null;

    /// <summary>
    /// В записи есть проблемы (пустые обязательные поля, неизвестный тип, дубль
    /// <c>AddInId</c> внутри файла или в других файлах той же версии) — вид показывает
    /// иконку предупреждения с <see cref="WarningMessage"/> в тултипе.
    /// </summary>
    public bool HasWarning => WarningMessage is not null;

    /// <summary>Заголовок карточки-тултипа иконки предупреждения.</summary>
    public string WarningTooltipTitle => localizer["WarningTooltipTitle"];

    /// <summary>Причины <see cref="HasWarning"/> человеческим языком; <c>null</c>, когда всё чисто.</summary>
    public string? WarningMessage
    {
        get
        {
            var reasons = new List<string>();
            if (string.IsNullOrWhiteSpace(Entry.AssemblyPath) || string.IsNullOrWhiteSpace(Entry.FullClassName))
                reasons.Add(localizer["WarningEmptyFields"]);
            if (Entry.Type == AddinEntryType.Unknown)
                reasons.Add(localizer["WarningUnknownType"]);
            if (duplicateInFile)
                reasons.Add(localizer["WarningDuplicateInFile"]);
            if (duplicateAcrossFiles)
                reasons.Add(localizer["WarningDuplicateAcrossFiles"]);

            return reasons.Count == 0 ? null : string.Join("; ", reasons);
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"Entry({DisplayName}, {Type}, {AddInIdText})";
}
