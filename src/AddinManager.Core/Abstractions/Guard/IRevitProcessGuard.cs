namespace AddinManager.Core.Abstractions.Guard;

/// <summary>
/// Сторож запущенного Revit: пока жив процесс, окно показывает предупреждение (план,
/// раздел 5). Реализации заменяемы через DI. Событие может прийти не из потока UI —
/// та же оговорка, что у <see cref="Storage.IAddinChangeWatcher"/> (другая часть того же
/// слоя, отсюда без <c>cref</c>): потребители маршалят сами.
/// </summary>
public interface IRevitProcessGuard : IDisposable
{
    /// <summary>Revit запущен прямо сейчас (есть хоть одна версия).</summary>
    bool IsRunning { get; }

    /// <summary>Годы запущенных версий Revit ("2024", "2025", ...), пусто — никто не запущен.</summary>
    IReadOnlySet<string> RunningVersions { get; }

    /// <summary>Набор версий сменился: Revit запустили, закрыли или сменился состав версий.</summary>
    event EventHandler? Changed;

    /// <summary>Начинает опрос. Повторный вызов ничего не делает.</summary>
    void Start();

    /// <summary>Внеочередная проверка помимо интервала опроса (фокус окна).</summary>
    void CheckNow();
}
