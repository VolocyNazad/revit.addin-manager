using AddinManager.Core.Manifests;
using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.Services;

namespace AddinManager.Launcher.Tests.TestDoubles;

/// <summary>
/// Отвечает сам, без окон: результат задаёт тест, показанные тексты записываются для проверок.
/// </summary>
public sealed class FakeDialogService : IDialogService
{
    /// <summary>Показанные тексты вопросов.</summary>
    public List<string> ShownMessages { get; } = [];

    /// <summary>Ответ на следующий вопрос. По умолчанию — согласие.</summary>
    public bool Result { get; set; } = true;

    /// <summary>Ответ на следующий диалог файла (<see langword="null"/> — отмена).</summary>
    public NewFileOptions? NewFileResult { get; set; }

    /// <summary>Ответ на следующий диалог записи (<see langword="null"/> — отмена).</summary>
    public AddinEntryType? NewEntryResult { get; set; }

    /// <inheritdoc />
    public bool Confirm(string message)
    {
        ShownMessages.Add(message);
        return Result;
    }

    /// <inheritdoc />
    public NewFileOptions? PromptNewFile() => NewFileResult;

    /// <inheritdoc />
    public AddinEntryType? PromptNewEntry() => NewEntryResult;

    /// <inheritdoc />
    public bool PromptUpdate(string message, string downloadUrl)
    {
        ShownMessages.Add(message);
        UpdateUrls.Add(downloadUrl);
        return Result;
    }

    /// <summary>Ссылки, которые предлагали открыть в <see cref="PromptUpdate"/>.</summary>
    public List<string> UpdateUrls { get; } = [];
}
