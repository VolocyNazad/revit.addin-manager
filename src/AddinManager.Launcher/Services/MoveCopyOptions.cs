using AddinManager.Core.Storage;

namespace AddinManager.Launcher.Services;

/// <summary>Тип операции переноса файла в другую версию/scope.</summary>
public enum MoveCopyKind
{
    /// <summary>Переместить, освободив исходную папку.</summary>
    Move,

    /// <summary>Скопировать, оставив исходник.</summary>
    Copy,
}

/// <summary>Запрос диалога выбора цели переноса/копирования.</summary>
/// <param name="File">Файл, который переносим или копируем.</param>
/// <param name="Kind">Перемещение или копирование.</param>
public sealed record MoveCopyRequest(AddinFile File, MoveCopyKind Kind);

/// <summary>Выбор пользователя в диалоге цели.</summary>
/// <param name="Version">Целевая версия Revit.</param>
/// <param name="Scope">Целевая область.</param>
/// <param name="EnableAfter">Включить файл в цели; иначе сохраняется текущее состояние.</param>
/// <param name="Overwrite">Заменить существующий целевой файл (старый уйдёт в Корзину).</param>
public sealed record MoveCopyOptions(string Version, AddinScope Scope, bool EnableAfter, bool Overwrite);
