using System.Windows.Input;

namespace AddinManager.Launcher.Abstractions.Services;

/// <summary>
/// Панель редактора с сохранением: форма, разметка и настройки файла. Контракт нужен
/// роутеру сохранения (<c>EditorViewModel.SaveActive</c>): какая панель сейчас видна,
/// решает режим редактора, а дернуть надо именно её команды с их готовыми проверками
/// (файл выбран, есть изменения, текст валиден, Revit закрыт).
/// </summary>
public interface ISavablePane
{
    /// <summary>Сохранить правки панели (доступность — через <see cref="ICommand.CanExecute"/>).</summary>
    ICommand SaveCommand { get; }

    /// <summary>Отбросить правки панели (доступность — через <see cref="ICommand.CanExecute"/>).</summary>
    ICommand DiscardCommand { get; }
}
