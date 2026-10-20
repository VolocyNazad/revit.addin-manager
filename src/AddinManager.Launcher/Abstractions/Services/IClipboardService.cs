namespace AddinManager.Launcher.Abstractions.Services;

/// <summary>
/// Буфер обмена Windows. Контракт нужен командам контекстного меню («Копировать путь»,
/// «Копировать AddInId» и т. п.); сам буфер — системный ресурс, поэтому вызовы идут через сервис,
/// а не напрямую из ViewModel.
/// </summary>
public interface IClipboardService
{
    /// <summary>Кладёт текст в буфер обмена.</summary>
    /// <param name="text">Текст; пустая строка не копируется.</param>
    /// <returns><see langword="true"/>, если текст скопирован; <see langword="false"/>, если буфер занят другим процессом.</returns>
    bool TrySetText(string text);
}
