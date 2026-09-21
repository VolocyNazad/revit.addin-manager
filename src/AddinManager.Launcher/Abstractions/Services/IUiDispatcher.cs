namespace AddinManager.Launcher.Abstractions.Services;

/// <summary>
/// Абстракция над WPF <see cref="System.Windows.Threading.Dispatcher"/>: единственная причина
/// существования — <see cref="Action"/> может прийти не из потока UI (см.
/// <c>AddinManager.Core.Storage.IAddinChangeWatcher.Changed</c>, другая сборка, отсюда без
/// <c>cref</c> — событие сырых <see cref="System.IO.FileSystemWatcher"/>-уведомлений приходит из
/// threadpool), а трогать <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/>
/// не из потока UI нельзя. Реализации заменяемы через DI, как и остальные абстракции проекта —
/// тестовый двойник может выполнять действие немедленно, синхронно, без настоящего Dispatcher.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// Выполняет <paramref name="action"/> в потоке UI, синхронно дожидаясь завершения.
    /// Если вызвано уже из потока UI — выполняет немедленно, без лишнего переключения.
    /// </summary>
    void Invoke(Action action);
}
