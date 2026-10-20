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
}
