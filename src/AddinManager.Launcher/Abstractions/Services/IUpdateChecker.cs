using AddinManager.Launcher.Services;

namespace AddinManager.Launcher.Abstractions.Services;

/// <summary>Проверка наличия более новой версии приложения.</summary>
public interface IUpdateChecker
{
    /// <summary>Проверяет последний релиз и сравнивает с текущей версией.</summary>
    /// <param name="cancellationToken">Отмена сетевого запроса.</param>
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default);
}
