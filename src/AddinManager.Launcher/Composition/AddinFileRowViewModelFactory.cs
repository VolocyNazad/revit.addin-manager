using AddinManager.Core.Abstractions.Storage;
using AddinManager.Core.Storage;
using AddinManager.Launcher.Abstractions.Composition;
using AddinManager.Launcher.Abstractions.Services;
using AddinManager.Launcher.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace AddinManager.Launcher.Composition;

/// <summary>Фабрика строк списка: зависимости — из контейнера, файл — параметром.</summary>
/// <param name="store">Чтение файлов с диска и переключение enabled/disabled.</param>
/// <param name="loggerFactory">
/// Источник логгера для строк: категория логгера (<c>AddinFileRowViewModel</c>) обязана
/// совпадать с охватывающим типом, иначе ругается анализатор (S6672) — поэтому фабрика,
/// а не готовый <c>ILogger{AddinFileRowViewModel}</c> параметром.
/// </param>
/// <param name="localizer">Строки списка файлов.</param>
/// <param name="mutationPolicy">Права на изменение файла: машинная область без прав администратора только для чтения.</param>
public sealed class AddinFileRowViewModelFactory(
    IAddinStore store,
    ILoggerFactory loggerFactory,
    IStringLocalizer<AddinFileRowViewModel> localizer,
    IFileMutationPolicy mutationPolicy) : IAddinFileRowViewModelFactory
{
    /// <inheritdoc />
    public AddinFileRowViewModel Create(AddinFile file) =>
        new(store, file, loggerFactory.CreateLogger<AddinFileRowViewModel>(), localizer)
        {
            IsReadOnly = mutationPolicy.IsReadOnly(file),
        };
}
