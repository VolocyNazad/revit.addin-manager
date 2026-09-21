using AddinManager.Launcher.ViewModels;

namespace AddinManager.Launcher.Abstractions.Composition;

/// <summary>
/// Создаёт пункты мульти-наборов формы: значения известны только из схемы при загрузке
/// записи, поэтому контейнер собирает фабрику, а та подставляет данные. Реализация
/// заменяема через DI.
/// </summary>
public interface ISelectableOptionViewModelFactory
{
    /// <summary>Создает пункт под значение XML-элемента.</summary>
    /// <param name="value">Значение XML-элемента, которое представляет этот пункт.</param>
    SelectableOptionViewModel Create(string value);
}
