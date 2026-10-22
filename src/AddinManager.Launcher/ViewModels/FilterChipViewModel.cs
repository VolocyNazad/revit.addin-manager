using CommunityToolkit.Mvvm.ComponentModel;

namespace AddinManager.Launcher.ViewModels;

/// <summary>
/// Чипс-фильтр: подпись, значение и признак выбора. Коллекцию чипсов строит владелец
/// (список или диалог) из <see cref="Core.RevitVersionCatalog"/>, поэтому список версий
/// не дублируется в XAML и в моделях.
/// </summary>
public sealed partial class FilterChipViewModel : ObservableObject
{
    /// <summary>Создает чипс.</summary>
    /// <param name="label">Подпись; владелец пересчитывает её при смене языка и счётчиков.</param>
    /// <param name="value">Значение фильтра; <see langword="null"/> — «все».</param>
    public FilterChipViewModel(string label, string? value)
    {
        _label = label;
        Value = value;
    }

    /// <summary>Значение фильтра, которое уходит в команду выбора; <see langword="null"/> — «все».</summary>
    public string? Value { get; }

    /// <summary>Подпись чипса (локализованная, со счётчиком там, где он нужен).</summary>
    [ObservableProperty]
    private string _label;

    /// <summary>Выбран ли этот чипс сейчас — стиль показывает активный ярче.</summary>
    [ObservableProperty]
    private bool _isActive;
}
