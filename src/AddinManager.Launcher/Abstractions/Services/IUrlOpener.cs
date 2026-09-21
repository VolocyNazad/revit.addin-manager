namespace AddinManager.Launcher.Abstractions.Services;

/// <summary>Открытие ссылки в системном браузере (за пределами приложения).</summary>
public interface IUrlOpener
{
    /// <summary>Открывает <paramref name="url"/> в браузере по умолчанию.</summary>
    void Open(string url);
}
