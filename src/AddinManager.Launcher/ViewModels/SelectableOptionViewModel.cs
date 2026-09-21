using CommunityToolkit.Mvvm.ComponentModel;

namespace AddinManager.Launcher.ViewModels;

/// <summary>
/// Один пункт мульти-набора формы (VisibilityMode/Discipline) — план, раздел 6: "No plain
/// checkboxes anywhere: multi-options ... are rows with toggles." Обертка вокруг строкового
/// значения схемы (<see cref="Core.Abstractions.Manifests.IManifestSchema.VisibilityModeValues"/>/
/// <see cref="Core.Abstractions.Manifests.IManifestSchema.DisciplineValues"/>) с переключаемым
/// выбором, а не булев массив по индексу — так строку XAML можно биндить на <see cref="Value"/>
/// напрямую, без параллельного маппинга индексов.
/// </summary>
/// <param name="value">Значение XML-элемента, которое представляет этот пункт.</param>
public sealed partial class SelectableOptionViewModel(string value) : ObservableObject
{
    /// <summary>Значение XML-элемента (например "Structure").</summary>
    public string Value { get; } = value;

    /// <summary>Выбран ли этот пункт — двусторонний биндинг на чип-тоггл в форме.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Revit запущен — чип гаснет (выставляет <see cref="FormViewModel"/>, тот же приём, что строки списка).</summary>
    [ObservableProperty]
    private bool _isLocked;

    /// <inheritdoc />
    public override string ToString() => $"{Value}={IsSelected}";
}
