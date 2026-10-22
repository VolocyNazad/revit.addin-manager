using AddinManager.Core.Storage;

namespace AddinManager.Launcher.Services;

/// <summary>
/// Результат файловой операции для зоны списка: что перечитать, что выбрать, что подсветить
/// в фильтрах, какую ошибку показать и каким тостом подтвердить. Сами диалоги, store-мутации и
/// запись undo выполняет <see cref="FileOperations"/>; зона только применяет результат к виду.
/// </summary>
/// <param name="RefreshNeeded">Файлы на диске изменились — список нужно перечитать.</param>
/// <param name="SelectIdentity">Файл, на который вернуть выбор после перечитывания.</param>
/// <param name="RevealTarget">Файл, который нужно сделать видимым, поправив фильтры списка.</param>
/// <param name="BannerError">Ошибка для баннера над списком; <see langword="null"/> — нет ошибки.</param>
/// <param name="Toast">Текст тоста-подтверждения; <see langword="null"/> — без тоста.</param>
public sealed record FileOperationOutcome(
    bool RefreshNeeded = false,
    AddinFileIdentity? SelectIdentity = null,
    AddinFile? RevealTarget = null,
    string? BannerError = null,
    string? Toast = null)
{
    /// <summary>Ничего не изменилось и показывать нечего.</summary>
    public static FileOperationOutcome None { get; } = new();
}
