namespace AddinManager.Localization;

/// <summary>Язык интерфейса: из системы, русский, английский.</summary>
public enum AppLanguage
{
    /// <summary>Язык системы: русская система — русский, любая другая — английский.</summary>
    System,

    /// <summary>Всегда русский, независимо от системы.</summary>
    Russian,

    /// <summary>Всегда английский, независимо от системы.</summary>
    English,
}
