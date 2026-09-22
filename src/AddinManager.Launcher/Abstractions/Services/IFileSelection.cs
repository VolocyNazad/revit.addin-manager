using System.ComponentModel;
using AddinManager.Launcher.ViewModels;

namespace AddinManager.Launcher.Abstractions.Services;

/// <summary>
/// Выбор файла: слот живёт в зоне списка (её модель и имплементирует контракт),
/// остальные зоны только читают и подписываются на изменения. Направление одно —
/// список → потребители: зоне списка выбор записей никогда не понадобится
/// (иначе цикл в DI).
/// </summary>
public interface IFileSelection : INotifyPropertyChanged
{
    /// <summary>Выбранный кликом файл; <see langword="null"/>, если ничего не выбрано.</summary>
    AddinFileRowViewModel? SelectedFile { get; set; }
}
