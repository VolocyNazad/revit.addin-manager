using System.ComponentModel;
using AddinManager.Launcher.ViewModels;

namespace AddinManager.Launcher.Abstractions.Services;

/// <summary>
/// Выбор записи: слот живёт в зоне записей (её модель и имплементирует контракт),
/// остальные зоны только читают и подписываются на изменения. Направление одно —
/// записи → потребители, как и у <see cref="IFileSelection"/> (список → потребители).
/// </summary>
public interface IEntrySelection : INotifyPropertyChanged
{
    /// <summary>Выбранная кликом запись; <see langword="null"/>, если ничего не выбрано.</summary>
    AddinEntryRowViewModel? SelectedEntry { get; set; }
}
