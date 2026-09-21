using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.Services;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// Возвращает заданный результат проверки обновлений, не трогая сеть: тест проверяет поведение
/// <see cref="Launcher.ViewModels.MainViewModel"/>, а не сам чекер.
/// </summary>
public sealed class FakeUpdateChecker : IUpdateChecker
{
    /// <summary>Результат, который вернёт следующий <see cref="CheckAsync"/>.</summary>
    public UpdateCheckResult Result { get; set; } =
        new(false, null, null, "1.0.0.0", null);

    /// <inheritdoc />
    public Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Result);
}
