namespace AddinManager.Launcher.Abstractions;

/// <summary>
/// Строка списка с флагом пакетного выбора. Флаг хранится в модели строки, поэтому переживает
/// виртуализацию списка: строка, уехавшая за пределы видимой области, остаётся отмеченной.
/// Из-за этого обычный клик по другой строке не может снять такой флаг сам —
/// <c>PlainClickSelectionBehavior</c> сверяет флаги с выбором <c>ListBox</c>.
/// </summary>
public interface IBulkSelectableRow
{
    /// <summary>Строка отмечена для пакетных команд списка.</summary>
    bool IsSelected { get; set; }
}
