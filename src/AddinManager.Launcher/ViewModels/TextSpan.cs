namespace AddinManager.Launcher.ViewModels;

/// <summary>Срез текста: начало и длина (для подсветки блока записи в разметке).</summary>
/// <param name="Start">Смещение от начала текста.</param>
/// <param name="Length">Длина среза.</param>
public readonly record struct TextSpan(int Start, int Length);
