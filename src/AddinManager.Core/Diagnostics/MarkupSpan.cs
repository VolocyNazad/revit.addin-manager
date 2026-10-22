namespace AddinManager.Core.Diagnostics;

/// <summary>
/// Срез текста разметки: смещение от начала и длина. Используется подсветкой редактора и
/// диагностикой; не зависит от AvalonEdit, чтобы правила жили в headless Core.
/// </summary>
/// <param name="Start">Смещение от начала текста.</param>
/// <param name="Length">Длина среза.</param>
public readonly record struct MarkupSpan(int Start, int Length);
