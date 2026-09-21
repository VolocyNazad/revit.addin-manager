namespace AddinManager.Core.Guard;

/// <summary>
/// Запись заблокирована: Revit запущен — перенос и сохранение .addin откладываются до его
/// закрытия (план, раздел 5 — "No .addin moves while live"). Текст уже локализован бросающей
/// стороной и показывается в UI как есть.
/// </summary>
public sealed class RevitRunningException : InvalidOperationException
{
    /// <summary>Создает исключение.</summary>
    /// <param name="message">Текст для UI.</param>
    public RevitRunningException(string message)
        : base(message)
    {
    }

    /// <summary>Создает исключение с внутренней причиной.</summary>
    /// <param name="message">Текст для UI.</param>
    /// <param name="innerException">Исходная ошибка.</param>
    public RevitRunningException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
