using AddinManager.Core.Manifests;
using AddinManager.Launcher.Services;

namespace AddinManager.Launcher.Abstractions.Services;

/// <summary>
/// Вопросы пользователю с блокирующим ответом. Реализация заменяема через DI
/// (тесты отвечают сами, без окон).
/// </summary>
public interface IDialogService
{
    /// <summary>Просит подтвердить необратимое действие.</summary>
    /// <param name="message">Текст вопроса (уже локализован вызывающим).</param>
    /// <returns><see langword="true"/> — подтверждено.</returns>
    bool Confirm(string message);

    /// <summary>Просит параметры нового файла.</summary>
    /// <returns>Параметры или <see langword="null"/> — отменили.</returns>
    NewFileOptions? PromptNewFile();

    /// <summary>Просит тип новой записи.</summary>
    /// <returns>Тип или <see langword="null"/> — отменили.</returns>
    AddinEntryType? PromptNewEntry();

    /// <summary>Показывает новость о новой версии и предлагает скачать.</summary>
    /// <param name="message">Текст новости (уже локализован вызывающим).</param>
    /// <param name="downloadUrl">Ссылка на скачивание — откроется в браузере при согласии.</param>
    /// <returns><see langword="true"/> — согласились скачать (браузер открыт).</returns>
    bool PromptUpdate(string message, string downloadUrl);
}
