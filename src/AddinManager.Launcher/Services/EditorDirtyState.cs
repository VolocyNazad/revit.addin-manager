namespace AddinManager.Launcher.Services;

/// <summary>
/// Изменяемое состояние грязного редактора: <see cref="ViewModels.EditorViewModel"/> обновляет
/// его при смене панели/правок, а <see cref="ViewModels.ListViewModel"/> читает перед переносом.
/// </summary>
public sealed class EditorDirtyState : Abstractions.Services.IEditorDirtyState
{
    private Func<bool>? _save;

    /// <inheritdoc />
    public bool IsDirty { get; private set; }

    /// <summary>Обновляет состояние и функцию сохранения видимой панели.</summary>
    /// <param name="isDirty">Есть ли правки.</param>
    /// <param name="save">Сохранение; возвращает <see langword="true"/> при успехе.</param>
    public void Update(bool isDirty, Func<bool> save)
    {
        IsDirty = isDirty;
        _save = save;
    }

    /// <inheritdoc />
    public bool TrySave() => _save?.Invoke() == true;
}
