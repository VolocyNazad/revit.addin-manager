using AddinManager.Core.Storage;

namespace AddinManager.Launcher.Services;

/// <summary>Параметры нового файла из диалога добавления.</summary>
/// <param name="FileName">Имя файла (суффикс <c>.addin</c> допишет стор).</param>
/// <param name="Scope">Область файла.</param>
/// <param name="Version">Версия Revit ("2021".."2027").</param>
/// <param name="Disabled">Сразу в <c>disabled/</c>.</param>
public sealed record NewFileOptions(string FileName, AddinScope Scope, string Version, bool Disabled);
