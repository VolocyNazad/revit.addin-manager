using System.Runtime.InteropServices;
using System.Windows;
using AddinManager.Launcher.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace AddinManager.Launcher.Services;

/// <summary>
/// Буфер обмена через <see cref="Clipboard"/>. Занятый другим процессом буфер — ожидаемая
/// пользовательская неудача: пишем предупреждение один раз здесь, команда просто не показывает
/// подтверждение; само приложение продолжает работать.
/// </summary>
/// <param name="logger">Логгер сервиса — сюда попадает предупреждение о занятом буфере.</param>
public sealed class WpfClipboardService(ILogger<WpfClipboardService> logger) : IClipboardService
{
    /// <inheritdoc />
    public bool TrySetText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch (ExternalException exception)
        {
            logger.LogWarning(exception, "Не удалось скопировать текст в буфер обмена");
            return false;
        }
    }
}
