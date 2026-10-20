namespace AddinManager.Launcher.Abstractions.Services;

/// <summary>
/// Текущее состояние несохранённых правок редактора и возможность сохранить видимую панель.
/// Позволяет списку спросить редактор, не создавая циклической зависимости через DI.
/// </summary>
public interface IEditorDirtyState
{
    /// <summary>Есть ли несохранённые правки в видимой панели.</summary>
    bool IsDirty { get; }

    /// <summary>Сохраняет видимую панель; <see langword="false"/>, если сохранить не удалось.</summary>
    bool TrySave();

    /// <summary>Обновляет состояние и функцию сохранения видимой панели (вызывает редактор).</summary>
    /// <param name="isDirty">Есть ли несохранённые правки.</param>
    /// <param name="save">Сохранение; возвращает <see langword="true"/> при успехе.</param>
    void Update(bool isDirty, Func<bool> save);
}
