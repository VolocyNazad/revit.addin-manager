using System.Windows.Input;

namespace AddinManager.Launcher.Abstractions.Services;

/// <summary>
/// Панель редактора с сохранением: форма, разметка и настройки файла. Контракт нужен
/// общему заголовку редактора и роутеру сохранения (<c>EditorViewModel.SaveActive</c>): какая
/// панель сейчас видна, решает режим редактора, а дернуть надо именно её команды с их готовыми
/// проверками (файл выбран, есть изменения, текст валиден, Revit закрыт).
/// </summary>
public interface ISavablePane
{
    /// <summary>Сохранить правки панели (доступность — через <see cref="ICommand.CanExecute"/>).</summary>
    ICommand SaveCommand { get; }

    /// <summary>Отбросить правки панели (доступность — через <see cref="ICommand.CanExecute"/>).</summary>
    ICommand DiscardCommand { get; }

    /// <summary>Есть ли у панели несохранённые правки — для бейджа в общем заголовке.</summary>
    bool IsDirty { get; }

    /// <summary>Ошибка панели, которую показывает общий заголовок; <see langword="null"/>, если её нет.</summary>
    string? ErrorMessage { get; }

    /// <summary>Подпись кнопки сохранения с её хоткеем — общая кнопка заголовка.</summary>
    string SaveButtonLabel { get; }

    /// <summary>Подпись кнопки отмены правок с её хоткеем — общая кнопка заголовка.</summary>
    string DiscardButtonLabel { get; }
}
