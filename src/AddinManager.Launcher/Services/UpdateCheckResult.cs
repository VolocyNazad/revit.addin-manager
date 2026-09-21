namespace AddinManager.Launcher.Services;

/// <summary>
/// Итог проверки обновлений. <see cref="Error"/> непуст — проверка не удалась (сеть, GitHub),
/// остальное не заполнено; иначе <see cref="HasUpdate"/> говорит, есть ли версия новее текущей.
/// </summary>
/// <param name="HasUpdate">Есть ли версия новее текущей.</param>
/// <param name="LatestVersion">Последняя версия в релизах (без префикса <c>v</c>), если удалось прочитать.</param>
/// <param name="DownloadUrl">Ссылка на страницу релиза (скачивание).</param>
/// <param name="CurrentVersion">Текущая версия приложения.</param>
/// <param name="Error">Ошибка проверки; <see langword="null"/> — всё прошло.</param>
public sealed record UpdateCheckResult(
    bool HasUpdate,
    string? LatestVersion,
    string? DownloadUrl,
    string? CurrentVersion,
    string? Error);
