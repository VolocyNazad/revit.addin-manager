using AddinManager.Launcher.Abstractions.Services;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// Буфер обмена в памяти: команды контекстного меню проверяют, что именно скопировано, без
/// настоящего системного буфера (он недоступен в headless-прогоне и не должен влиять на тесты).
/// </summary>
public sealed class FakeClipboardService : IClipboardService
{
    /// <summary>Последний скопированный текст; <see langword="null"/>, если копирования не было.</summary>
    public string? LastText { get; private set; }

    /// <summary>Сколько раз пробовали скопировать текст.</summary>
    public int Calls { get; private set; }

    /// <inheritdoc />
    public bool TrySetText(string text)
    {
        Calls++;
        LastText = text;
        return true;
    }
}
